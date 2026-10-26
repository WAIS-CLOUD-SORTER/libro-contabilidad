using System.Globalization;
using Libro.Desktop.Data;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed class IncomeStatement
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public long InvoicedRevenueCents { get; set; }
    public long OtherIncomeCents { get; set; }
    public long TotalIncomeCents => InvoicedRevenueCents + OtherIncomeCents;
    public long TotalExpensesCents { get; set; }
    public long OperatingProfitCents => TotalIncomeCents - TotalExpensesCents;
    public long CogsReferenceCents { get; set; }
    public long GrossMarginReferenceCents => InvoicedRevenueCents - CogsReferenceCents;
    public List<ExpenseCategoryRow> ExpensesByCategory { get; set; } = new();
}

public sealed class ExpenseCategoryRow
{
    public string Name { get; set; } = "";
    public long AmountCents { get; set; }
}

public sealed class MonthlyPerformanceRow
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public long InvoicedCents { get; set; }
    public long OtherIncomeCents { get; set; }
    public long IncomeCents => InvoicedCents + OtherIncomeCents;
    public long ExpenseCents { get; set; }
    public long ProfitCents => IncomeCents - ExpenseCents;
}

public sealed class CashflowMonthRow
{
    public string Label { get; set; } = "";
    public long InflowCents { get; set; }
    public long OutflowCents { get; set; }
    public long NetCents => InflowCents - OutflowCents;
}

public sealed class CashflowSummary
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public long InvoicePaymentsCents { get; set; }
    public long OtherIncomeCents { get; set; }
    public long ExpensesCents { get; set; }
    public long NetCents => InvoicePaymentsCents + OtherIncomeCents - ExpensesCents;
    public List<CashflowMonthRow> Monthly { get; set; } = new();
}

public sealed class AgingBucketRow
{
    public string Label { get; set; } = "";
    public long AmountCents { get; set; }
    public long Count { get; set; }
}

public sealed class AgingInvoiceRow
{
    public long Id { get; set; }
    public string Folio { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string DueDate { get; set; } = "";
    public long BalanceCents { get; set; }
    public long OverdueDays { get; set; }
    public string Bucket { get; set; } = "";
    public string DueLabel => OverdueDays > 0
        ? $"{OverdueDays} días vencida"
        : "Por vencer";
}

public sealed class AgingResult
{
    public List<AgingBucketRow> Buckets { get; set; } = new();
    public List<AgingInvoiceRow> Invoices { get; set; } = new();
    public long TotalCents => Buckets.Sum(bucket => bucket.AmountCents);
}

internal sealed class ReportSumRow
{
    public long Total { get; set; }
}

public static class ReportService
{
    public static IncomeStatement GetIncomeStatement(string from, string to)
    {
        var invoiced = AppState.Db.One<ReportSumRow>(
            """
            SELECT COALESCE(SUM(subtotal_cents), 0) AS total FROM invoices
            WHERE status IN ('emitida', 'pagada') AND date BETWEEN $from AND $to;
            """, new { from, to })?.Total ?? 0;
        var other = AppState.Db.One<ReportSumRow>(
            """
            SELECT COALESCE(SUM(subtotal_cents), 0) AS total FROM transactions
            WHERE type = 'ingreso' AND date BETWEEN $from AND $to;
            """, new { from, to })?.Total ?? 0;
        var cogs = AppState.Db.One<ReportSumRow>(
            """
            SELECT COALESCE(SUM(CAST(ROUND(ii.qty * ii.unit_cost_cents) AS INTEGER)), 0) AS total
            FROM invoice_items ii
            JOIN invoices i ON i.id = ii.invoice_id
            WHERE i.status IN ('emitida', 'pagada') AND i.date BETWEEN $from AND $to;
            """, new { from, to })?.Total ?? 0;
        var categories = AppState.Db.All<ExpenseCategoryRow>(
            """
            SELECT COALESCE(cat.name, 'Sin categoría') AS name,
                   COALESCE(SUM(t.subtotal_cents), 0) AS amount_cents
            FROM transactions t
            LEFT JOIN categories cat ON cat.id = t.category_id
            WHERE t.type = 'gasto' AND t.date BETWEEN $from AND $to
            GROUP BY COALESCE(cat.name, 'Sin categoría')
            ORDER BY amount_cents DESC;
            """, new { from, to });

        return new IncomeStatement
        {
            From = from,
            To = to,
            InvoicedRevenueCents = invoiced,
            OtherIncomeCents = other,
            TotalExpensesCents = categories.Sum(category => category.AmountCents),
            CogsReferenceCents = cogs,
            ExpensesByCategory = categories,
        };
    }

    public static List<MonthlyPerformanceRow> GetMonthlyPerformance(string from, string to)
    {
        var rows = new List<MonthlyPerformanceRow>();
        if (!DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var end) ||
            start > end)
            return rows;

        var cursor = new DateTime(start.Year, start.Month, 1);
        while (cursor <= end)
        {
            var (monthStart, monthEnd) = Dates.MonthRange(cursor.Year, cursor.Month);
            var queryStart = string.CompareOrdinal(monthStart, from) < 0 ? from : monthStart;
            var queryEnd = string.CompareOrdinal(monthEnd, to) > 0 ? to : monthEnd;
            var invoiced = AppState.Db.One<ReportSumRow>(
                """
                SELECT COALESCE(SUM(subtotal_cents), 0) AS total FROM invoices
                WHERE status IN ('emitida', 'pagada') AND date BETWEEN $from AND $to;
                """, new { from = queryStart, to = queryEnd })?.Total ?? 0;
            var other = AppState.Db.One<ReportSumRow>(
                """
                SELECT COALESCE(SUM(subtotal_cents), 0) AS total FROM transactions
                WHERE type = 'ingreso' AND date BETWEEN $from AND $to;
                """, new { from = queryStart, to = queryEnd })?.Total ?? 0;
            var expenses = AppState.Db.One<ReportSumRow>(
                """
                SELECT COALESCE(SUM(subtotal_cents), 0) AS total FROM transactions
                WHERE type = 'gasto' AND date BETWEEN $from AND $to;
                """, new { from = queryStart, to = queryEnd })?.Total ?? 0;

            rows.Add(new MonthlyPerformanceRow
            {
                Key = $"{cursor.Year:0000}-{cursor.Month:00}",
                Label = Dates.ShortMonthLabel(cursor.Year, cursor.Month),
                InvoicedCents = invoiced,
                OtherIncomeCents = other,
                ExpenseCents = expenses,
            });
            cursor = cursor.AddMonths(1);
        }
        return rows;
    }

    public static CashflowSummary GetCashflow(string from, string to)
    {
        var payments = AppState.Db.One<ReportSumRow>(
            "SELECT COALESCE(SUM(amount_cents), 0) AS total FROM payments WHERE date BETWEEN $from AND $to;",
            new { from, to })?.Total ?? 0;
        var income = AppState.Db.One<ReportSumRow>(
            """
            SELECT COALESCE(SUM(total_cents), 0) AS total FROM transactions
            WHERE type = 'ingreso' AND date BETWEEN $from AND $to;
            """, new { from, to })?.Total ?? 0;
        var expenses = AppState.Db.One<ReportSumRow>(
            """
            SELECT COALESCE(SUM(total_cents), 0) AS total FROM transactions
            WHERE type = 'gasto' AND date BETWEEN $from AND $to;
            """, new { from, to })?.Total ?? 0;

        var monthly = new List<CashflowMonthRow>();
        if (DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start) &&
            DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var end) && start <= end)
        {
            var cursor = new DateTime(start.Year, start.Month, 1);
            while (cursor <= end)
            {
                var (monthStart, monthEnd) = Dates.MonthRange(cursor.Year, cursor.Month);
                var queryStart = string.CompareOrdinal(monthStart, from) < 0 ? from : monthStart;
                var queryEnd = string.CompareOrdinal(monthEnd, to) > 0 ? to : monthEnd;
                var monthPayments = AppState.Db.One<ReportSumRow>(
                    "SELECT COALESCE(SUM(amount_cents), 0) AS total FROM payments WHERE date BETWEEN $from AND $to;",
                    new { from = queryStart, to = queryEnd })?.Total ?? 0;
                var monthIncome = AppState.Db.One<ReportSumRow>(
                    """
                    SELECT COALESCE(SUM(total_cents), 0) AS total FROM transactions
                    WHERE type = 'ingreso' AND date BETWEEN $from AND $to;
                    """, new { from = queryStart, to = queryEnd })?.Total ?? 0;
                var monthExpenses = AppState.Db.One<ReportSumRow>(
                    """
                    SELECT COALESCE(SUM(total_cents), 0) AS total FROM transactions
                    WHERE type = 'gasto' AND date BETWEEN $from AND $to;
                    """, new { from = queryStart, to = queryEnd })?.Total ?? 0;
                monthly.Add(new CashflowMonthRow
                {
                    Label = Dates.ShortMonthLabel(cursor.Year, cursor.Month),
                    InflowCents = monthPayments + monthIncome,
                    OutflowCents = monthExpenses,
                });
                cursor = cursor.AddMonths(1);
            }
        }

        return new CashflowSummary
        {
            From = from,
            To = to,
            InvoicePaymentsCents = payments,
            OtherIncomeCents = income,
            ExpensesCents = expenses,
            Monthly = monthly,
        };
    }

    public static AgingResult GetReceivableAging()
    {
        var today = Dates.TodayIso();
        var invoices = AppState.Db.All<Invoice>(
            """
            SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc
            FROM invoices i JOIN contacts c ON c.id = i.contact_id
            WHERE i.status = 'emitida' AND (i.total_cents - i.paid_cents) > 0
            ORDER BY i.due_date;
            """);
        var labels = new[]
        {
            "Por vencer", "1 a 30 días", "31 a 60 días", "61 a 90 días", "Más de 90 días",
        };
        var buckets = labels.Select(label => new AgingBucketRow { Label = label }).ToList();
        var rows = new List<AgingInvoiceRow>();

        foreach (var invoice in invoices)
        {
            var overdue = Dates.DiffDays(invoice.DueDate, today);
            var index = overdue <= 0 ? 0 : overdue <= 30 ? 1 : overdue <= 60 ? 2 : overdue <= 90 ? 3 : 4;
            var balance = invoice.TotalCents - invoice.PaidCents;
            buckets[index].AmountCents += balance;
            buckets[index].Count++;
            rows.Add(new AgingInvoiceRow
            {
                Id = invoice.Id,
                Folio = invoice.DisplayFolio,
                ContactName = invoice.ContactName ?? "—",
                DueDate = invoice.DueDate,
                BalanceCents = balance,
                OverdueDays = Math.Max(0, overdue),
                Bucket = labels[index],
            });
        }

        return new AgingResult { Buckets = buckets, Invoices = rows };
    }

    public static List<AccountBalance> GetAccountBalances() =>
        CompanyService.GetAccountBalances(AppState.Db);
}
