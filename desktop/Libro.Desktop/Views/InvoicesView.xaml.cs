using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop.Views;

public partial class InvoicesView : UserControl, IRefreshable
{
    private bool _initialized;

    public InvoicesView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            var canWrite = AppState.Can(Permissions.InvoiceWrite);
            NewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            EmptyNewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            _initialized = true;
        }
        LoadInvoices();
    }

    private void LoadInvoices()
    {
        try
        {
            var status = (StatusFilter.SelectedItem as ComboBoxItem)?.Tag as string;
            var page = InvoiceService.GetInvoices(new InvoiceFilters
            {
                Status = string.IsNullOrWhiteSpace(status) ? null : status,
                Query = SearchBox.Text.Trim(),
            }, limit: 200);
            var overview = InvoiceService.GetOverview();

            InvoicesGrid.ItemsSource = page.Rows;
            EmptyPanel.Visibility = page.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            InvoicesGrid.Visibility = page.Rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            ReceivableValue.Text = Money.Format(overview.ReceivableCents);
            OverdueValue.Text = Money.Format(overview.OverdueCents);
            OverdueValue.Foreground = overview.OverdueCents > 0
                ? (Brush)FindResource("DangerBrush")
                : (Brush)FindResource("InkBrush");
            OverdueHint.Text = $"{overview.OverdueCount} factura(s) vencida(s)";
            OverdueHint.Foreground = overview.OverdueCount > 0
                ? (Brush)FindResource("DangerBrush")
                : (Brush)FindResource("MutedBrush");
            MonthValue.Text = Money.Format(overview.MonthTotalCents);
            MonthHint.Text = $"{overview.MonthCount} factura(s)";
            DraftValue.Text = overview.DraftCount.ToString();

            CountText.Text = page.Total == page.Rows.Count
                ? $"{page.Total} factura{(page.Total == 1 ? "" : "s")}"
                : $"{page.Total} facturas · mostrando {page.Rows.Count}";
            SumText.Text = $"Saldo listado: {Money.Format(page.Rows.Sum(row => row.BalanceCents))}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Facturas", ex);
            MessageBox.Show("No se pudieron cargar las facturas.\n\n" + ex.Message,
                "Libro · Facturas", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => LoadInvoices();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        StatusFilter.SelectedIndex = 0;
        LoadInvoices();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.Can(Permissions.InvoiceWrite)) return;
        var dialog = new InvoiceDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadInvoices();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Invoice invoice }) OpenDetail(invoice);
    }

    private void InvoicesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (InvoicesGrid.SelectedItem is Invoice invoice) OpenDetail(invoice);
    }

    private void OpenDetail(Invoice invoice)
    {
        var dialog = new InvoiceDetailWindow(invoice) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        LoadInvoices();
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
