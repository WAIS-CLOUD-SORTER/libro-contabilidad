using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class EmployeeDialog : Window
{
    private readonly Employee? _employee;

    public EmployeeDialog(Employee? employee = null)
    {
        InitializeComponent();
        _employee = employee;

        if (employee is null)
        {
            StartDate.SelectedDate = DateTime.Today;
            StatusBox.SelectedIndex = 0;
        }
        else
        {
            TitleText.Text = "Editar empleado";
            SubtitleText.Text = "Actualiza los datos laborales y fiscales del empleado.";
            NameBox.Text = employee.Name;
            RfcBox.Text = employee.Rfc;
            CurpBox.Text = employee.Curp;
            NssBox.Text = employee.Nss;
            PositionBox.Text = employee.Position;
            DepartmentBox.Text = employee.Department;
            SalaryBox.Text = Money.CentsToInput(employee.SalaryCents);
            StartDate.SelectedDate = ParseDate(employee.StartDate);
            EndDate.SelectedDate = ParseDate(employee.EndDate);
            StatusBox.SelectedIndex = employee.Status == "inactivo" ? 1 : 0;
            BankNameBox.Text = employee.BankName;
            BankAccountBox.Text = employee.BankAccount;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Escribe el nombre del empleado.";
            return;
        }
        if (StartDate.SelectedDate is null)
        {
            ErrorText.Text = "Selecciona la fecha de ingreso.";
            return;
        }

        var input = new EmployeeInput
        {
            Id = _employee?.Id ?? 0,
            Name = NameBox.Text.Trim(),
            Rfc = RfcBox.Text.Trim(),
            Curp = CurpBox.Text.Trim(),
            Nss = NssBox.Text.Trim(),
            Position = PositionBox.Text.Trim(),
            Department = DepartmentBox.Text.Trim(),
            SalaryCents = Money.ParseAmount(SalaryBox.Text) ?? 0,
            StartDate = StartDate.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = EndDate.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Status = (StatusBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "activo",
            BankName = BankNameBox.Text.Trim(),
            BankAccount = BankAccountBox.Text.Trim(),
        };

        try
        {
            EmployeeService.SaveEmployee(input);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar empleado", ex);
            ErrorText.Text = ex.Message;
        }
    }

    private static DateTime? ParseDate(string? iso) =>
        DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
