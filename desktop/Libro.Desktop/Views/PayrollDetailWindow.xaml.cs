using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop.Views;

public partial class PayrollDetailWindow : Window
{
    private readonly long _periodId;
    private PayrollPeriod? _period;

    public PayrollDetailWindow(long periodId)
    {
        InitializeComponent();
        _periodId = periodId;
        LoadPeriod();
    }

    private void LoadPeriod()
    {
        try
        {
            _period = PayrollService.GetPeriod(_periodId);
            if (_period is null)
            {
                MessageBox.Show("La nómina ya no existe.", "Libro · Nómina",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            var items = PayrollService.GetItems(_periodId);
            ItemsGrid.ItemsSource = items;
            EmptyPanel.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ItemsGrid.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            Title = $"{_period.Label} · Libro";
            TitleText.Text = _period.Label;
            RangeText.Text = _period.RangeLabel;
            StatusText.Text = _period.StatusLabel;
            var paid = _period.Status == "pagada";
            StatusPill.Background = (System.Windows.Media.Brush)FindResource(
                paid ? "SuccessSoftBrush" : "WarnSoftBrush");
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource(
                paid ? "SuccessBrush" : "WarnBrush");

            var canWrite = AppState.Can(Permissions.PayrollWrite);
            RecalculateButton.Visibility = canWrite && !paid ? Visibility.Visible : Visibility.Collapsed;
            PayButton.Visibility = canWrite && !paid ? Visibility.Visible : Visibility.Collapsed;
            RevertButton.Visibility = canWrite && paid ? Visibility.Visible : Visibility.Collapsed;
            WriteActionsColumn.Visibility = canWrite && !paid ? Visibility.Visible : Visibility.Collapsed;

            var perceptions = items.Sum(item => item.PerceptionsCents);
            var deductions = items.Sum(item => item.DeductionsCents);
            var net = items.Sum(item => item.NetCents);
            EmployeeValue.Text = items.Count.ToString();
            PerceptionsValue.Text = Money.Format(perceptions);
            DeductionsValue.Text = Money.Format(deductions);
            NetValue.Text = Money.Format(net);
            FooterNetText.Text = $"Neto del periodo: {Money.Format(net)}";

            if (paid)
            {
                PaidNote.Visibility = Visibility.Visible;
                PaidNote.Text = string.IsNullOrWhiteSpace(_period.PaidAt)
                    ? "Nómina pagada. Revierte el pago para editarla."
                    : $"Pagada el {Dates.FormatDateTime(_period.PaidAt)}. " +
                      "Revierte el pago para volver a editarla.";
            }
            else
            {
                PaidNote.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Detalle de nómina", ex);
            MessageBox.Show("No se pudo cargar la nómina.\n\n" + ex.Message,
                "Libro · Nómina", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_period is null || sender is not Button { Tag: PayrollItem item }) return;
        if (!AppState.Can(Permissions.PayrollWrite)) return;
        var dialog = new PayrollItemDialog(_period, item) { Owner = this };
        if (dialog.ShowDialog() == true) LoadPeriod();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_period is null || sender is not Button { Tag: PayrollItem item }) return;
        if (!AppState.Can(Permissions.PayrollWrite)) return;
        if (MessageBox.Show(
                $"¿Quitar el concepto de {item.EmployeeName} de esta nómina?",
                "Libro · Nómina", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            PayrollService.DeleteItem(item.Id, _periodId);
            LoadPeriod();
        }
        catch (Exception ex)
        {
            AppLog.Error("Quitar concepto de nómina", ex);
            MessageBox.Show(ex.Message, "Libro · Nómina", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Recalculate_Click(object sender, RoutedEventArgs e)
    {
        if (_period is null || !AppState.Can(Permissions.PayrollWrite)) return;
        if (MessageBox.Show(
                "Se recalcularán sueldo base, ISR e IMSS de todos los empleados activos. " +
                "Se conservan las percepciones extra, Infonavit, otras deducciones y notas.",
                "Libro · Nómina", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            PayrollService.Recalculate(_periodId);
            LoadPeriod();
        }
        catch (Exception ex)
        {
            AppLog.Error("Recalcular nómina", ex);
            MessageBox.Show(ex.Message, "Libro · Nómina", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Pay_Click(object sender, RoutedEventArgs e)
    {
        if (_period is null || !AppState.Can(Permissions.PayrollWrite)) return;
        var dialog = new PayrollPaymentDialog(_period) { Owner = this };
        if (dialog.ShowDialog() == true) LoadPeriod();
    }

    private void Revert_Click(object sender, RoutedEventArgs e)
    {
        if (_period is null || !AppState.Can(Permissions.PayrollWrite)) return;
        if (MessageBox.Show(
                "¿Revertir el pago? Se eliminará el gasto de nómina vinculado y el periodo volverá a borrador.",
                "Libro · Nómina", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            PayrollService.RevertPayment(_periodId);
            LoadPeriod();
        }
        catch (Exception ex)
        {
            AppLog.Error("Revertir pago de nómina", ex);
            MessageBox.Show(ex.Message, "Libro · Nómina", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
