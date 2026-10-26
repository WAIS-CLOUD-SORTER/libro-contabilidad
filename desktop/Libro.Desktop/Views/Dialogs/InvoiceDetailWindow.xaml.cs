using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class InvoiceDetailWindow : Window
{
    private Invoice _invoice;

    public InvoiceDetailWindow(Invoice invoice)
    {
        InitializeComponent();
        _invoice = invoice;
        Reload();
    }

    private void Reload()
    {
        try
        {
            var invoice = InvoiceService.GetInvoice(_invoice.Id);
            if (invoice is null)
            {
                Close();
                return;
            }
            _invoice = invoice;

            var items = InvoiceService.GetItems(_invoice.Id);
            var payments = InvoiceService.GetPayments(_invoice.Id);
            var canWrite = AppState.Can(Permissions.InvoiceWrite);
            var cancelled = _invoice.Status == "cancelada";
            var draft = _invoice.Status == "borrador";
            var canPay = _invoice.Status is "emitida" or "pagada" && _invoice.BalanceCents > 0;
            var accounts = CompanyService.GetAccounts(AppState.Db);

            Title = $"{_invoice.DisplayFolio} · Libro";
            TitleText.Text = _invoice.DisplayFolio;
            SubtitleText.Text = $"{_invoice.ContactName} · {Dates.FormatDate(_invoice.Date)}";
            ClientName.Text = _invoice.ContactName ?? "—";
            ClientRfc.Text = string.IsNullOrWhiteSpace(_invoice.ContactRfc)
                ? "Sin RFC registrado"
                : _invoice.ContactRfc;
            IssueDate.Text = Dates.FormatDate(_invoice.Date);
            DueDateText.Text = Dates.FormatDate(_invoice.DueDate);
            TermsText.Text = string.IsNullOrWhiteSpace(_invoice.PaymentTerms) ? "—" : _invoice.PaymentTerms;
            CreatedByText.Text = string.IsNullOrWhiteSpace(_invoice.CreatedByName) ? "—" : _invoice.CreatedByName;
            NotesText.Text = _invoice.Notes;
            NotesPanel.Visibility = string.IsNullOrWhiteSpace(_invoice.Notes)
                ? Visibility.Collapsed
                : Visibility.Visible;

            SetStatus();
            CancelledPanel.Visibility = cancelled ? Visibility.Visible : Visibility.Collapsed;
            CancelledReason.Text = string.IsNullOrWhiteSpace(_invoice.CancelledReason)
                ? "Sin motivo registrado."
                : _invoice.CancelledReason +
                  (string.IsNullOrWhiteSpace(_invoice.CancelledAt)
                      ? ""
                      : $" · {Dates.FormatDateTime(_invoice.CancelledAt)}");

            ItemsGrid.ItemsSource = items;
            ItemsHint.Text = $"{items.Count} partida{(items.Count == 1 ? "" : "s")}";
            PaymentsGrid.ItemsSource = payments;
            PaymentsHint.Text = payments.Count > 0 ? $"{payments.Count} pago(s)" : "Sin pagos registrados";
            NoPaymentsText.Visibility = payments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PaymentsGrid.Visibility = payments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (PaymentsGrid.Columns.Count > 0)
                PaymentsGrid.Columns[^1].Visibility = canWrite && !cancelled
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            SubtotalValue.Text = Money.Format(_invoice.SubtotalCents);
            IvaValue.Text = Money.Format(_invoice.IvaCents);
            TotalValue.Text = Money.Format(_invoice.TotalCents);
            PaidValue.Text = Money.Format(_invoice.PaidCents);
            BalanceValue.Text = Money.Format(_invoice.BalanceCents);

            DraftPanel.Visibility = draft && canWrite ? Visibility.Visible : Visibility.Collapsed;
            PaymentPanel.Visibility = canPay && canWrite && accounts.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            OutstandingHint.Text = $"Saldo pendiente: {Money.Format(_invoice.BalanceCents)}";
            CancelPanel.Visibility = canWrite && !draft && !cancelled && _invoice.PaidCents == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            PaidNote.Visibility = _invoice.PaidCents > 0 && !cancelled
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            AppLog.Error("Detalle de factura", ex);
            MessageBox.Show("No se pudo cargar la factura.\n\n" + ex.Message,
                "Libro · Facturas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetStatus()
    {
        var label = _invoice.Status switch
        {
            "borrador" => "Borrador",
            "emitida" => _invoice.IsOverdue ? "Vencida" : "Emitida",
            "pagada" => "Pagada",
            "cancelada" => "Cancelada",
            _ => _invoice.Status,
        };
        var background = _invoice.Status switch
        {
            "borrador" => "WarnSoftBrush",
            "emitida" => _invoice.IsOverdue ? "DangerSoftBrush" : "InfoSoftBrush",
            "pagada" => "SuccessSoftBrush",
            "cancelada" => "DangerSoftBrush",
            _ => "ElevatedBrush",
        };
        var foreground = _invoice.Status switch
        {
            "borrador" => "WarnBrush",
            "emitida" => _invoice.IsOverdue ? "DangerBrush" : "InfoBrush",
            "pagada" => "SuccessBrush",
            "cancelada" => "DangerBrush",
            _ => "MutedBrush",
        };
        StatusText.Text = label;
        StatusPill.Background = (Brush)FindResource(background);
        StatusText.Foreground = (Brush)FindResource(foreground);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InvoiceDialog(_invoice) { Owner = this };
        if (dialog.ShowDialog() == true) Reload();
    }

    private void Emit_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "¿Emitir esta factura? Se asignará folio y se descontará inventario.",
            "Libro · Emitir factura", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            InvoiceService.Emit(_invoice.Id, AppState.UserId);
            Reload();
        }
        catch (Exception ex)
        {
            AppLog.Error("Emitir factura", ex);
            MessageBox.Show(ex.Message, "Libro · Facturas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteDraft_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show("¿Eliminar este borrador definitivamente?",
            "Libro · Eliminar borrador", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            InvoiceService.DeleteDraft(_invoice.Id, AppState.UserId);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AppLog.Error("Eliminar borrador", ex);
            MessageBox.Show(ex.Message, "Libro · Facturas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RegisterPayment_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PaymentDialog(_invoice) { Owner = this };
        if (dialog.ShowDialog() == true) Reload();
    }

    private void DeletePayment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Payment payment }) return;
        var confirm = MessageBox.Show(
            $"¿Eliminar el pago de {Money.Format(payment.AmountCents)}? El saldo se recalcula.",
            "Libro · Eliminar pago", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            InvoiceService.DeletePayment(payment.Id, AppState.UserId);
            Reload();
        }
        catch (Exception ex)
        {
            AppLog.Error("Eliminar pago", ex);
            MessageBox.Show(ex.Message, "Libro · Facturas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelInvoice_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new TextPromptDialog("Cancelar factura",
            "Se repondrá el inventario vendido. Esta acción no se puede deshacer.")
        {
            Owner = this,
        };
        if (prompt.ShowDialog() != true) return;

        try
        {
            InvoiceService.Cancel(_invoice.Id, prompt.Value, AppState.UserId);
            Reload();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cancelar factura", ex);
            MessageBox.Show(ex.Message, "Libro · Facturas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
