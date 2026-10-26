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

public partial class TaxesView : UserControl, IRefreshable
{
    private bool _initialized;
    private TaxSummary? _summary;

    public TaxesView()
    {
        InitializeComponent();
        for (var month = 1; month <= 12; month++)
            MonthBox.Items.Add(Dates.MonthLabel(month));
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            var today = DateTime.Today;
            YearBox.Text = today.Year.ToString();
            MonthBox.SelectedIndex = today.Month - 1;
            _initialized = true;
        }
        LoadTaxes();
    }

    private (int Year, int Month)? ReadPeriod()
    {
        if (!int.TryParse(YearBox.Text.Trim(), out var year) || year is < 2000 or > 2200)
        {
            MessageBox.Show("Escribe un año válido.", "Libro · Impuestos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        if (MonthBox.SelectedIndex is < 0 or > 11)
        {
            MessageBox.Show("Selecciona el mes.", "Libro · Impuestos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        return (year, MonthBox.SelectedIndex + 1);
    }

    private void LoadTaxes()
    {
        var period = ReadPeriod();
        if (period is null) return;
        var (year, month) = period.Value;
        try
        {
            var (start, end) = Dates.MonthRange(year, month);
            var summary = TaxService.GetSummary(start, end);
            var annual = TaxService.GetAnnual(year);
            _summary = summary;

            PeriodText.Text = $"{Dates.MonthLabel(month)} {year} · {Dates.FormatDate(start)} a {Dates.FormatDate(end)}";
            TransferredValue.Text = Money.Format(summary.IvaTrasladadoCents);
            CreditableValue.Text = Money.Format(summary.IvaAcreditableCents);
            NetValue.Text = Money.Format(summary.IvaNetoCents);
            NetValue.Foreground = (Brush)FindResource(
                summary.IvaNetoCents < 0 ? "SuccessBrush" : "AccentBrush");
            InvoiceCountValue.Text = summary.InvoiceCount.ToString();

            InvoicedSubtotalText.Text = Money.Format(summary.InvoiceSubtotalCents);
            InvoicedIvaText.Text = $"IVA {Money.Format(summary.InvoiceIvaCents)}";
            OtherSubtotalText.Text = Money.Format(summary.OtherIncomeSubtotalCents);
            OtherIvaText.Text = $"IVA {Money.Format(summary.OtherIncomeIvaCents)}";
            ExpenseSubtotalText.Text = Money.Format(summary.ExpenseSubtotalCents);
            ExpenseIvaText.Text = $"IVA {Money.Format(summary.ExpenseIvaCents)}";

            IncomeGrid.ItemsSource = summary.IncomeRows;
            AnnualGrid.ItemsSource = annual;

            var transferred = annual.Sum(row => row.TrasladadoCents);
            var creditable = annual.Sum(row => row.AcreditableCents);
            var net = annual.Sum(row => row.NetoCents);
            AnnualTotalText.Text =
                $"Acumulado {year}: trasladado {Money.Format(transferred)} · " +
                $"acreditable {Money.Format(creditable)} · neto {Money.Format(net)}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Impuestos", ex);
            MessageBox.Show("No se pudieron cargar los impuestos.\n\n" + ex.Message,
                "Libro · Impuestos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => LoadTaxes();

    private void Current_Click(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        YearBox.Text = today.Year.ToString();
        MonthBox.SelectedIndex = today.Month - 1;
        LoadTaxes();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_summary is null)
        {
            MessageBox.Show("Primero consulta un periodo.", "Libro · Impuestos",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exportar IVA del mes",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"IVA-{_summary.From}-a-{_summary.To}.csv",
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("Tipo,Folio,Fecha,Cliente,Detalle,Subtotal,IVA,Total");
            foreach (var row in _summary.IncomeRows)
            {
                csv.AppendLine(string.Join(",",
                    Csv(row.Kind), Csv(row.Folio), Csv(row.Date), Csv(row.Name), Csv(row.Detail),
                    Money.CentsToInput(row.SubtotalCents), Money.CentsToInput(row.IvaCents),
                    Money.CentsToInput(row.TotalCents)));
            }
            foreach (var expense in _summary.Expenses)
            {
                csv.AppendLine(string.Join(",",
                    "Gasto", $"MOV-{expense.Id}", Csv(expense.Date), Csv(expense.ContactName),
                    Csv(expense.Description), Money.CentsToInput(expense.SubtotalCents),
                    Money.CentsToInput(expense.IvaCents), Money.CentsToInput(expense.TotalCents)));
            }
            csv.AppendLine();
            csv.AppendLine($"IVA trasladado,{Money.CentsToInput(_summary.IvaTrasladadoCents)}");
            csv.AppendLine($"IVA acreditable,{Money.CentsToInput(_summary.IvaAcreditableCents)}");
            csv.AppendLine($"IVA neto,{Money.CentsToInput(_summary.IvaNetoCents)}");
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show("IVA exportado correctamente.", "Libro · Impuestos",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Exportar IVA", ex);
            MessageBox.Show("No se pudo exportar el IVA.\n\n" + ex.Message,
                "Libro · Impuestos", MessageBoxButton.OK, MessageBoxImage.Error);
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
