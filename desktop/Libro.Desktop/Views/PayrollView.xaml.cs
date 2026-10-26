using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop.Views;

public partial class PayrollView : UserControl, IRefreshable
{
    private bool _initialized;

    public PayrollView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            var canWrite = AppState.Can(Permissions.PayrollWrite);
            NewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            EmptyNewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            _initialized = true;
        }
        LoadPeriods();
    }

    private void LoadPeriods()
    {
        try
        {
            var periods = PayrollService.GetPeriods();
            var summary = PayrollService.GetSummary();

            PeriodsGrid.ItemsSource = periods;
            EmptyPanel.Visibility = periods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            PeriodsGrid.Visibility = periods.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            EmployeeValue.Text = summary.EmployeeCount.ToString();
            DraftValue.Text = summary.DraftCount.ToString();
            DraftNetText.Text = $"{Money.Format(summary.DraftNetCents)} por pagar";
            PaidValue.Text = Money.Format(summary.PaidThisMonthCents);
            PaidCountText.Text = summary.PaidCount == 1 ? "1 nómina" : $"{summary.PaidCount} nóminas";
            CountText.Text = periods.Count == 1 ? "1 periodo" : $"{periods.Count} periodos";
        }
        catch (Exception ex)
        {
            AppLog.Error("Nómina", ex);
            MessageBox.Show("No se pudo cargar la nómina.\n\n" + ex.Message,
                "Libro · Nómina", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.Can(Permissions.PayrollWrite)) return;
        var dialog = new PayrollPeriodDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        LoadPeriods();
        OpenPeriod(dialog.CreatedPeriodId);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PayrollPeriod period }) OpenPeriod(period.Id);
    }

    private void PeriodsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (PeriodsGrid.SelectedItem is PayrollPeriod period) OpenPeriod(period.Id);
    }

    private void OpenPeriod(long id)
    {
        var window = new PayrollDetailWindow(id) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        LoadPeriods();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PayrollPeriod period }) return;
        if (!AppState.Can(Permissions.PayrollWrite)) return;
        if (MessageBox.Show(
                $"¿Eliminar la nómina \"{period.Label}\"? Se borrarán sus conceptos. Esta acción no se puede deshacer.",
                "Libro · Nómina", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            PayrollService.DeletePeriod(period.Id);
            LoadPeriods();
        }
        catch (Exception ex)
        {
            AppLog.Error("Eliminar nómina", ex);
            MessageBox.Show(ex.Message, "Libro · Nómina", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
