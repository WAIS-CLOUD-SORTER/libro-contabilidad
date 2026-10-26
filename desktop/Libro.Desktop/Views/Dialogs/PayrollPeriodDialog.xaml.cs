using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class PayrollPeriodDialog : Window
{
    private bool _ready;

    public long CreatedPeriodId { get; private set; }

    public PayrollPeriodDialog()
    {
        InitializeComponent();

        for (var month = 1; month <= 12; month++)
            MonthBox.Items.Add(Dates.MonthLabel(month));

        var today = DateTime.Today;
        YearBox.Text = today.Year.ToString();
        MonthBox.SelectedIndex = today.Month - 1;
        TypeBox.SelectedIndex = 0;
        QuincenaBox.SelectedIndex = 0;
        _ready = true;
        UpdatePreview();
    }

    private void Period_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private (int Year, int Month, string Type, int Quincena)? ReadPeriod()
    {
        if (!int.TryParse(YearBox.Text.Trim(), out var year) || year is < 2000 or > 2200)
        {
            ErrorText.Text = "Escribe un año válido.";
            return null;
        }
        if (MonthBox.SelectedIndex is < 0 or > 11)
        {
            ErrorText.Text = "Selecciona el mes.";
            return null;
        }
        var type = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "mensual";
        var quincena = 0;
        if (type == "quincenal")
        {
            quincena = int.TryParse((QuincenaBox.SelectedItem as ComboBoxItem)?.Tag as string, out var q)
                ? q
                : 1;
        }
        return (year, MonthBox.SelectedIndex + 1, type, quincena);
    }

    private void UpdatePreview()
    {
        if (!_ready) return;
        ErrorText.Text = "";
        QuincenaBox.IsEnabled = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string == "quincenal";
        var period = ReadPeriod();
        if (period is null) return;
        var (year, month, type, quincena) = period.Value;
        var (monthStart, monthEnd) = Dates.MonthRange(year, month);
        var start = type == "quincenal" ? (quincena == 1 ? monthStart : $"{year:0000}-{month:00}-16") : monthStart;
        var end = type == "quincenal" ? (quincena == 1 ? $"{year:0000}-{month:00}-15" : monthEnd) : monthEnd;
        RangeText.Text = $"{Dates.FormatDate(start)} a {Dates.FormatDate(end)}";

        var employees = EmployeeService.GetEmployees(false).Count(employee =>
            string.CompareOrdinal(employee.StartDate, end) <= 0 &&
            (employee.EndDate is null || string.CompareOrdinal(employee.EndDate, start) >= 0));
        EmployeesText.Text = employees == 0
            ? "No hay empleados activos en este periodo."
            : employees == 1 ? "1 empleado activo" : $"{employees} empleados activos";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var period = ReadPeriod();
        if (period is null) return;
        try
        {
            var (year, month, type, quincena) = period.Value;
            CreatedPeriodId = PayrollService.CreatePeriod(year, month, type, quincena);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Crear nómina", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
