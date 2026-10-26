using System.Globalization;
using System.Windows;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class PaymentDialog : Window
{
    private readonly Invoice _invoice;

    public PaymentDialog(Invoice invoice)
    {
        InitializeComponent();
        _invoice = invoice;
        SubtitleText.Text = $"{invoice.DisplayFolio} · {invoice.ContactName} · Saldo {Money.Format(invoice.BalanceCents)}";
        AmountBox.Text = Money.CentsToInput(invoice.BalanceCents);
        PaymentDate.SelectedDate = DateTime.Today;

        MethodBox.ItemsSource = new List<string>
        {
            "Transferencia", "Efectivo", "Tarjeta", "Cheque", "Domiciliación", "Otro",
        };
        MethodBox.SelectedIndex = 0;

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
        var amount = Money.ParseAmount(AmountBox.Text);
        if (amount is null or <= 0)
        {
            ErrorText.Text = "Escribe un monto mayor a cero.";
            return;
        }
        if (PaymentDate.SelectedDate is null)
        {
            ErrorText.Text = "Selecciona la fecha del pago.";
            return;
        }

        try
        {
            InvoiceService.RegisterPayment(
                _invoice.Id,
                PaymentDate.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                amount.Value,
                AccountBox.SelectedValue is long accountId ? accountId : null,
                MethodBox.SelectedItem as string ?? "",
                ReferenceBox.Text,
                NotesBox.Text,
                AppState.UserId);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Registrar pago", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
