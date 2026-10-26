using Libro.Desktop.Data;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed class DashboardData
{
    public string RangeStart { get; set; } = "";
    public string RangeEnd { get; set; } = "";
    public long RevenueCents { get; set; }
    public long OtherIncomeCents { get; set; }
    public long ExpenseCents { get; set; }
    public long ProfitCents { get; set; }
    public long TotalIncomeCents => RevenueCents + OtherIncomeCents;
    public long IvaTrasladadoCents { get; set; }
    public long IvaAcreditableCents { get; set; }
    public long ReceivableCents { get; set; }
    public long OverdueCount { get; set; }
    public long CashCents { get; set; }
    public long InventoryCents { get; set; }
    public List<Product> LowStock { get; set; } = new();
    public List<Invoice> RecentInvoices { get; set; } = new();
    public List<TransactionRow> RecentTransactions { get; set; } = new();
    public List<MonthlyPoint> Monthly { get; set; } = new();
}

public sealed class MonthlyPoint
{
    public string Label { get; set; } = "";
    public long IncomeCents { get; set; }
    public long ExpenseCents { get; set; }
    public double IncomeHeight { get; set; }
    public double ExpenseHeight { get; set; }
}

public sealed class AmountPair
{
    public long Subtotal { get; set; }
    public long Iva { get; set; }
}

public sealed class ReceivablePair
{
    public long Total { get; set; }
    public long Count { get; set; }
}

public static class DashboardService
{
    public static DashboardData GetDashboard(Database db)
    {
        var (start, end) = Dates.CurrentMonthRange();
        var today = Dates.TodayIso();

        var invoiced = db.One<AmountPair>(
            """
            SELECT COALESCE(SUM(subtotal_cents), 0) AS subtotal,
                   COALESCE(SUM(iva_cents), 0) AS iva
            FROM invoices
            WHERE status IN ('emitida','pagada') AND date BETWEEN $start AND $end;
            """,
            new { start, end }) ?? new AmountPair();

        var otherIncome = db.One<AmountPair>(
            """
            SELECT COALESCE(SUM(subtotal_cents), 0) AS subtotal,
                   COALESCE(SUM(iva_cents), 0) AS iva
            FROM transactions
            WHERE type = 'ingreso' AND date BETWEEN $start AND $end;
            """,
            new { start, end }) ?? new AmountPair();

        var expenses = db.One<AmountPair>(
            """
            SELECT COALESCE(SUM(subtotal_cents), 0) AS subtotal,
                   COALESCE(SUM(iva_cents), 0) AS iva
            FROM transactions
            WHERE type = 'gasto' AND date BETWEEN $start AND $end;
            """,
            new { start, end }) ?? new AmountPair();

        var receivable = db.One<ReceivablePair>(
            """
            SELECT COALESCE(SUM(total_cents - paid_cents), 0) AS total, COUNT(*) AS count
            FROM invoices
            WHERE status = 'emitida' AND date <= $today;
            """,
            new { today }) ?? new ReceivablePair();

        var overdueCount = db.ScalarLong(
            """
            SELECT COUNT(*) FROM invoices
            WHERE status = 'emitida' AND due_date < $today AND (total_cents - paid_cents) > 0;
            """,
            new { today });

        var cashCents = CompanyService.GetAccountBalances(db).Sum(account => account.BalanceCents);
        var inventoryCents = db.ScalarLong(
            """
            SELECT COALESCE(SUM(CAST(ROUND(stock * cost_cents) AS INTEGER)), 0)
            FROM products WHERE active = 1 AND track_stock = 1;
            """);

        var monthly = Dates.LastMonths(6).Select(month =>
        {
            var incomeInvoices = db.ScalarLong(
                "SELECT COALESCE(SUM(subtotal_cents),0) FROM invoices WHERE status IN ('emitida','pagada') AND date BETWEEN $start AND $end;",
                new { start = month.Start, end = month.End });
            var other = db.ScalarLong(
                "SELECT COALESCE(SUM(subtotal_cents),0) FROM transactions WHERE type = 'ingreso' AND date BETWEEN $start AND $end;",
                new { start = month.Start, end = month.End });
            var expense = db.ScalarLong(
                "SELECT COALESCE(SUM(subtotal_cents),0) FROM transactions WHERE type = 'gasto' AND date BETWEEN $start AND $end;",
                new { start = month.Start, end = month.End });
            return new MonthlyPoint
            {
                Label = month.Label,
                IncomeCents = incomeInvoices + other,
                ExpenseCents = expense,
            };
        }).ToList();

        var max = Math.Max(1, monthly.Max(point => Math.Max(point.IncomeCents, point.ExpenseCents)));
        foreach (var point in monthly)
        {
            point.IncomeHeight = Math.Max(2, Math.Round(point.IncomeCents / (double)max * 108, 0));
            point.ExpenseHeight = Math.Max(2, Math.Round(point.ExpenseCents / (double)max * 108, 0));
        }

        var revenueCents = invoiced.Subtotal;
        var otherIncomeCents = otherIncome.Subtotal;
        var expenseCents = expenses.Subtotal;

        return new DashboardData
        {
            RangeStart = start,
            RangeEnd = end,
            RevenueCents = revenueCents,
            OtherIncomeCents = otherIncomeCents,
            ExpenseCents = expenseCents,
            ProfitCents = revenueCents + otherIncomeCents - expenseCents,
            IvaTrasladadoCents = invoiced.Iva + otherIncome.Iva,
            IvaAcreditableCents = expenses.Iva,
            ReceivableCents = receivable.Total,
            OverdueCount = overdueCount,
            CashCents = cashCents,
            InventoryCents = inventoryCents,
            Monthly = monthly,
            LowStock = db.All<Product>(
                """
                SELECT * FROM products
                WHERE active = 1 AND track_stock = 1 AND stock <= min_stock
                ORDER BY (stock - min_stock) ASC LIMIT 5;
                """),
            RecentInvoices = db.All<Invoice>(
                """
                SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc
                FROM invoices i JOIN contacts c ON c.id = i.contact_id
                ORDER BY i.date DESC, i.id DESC LIMIT 5;
                """),
            RecentTransactions = db.All<TransactionRow>(
                """
                SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
                       c.name AS contact_name, a.name AS account_name
                FROM transactions t
                LEFT JOIN categories cat ON cat.id = t.category_id
                LEFT JOIN contacts c ON c.id = t.contact_id
                LEFT JOIN accounts a ON a.id = t.account_id
                ORDER BY t.date DESC, t.id DESC LIMIT 5;
                """),
        };
    }
}
