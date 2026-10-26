using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Microsoft.Win32;

namespace Libro.Desktop.Views;

public sealed class ExpenseBarRow
{
    public string Name { get; set; } = "";
    public long AmountCents { get; set; }
    public double Percent { get; set; }
    public string AmountLabel => Money.Format(AmountCents);
}

public sealed class AgingListRow
{
    public string Label { get; set; } = "";
    public long Count { get; set; }
    public long AmountCents { get; set; }
    public string AmountLabel => Money.Format(AmountCents);
}

public sealed class AccountBalanceRow
{
    public string Name { get; set; } = "";
    public long BalanceCents { get; set; }
    public string BalanceLabel => Money.Format(BalanceCents);
}

public partial class ReportsView : UserControl, IRefreshable
{
    private bool _initialized;
    private string _from = "";
    private string _to = "";

    public ReportsView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            var today = DateTime.Today;
            FromField.SelectedDate = new DateTime(today.Year, 1, 1);
            ToField.SelectedDate = new DateTime(today.Year, 12, 31);
            _initialized = true;
        }
        LoadReports();
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => LoadReports();

    private void Month_Click(object sender, RoutedEventArgs e)
    {
        var (start, end) = Dates.CurrentMonthRange();
        SetRange(start, end);
        LoadReports();
    }

    private void Year_Click(object sender, RoutedEventArgs e)
    {
        var year = DateTime.Today.Year;
        var (start, end) = Dates.YearRange(year);
        SetRange(start, end);
        LoadReports();
    }

    private void SetRange(string from, string to)
    {
        FromField.SelectedDate = DateTime.ParseExact(from, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
        ToField.SelectedDate = DateTime.ParseExact(to, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private bool ReadRange()
    {
        if (FromField.SelectedDate is null || ToField.SelectedDate is null)
        {
            MessageBox.Show("Selecciona las fechas desde y hasta.", "Libro · Reportes",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var from = FromField.SelectedDate.Value;
        var to = ToField.SelectedDate.Value;
        if (from > to) (from, to) = (to, from);
        FromField.SelectedDate = from;
        ToField.SelectedDate = to;
        _from = from.ToString("yyyy-MM-dd");
        _to = to.ToString("yyyy-MM-dd");
        RangeText.Text = $"{Dates.FormatDate(_from)} a {Dates.FormatDate(_to)}";
        return true;
    }

    private void LoadReports()
    {
        if (!ReadRange()) return;
        try
        {
            var statement = ReportService.GetIncomeStatement(_from, _to);
            var cashflow = ReportService.GetCashflow(_from, _to);
            var aging = ReportService.GetReceivableAging();
            var performance = ReportService.GetMonthlyPerformance(_from, _to);
            var inventory = InventoryService.GetSummary();
            var accounts = CompanyService.GetAccountBalances(AppState.Db);

            IncomeValue.Text = Money.Format(statement.TotalIncomeCents);
            ExpenseValue.Text = Money.Format(statement.TotalExpensesCents);
            ProfitValue.Text = Money.Format(statement.OperatingProfitCents);
            ProfitValue.Foreground = (Brush)FindResource(
                statement.OperatingProfitCents < 0 ? "DangerBrush" : "InkBrush");
            CashflowValue.Text = Money.Format(cashflow.NetCents);
            CashflowValue.Foreground = (Brush)FindResource(
                cashflow.NetCents < 0 ? "DangerBrush" : "InkBrush");

            InvoicedRevenueText.Text = Money.Format(statement.InvoicedRevenueCents);
            OtherIncomeText.Text = Money.Format(statement.OtherIncomeCents);
            TotalIncomeText.Text = Money.Format(statement.TotalIncomeCents);
            TotalExpenseText.Text = Money.Format(statement.TotalExpensesCents);
            OperatingProfitText.Text = Money.Format(statement.OperatingProfitCents);
            OperatingProfitText.Foreground = (Brush)FindResource(
                statement.OperatingProfitCents < 0 ? "DangerBrush" : "AccentBrush");
            CogsText.Text = Money.Format(statement.CogsReferenceCents);
            MarginText.Text = Money.Format(statement.GrossMarginReferenceCents);

            PerformanceGrid.ItemsSource = performance;
            PerformanceSubtitle.Text = $"{performance.Count} mes(es) · base sin IVA";

            var maxExpense = statement.ExpensesByCategory.Count > 0
                ? statement.ExpensesByCategory[0].AmountCents
                : 0;
            CategoryList.ItemsSource = statement.ExpensesByCategory
                .Select(category => new ExpenseBarRow
                {
                    Name = category.Name,
                    AmountCents = category.AmountCents,
                    Percent = maxExpense > 0 ? category.AmountCents * 100d / maxExpense : 0,
                }).ToList();
            CategorySubtitle.Text = $"{statement.ExpensesByCategory.Count} categoría(s) · sin IVA";
            NoCategoriesText.Visibility = statement.ExpensesByCategory.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            AgingList.ItemsSource = aging.Buckets
                .Select(bucket => new AgingListRow
                {
                    Label = bucket.Label,
                    Count = bucket.Count,
                    AmountCents = bucket.AmountCents,
                }).ToList();
            AgingTotalText.Text = Money.Format(aging.TotalCents);
            AgingGrid.ItemsSource = aging.Invoices;
            AgingInvoicesSubtitle.Text = aging.Invoices.Count == 1
                ? "1 factura"
                : $"{aging.Invoices.Count} facturas";

            AccountsList.ItemsSource = accounts
                .Select(account => new AccountBalanceRow
                {
                    Name = account.Name,
                    BalanceCents = account.BalanceCents,
                }).ToList();

            InventoryValueText.Text = Money.Format(inventory.ValueCents);
            InventoryCountText.Text = inventory.ProductCount.ToString();
            InventoryLowText.Text = $"{inventory.LowStockCount} / {inventory.OutOfStockCount}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Reportes", ex);
            MessageBox.Show("No se pudieron cargar los reportes.\n\n" + ex.Message,
                "Libro · Reportes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportMovements_Click(object sender, RoutedEventArgs e)
    {
        if (!ReadRange()) return;
        try
        {
            var rows = TransactionService.GetTransactions(
                new TransactionFilters { From = _from, To = _to }, 10000).Rows;
            var dialog = new SaveFileDialog
            {
                Title = "Exportar movimientos",
                Filter = "Archivo CSV (*.csv)|*.csv",
                FileName = $"Movimientos-{_from}-a-{_to}.csv",
                AddExtension = true,
            };
            if (dialog.ShowDialog() != true) return;
            var csv = new StringBuilder();
            csv.AppendLine("Fecha,Tipo,Categoría,Contacto,Descripción,Subtotal,IVA,Total,Cuenta,Origen");
            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",",
                    Csv(row.Date), Csv(row.Type), Csv(row.CategoryName), Csv(row.ContactName),
                    Csv(row.Description), Money.CentsToInput(row.SubtotalCents),
                    Money.CentsToInput(row.IvaCents), Money.CentsToInput(row.TotalCents),
                    Csv(row.AccountName), Csv(row.Source)));
            }
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Movimientos exportados correctamente.", "Libro · Reportes",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Exportar movimientos", ex);
            MessageBox.Show("No se pudieron exportar los movimientos.\n\n" + ex.Message,
                "Libro · Reportes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportInvoices_Click(object sender, RoutedEventArgs e)
    {
        if (!ReadRange()) return;
        try
        {
            var rows = InvoiceService.GetInvoices(
                new InvoiceFilters { From = _from, To = _to }, 10000).Rows;
            var dialog = new SaveFileDialog
            {
                Title = "Exportar facturas",
                Filter = "Archivo CSV (*.csv)|*.csv",
                FileName = $"Facturas-{_from}-a-{_to}.csv",
                AddExtension = true,
            };
            if (dialog.ShowDialog() != true) return;
            var csv = new StringBuilder();
            csv.AppendLine("Folio,Fecha,Vence,Cliente,Estado,Subtotal,IVA,Total,Pagado,Saldo");
            foreach (var invoice in rows)
            {
                csv.AppendLine(string.Join(",",
                    Csv(invoice.DisplayFolio), Csv(invoice.Date), Csv(invoice.DueDate),
                    Csv(invoice.ContactName), Csv(invoice.Status),
                    Money.CentsToInput(invoice.SubtotalCents), Money.CentsToInput(invoice.IvaCents),
                    Money.CentsToInput(invoice.TotalCents), Money.CentsToInput(invoice.PaidCents),
                    Money.CentsToInput(invoice.BalanceCents)));
            }
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Facturas exportadas correctamente.", "Libro · Reportes",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Exportar facturas", ex);
            MessageBox.Show("No se pudieron exportar las facturas.\n\n" + ex.Message,
                "Libro · Reportes", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string Csv(string? value)
    {
        value ??= "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
