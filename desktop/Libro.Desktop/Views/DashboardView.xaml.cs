using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views;

public partial class DashboardView : UserControl, IRefreshable
{
    public DashboardView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        try
        {
            var data = DashboardService.GetDashboard(AppState.Db);
            DataContext = data;

            var year = int.Parse(data.RangeStart[..4]);
            var month = int.Parse(data.RangeStart[5..7]);
            SubtitleText.Text = $"Resumen de {Dates.MonthLabel(month).ToLowerInvariant()} de {year} · {AppState.Company.Name}";

            ProfitValue.Foreground = data.ProfitCents >= 0
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("DangerBrush");

            if (data.OverdueCount > 0)
            {
                OverdueText.Text = $"{data.OverdueCount} factura{(data.OverdueCount == 1 ? "" : "s")} vencida{(data.OverdueCount == 1 ? "" : "s")}";
                OverdueBadge.Background = (Brush)FindResource("DangerSoftBrush");
                OverdueBadgeText.Foreground = (Brush)FindResource("DangerBrush");
                OverdueBadgeText.Text = "Requiere atención";
            }
            else
            {
                OverdueText.Text = "Sin facturas vencidas";
                OverdueBadge.Background = (Brush)FindResource("SuccessSoftBrush");
                OverdueBadgeText.Foreground = (Brush)FindResource("SuccessBrush");
                OverdueBadgeText.Text = "Al día";
            }

            StockEmptyText.Visibility = data.LowStock.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            AppLog.Error("Dashboard", ex);
            SubtitleText.Text = "No se pudo cargar el resumen. Revisa el registro de la aplicación.";
        }
    }

    private void NewTransaction_Click(object sender, RoutedEventArgs e) => Navigate("movimientos");

    private void NewInvoice_Click(object sender, RoutedEventArgs e) => Navigate("facturas");

    private void ViewTransactions_Click(object sender, RoutedEventArgs e) => Navigate("movimientos");

    private void ViewInvoices_Click(object sender, RoutedEventArgs e) => Navigate("facturas");

    private void Navigate(string key)
    {
        if (Window.GetWindow(this) is MainWindow main) main.Navigate(key);
    }
}
