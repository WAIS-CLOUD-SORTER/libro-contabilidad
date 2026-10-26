using Libro.Desktop.Data;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed class TaxSummary
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public long InvoiceCount { get; set; }
    public long InvoiceSubtotalCents { get; set; }
    public long InvoiceIvaCents { get; set; }
    public long OtherIncomeSubtotalCents { get; set; }
    public long OtherIncomeIvaCents { get; set; }
    public long ExpenseSubtotalCents { get; set; }
    public long ExpenseIvaCents { get; set; }
    public long IvaTrasladadoCents => InvoiceIvaCents + OtherIncomeIvaCents;
    public long IvaAcreditableCents => ExpenseIvaCents;
    public long IvaNetoCents => IvaTrasladadoCents - IvaAcreditableCents;
    public List<TaxIncomeRow> IncomeRows { get; set; } = new();
    public List<TransactionRow> Expenses { get; set; } = new();
}

public sealed class TaxIncomeRow
{
    public string Kind { get; set; } = "";
    public string Folio { get; set; } = "";
    public string Date { get; set; } = "";
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public long SubtotalCents { get; set; }
    public long IvaCents { get; set; }
    public long TotalCents { get; set; }
}

public sealed class MonthlyTaxRow
{
    public int Month { get; set; }
    public string Label { get; set; } = "";
    public long TrasladadoCents { get; set; }
    public long AcreditableCents { get; set; }
    public long NetoCents { get; set; }
}

public static class TaxService
{
    public static TaxSummary GetSummary(string from, string to)
    {
        var invoices = AppState.Db.All<Invoice>(
            """
            SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc
            FROM invoices i
            JOIN contacts c ON c.id = i.contact_id
            WHERE i.status IN ('emitida', 'pagada') AND i.date BETWEEN $from AND $to
            ORDER BY i.date, i.id;
            """, new { from, to });

        var income = AppState.Db.All<TransactionRow>(
            """
            SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
                   c.name AS contact_name, a.name AS account_name
            FROM transactions t
            LEFT JOIN categories cat ON cat.id = t.category_id
            LEFT JOIN contacts c ON c.id = t.contact_id
            LEFT JOIN accounts a ON a.id = t.account_id
            WHERE t.type = 'ingreso' AND t.date BETWEEN $from AND $to
            ORDER BY t.date, t.id;
            """, new { from, to });

        var expenses = AppState.Db.All<TransactionRow>(
            """
            SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
                   c.name AS contact_name, a.name AS account_name
            FROM transactions t
            LEFT JOIN categories cat ON cat.id = t.category_id
            LEFT JOIN contacts c ON c.id = t.contact_id
            LEFT JOIN accounts a ON a.id = t.account_id
            WHERE t.type = 'gasto' AND t.date BETWEEN $from AND $to
            ORDER BY t.date, t.id;
            """, new { from, to });

        var rows = new List<TaxIncomeRow>();
        rows.AddRange(invoices.Select(invoice => new TaxIncomeRow
        {
            Kind = "Factura",
            Folio = invoice.Number is null ? $"Borrador {invoice.Id}" : invoice.DisplayFolio,
            Date = invoice.Date,
            Name = invoice.ContactName ?? "—",
            Detail = string.IsNullOrWhiteSpace(invoice.Notes) ? "Venta facturada" : invoice.Notes,
            SubtotalCents = invoice.SubtotalCents,
            IvaCents = invoice.IvaCents,
            TotalCents = invoice.TotalCents,
        }));
        rows.AddRange(income.Select(transaction => new TaxIncomeRow
        {
            Kind = "Ingreso",
            Folio = $"MOV-{transaction.Id}",
            Date = transaction.Date,
            Name = transaction.ContactName ?? "—",
            Detail = transaction.Description,
            SubtotalCents = transaction.SubtotalCents,
            IvaCents = transaction.IvaCents,
            TotalCents = transaction.TotalCents,
        }));

        return new TaxSummary
        {
            From = from,
            To = to,
            InvoiceCount = invoices.Count,
            InvoiceSubtotalCents = invoices.Sum(invoice => invoice.SubtotalCents),
            InvoiceIvaCents = invoices.Sum(invoice => invoice.IvaCents),
            OtherIncomeSubtotalCents = income.Sum(transaction => transaction.SubtotalCents),
            OtherIncomeIvaCents = income.Sum(transaction => transaction.IvaCents),
            ExpenseSubtotalCents = expenses.Sum(transaction => transaction.SubtotalCents),
            ExpenseIvaCents = expenses.Sum(transaction => transaction.IvaCents),
            IncomeRows = rows.OrderBy(row => row.Date).ThenBy(row => row.Kind).ToList(),
            Expenses = expenses,
        };
    }

    public static List<MonthlyTaxRow> GetAnnual(int year)
    {
        var result = new List<MonthlyTaxRow>();
        for (var month = 1; month <= 12; month++)
        {
            var (start, end) = Dates.MonthRange(year, month);
            var summary = GetSummary(start, end);
            result.Add(new MonthlyTaxRow
            {
                Month = month,
                Label = Dates.MonthLabel(month),
                TrasladadoCents = summary.IvaTrasladadoCents,
                AcreditableCents = summary.IvaAcreditableCents,
                NetoCents = summary.IvaNetoCents,
            });
        }
        return result;
    }
}
