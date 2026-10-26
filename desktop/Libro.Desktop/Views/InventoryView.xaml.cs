using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;
using Microsoft.Win32;

namespace Libro.Desktop.Views;

public partial class InventoryView : UserControl, IRefreshable
{
    private bool _initialized;

    public InventoryView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            var canWrite = AppState.Can(Permissions.InventoryWrite) ||
                           AppState.Can(Permissions.ProductsWrite);
            NewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            EmptyNewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            WriteActionsColumn.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            _initialized = true;
        }
        LoadProducts();
    }

    private void LoadProducts()
    {
        try
        {
            var filters = new InventoryFilters
            {
                Query = SearchBox.Text.Trim(),
                OnlyLow = LowOnlyBox.IsChecked == true,
                IncludeInactive = InactiveBox.IsChecked == true,
            };
            var products = InventoryService.GetProducts(filters);
            var summary = InventoryService.GetSummary();

            ProductsGrid.ItemsSource = products;
            EmptyPanel.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ProductsGrid.Visibility = products.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            ProductCountValue.Text = summary.ProductCount.ToString();
            InventoryValue.Text = Money.Format(summary.ValueCents);
            LowStockValue.Text = summary.LowStockCount.ToString();
            LowStockValue.Foreground = summary.LowStockCount > 0
                ? (Brush)FindResource("WarnBrush")
                : (Brush)FindResource("InkBrush");
            OutOfStockValue.Text = summary.OutOfStockCount.ToString();
            OutOfStockValue.Foreground = summary.OutOfStockCount > 0
                ? (Brush)FindResource("DangerBrush")
                : (Brush)FindResource("InkBrush");

            CountText.Text = products.Count == 1 ? "1 producto" : $"{products.Count} productos";
            UnitCountText.Text = $"{summary.ProductCount} activos · {Money.FormatQty(summary.UnitCount)} unidades en stock";
        }
        catch (Exception ex)
        {
            AppLog.Error("Inventario", ex);
            MessageBox.Show("No se pudo cargar el inventario.\n\n" + ex.Message,
                "Libro · Inventario", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => LoadProducts();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        LowOnlyBox.IsChecked = false;
        InactiveBox.IsChecked = false;
        LoadProducts();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) LoadProducts();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.Can(Permissions.InventoryWrite) && !AppState.Can(Permissions.ProductsWrite)) return;
        var dialog = new ProductDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadProducts();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Product product }) return;
        if (!AppState.Can(Permissions.InventoryWrite) && !AppState.Can(Permissions.ProductsWrite)) return;
        var dialog = new ProductDialog(product) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadProducts();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Product product }) return;
        if (!AppState.Can(Permissions.InventoryWrite) && !AppState.Can(Permissions.ProductsWrite)) return;

        var activate = product.Active == 0;
        var message = activate
            ? "¿Reactivar este producto?"
            : "¿Desactivar este producto? No aparecerá en facturas nuevas.";
        if (MessageBox.Show(message, "Libro · Inventario", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            InventoryService.ToggleActive(product.Id, activate, AppState.UserId);
            LoadProducts();
        }
        catch (Exception ex)
        {
            AppLog.Error("Activar producto", ex);
            MessageBox.Show(ex.Message, "Libro · Inventario", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Product product }) OpenProduct(product);
    }

    private void ProductsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (ProductsGrid.SelectedItem is Product product) OpenProduct(product);
    }

    private void OpenProduct(Product product)
    {
        var dialog = new ProductDetailWindow(product.Id) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        LoadProducts();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var products = (ProductsGrid.ItemsSource as IEnumerable<Product>)?.ToList() ?? new List<Product>();
        if (products.Count == 0)
        {
            MessageBox.Show("No hay productos para exportar.", "Libro · Inventario",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Exportar inventario",
            Filter = "Archivo CSV (*.csv)|*.csv",
            FileName = $"Inventario-{DateTime.Now:yyyyMMdd}.csv",
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("Producto,SKU,Unidad,Stock,Minimo,Costo,Precio,Valor,Estado,Activo");
            foreach (var product in products)
            {
                csv.AppendLine(string.Join(",",
                    Csv(product.Name),
                    Csv(product.Sku),
                    Csv(product.Unit),
                    product.Stock.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    product.MinStock.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    Money.CentsToInput(product.CostCents),
                    Money.CentsToInput(product.PriceCents),
                    Money.CentsToInput(product.InventoryValueCents),
                    Csv(product.InventoryStatus),
                    product.Active == 1 ? "Sí" : "No"));
            }
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Inventario exportado correctamente.", "Libro · Inventario",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Exportar inventario", ex);
            MessageBox.Show("No se pudo exportar el inventario.\n\n" + ex.Message,
                "Libro · Inventario", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string Csv(string? value)
    {
        value ??= "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
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
