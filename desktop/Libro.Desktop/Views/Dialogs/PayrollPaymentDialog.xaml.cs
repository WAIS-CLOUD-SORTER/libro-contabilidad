using System.Globalization;
using System.Windows;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class PayrollPaymentDialog : Window
{
    private readonly PayrollPeriod _period;

    public PayrollPaymentDialog(PayrollPeriod period)
    {
        InitializeComponent();
        _period = period;
        SubtitleText.Text = period.Label;
        NetText.Text = Money.Format(period.TotalNetCents);
        EmployeesText.Text = period.EmployeeCount == 1
            ? "1 empleado"
            : $"{period.EmployeeCount} empleados";
        PaymentDate.SelectedDate = DateTime.Today;

        var accounts = CompanyService.GetAccounts(AppState.Db);
        AccountBox.ItemsSource = accounts;
        if (accounts.Count > 0) AccountBox.SelectedIndex = 0;
        else
        {
            ErrorText.Text = "No hay cuentas de efectivo activas.";
            SaveButton.IsEnabled = false;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (AccountBox.SelectedValue is not long accountId)
        {
            ErrorText.Text = "Selecciona la cuenta de pago.";
            return;
        }
        if (PaymentDate.SelectedDate is null)
        {
            ErrorText.Text = "Selecciona la fecha de pago.";
            return;
        }

        try
        {
            PayrollService.Pay(_period.Id, accountId,
                PaymentDate.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Pagar nómina", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
