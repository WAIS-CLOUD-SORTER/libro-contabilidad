using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class ProductDetailWindow : Window
{
    private readonly long _productId;
    private Product _product = new();
    private bool _loaded;
    private bool _suppressTypeChange;

    public ProductDetailWindow(long productId)
    {
        InitializeComponent();
        _productId = productId;
        QtyBox.TextChanged += (_, _) => UpdateHint();
        LoadProduct();
    }

    private void LoadProduct()
    {
        try
        {
            var product = InventoryService.GetProduct(_productId);
            if (product is null)
            {
                Close();
                return;
            }
            _product = product;
            var movements = InventoryService.GetMovements(_productId);

            _loaded = false;
            Title = $"{product.Name} · Libro";
            TitleText.Text = product.Name;
            SubtitleText.Text = (string.IsNullOrWhiteSpace(product.Sku) ? "" : $"{product.Sku} · ") + product.Unit;
            StockValue.Text = product.TrackStock == 1 ? product.StockLabel : "—";
            MinStockHint.Text = product.TrackStock == 1
                ? $"Mínimo {FormatQty(product.MinStock)} {product.Unit}"
                : "Sin control de inventario";
            CostValue.Text = Money.Format(product.CostCents);
            InventoryValue.Text = product.TrackStock == 1
                ? Money.Format(product.InventoryValueCents)
                : "—";
            MarginValue.Text = Money.FormatPercent(product.MarginRate);
            PriceHint.Text = $"Precio {Money.Format(product.PriceCents)}";
            SkuText.Text = string.IsNullOrWhiteSpace(product.Sku) ? "—" : product.Sku;
            UnitText.Text = product.Unit;
            IvaText.Text = Money.FormatPercent(product.IvaRate);
            DescriptionText.Text = product.Description;
            DescriptionText.Visibility = string.IsNullOrWhiteSpace(product.Description)
                ? Visibility.Collapsed
                : Visibility.Visible;

            MovementsGrid.ItemsSource = movements;
            MovementsHint.Text = $"{movements.Count} registro(s)";
            NoMovementsText.Visibility = movements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MovementsGrid.Visibility = movements.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            var canWrite = AppState.Can(Permissions.InventoryWrite) || AppState.Can(Permissions.ProductsWrite);
            EditButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            MovementPanel.Visibility = canWrite && product.TrackStock == 1
                ? Visibility.Visible
                : Visibility.Collapsed;
            NoStockPanel.Visibility = product.TrackStock == 0 ? Visibility.Visible : Visibility.Collapsed;

            LoadForm(product);
            _loaded = true;
            UpdateTypeFields();
        }
        catch (Exception ex)
        {
            AppLog.Error("Detalle de producto", ex);
            MessageBox.Show("No se pudo cargar el producto.\n\n" + ex.Message,
                "Libro · Inventario", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadForm(Product product)
    {
        MovementDate.SelectedDate = DateTime.Today;
        CostBox.Text = Money.CentsToInput(product.CostCents);

        var accounts = new List<Account>
        {
            new() { Id = 0, Name = "No registrar salida de efectivo" },
        };
        accounts.AddRange(CompanyService.GetAccounts(AppState.Db));
        AccountBox.ItemsSource = accounts;
        AccountBox.SelectedValue = 0;

        IvaBox.ItemsSource = new List<InvoiceIvaOption>
        {
            new(0.16, "16%"),
            new(0.08, "8% frontera"),
            new(0, "0% / exento"),
        };
        IvaBox.SelectedValue = product.IvaRate;
        if (IvaBox.SelectedIndex < 0) IvaBox.SelectedIndex = 0;
    }

    private void UpdateTypeFields()
    {
        if (!_loaded) return;
        var type = CurrentType();
        var entrada = type == "entrada";
        PurchasePanel.Visibility = entrada ? Visibility.Visible : Visibility.Collapsed;
        QtyLabel.Text = type == "ajuste" ? "Stock real contado" : "Cantidad";
        SaveButton.Content = type switch
        {
            "entrada" => "Registrar entrada",
            "salida" => "Registrar salida",
            _ => "Ajustar stock",
        };

        if (_suppressTypeChange) return;
        _suppressTypeChange = true;
        if (type == "ajuste")
            QtyBox.Text = _product.Stock.ToString("0.###", CultureInfo.InvariantCulture);
        else
            QtyBox.Text = "";
        _suppressTypeChange = false;
        UpdateHint();
    }

    private void UpdateHint()
    {
        if (!_loaded) return;
        var type = CurrentType();
        var qty = Money.ParseQty(QtyBox.Text) ?? 0;
        var delta = type == "entrada" ? qty : type == "salida" ? -qty : qty - _product.Stock;
        var result = type == "ajuste" ? qty : _product.Stock + delta;
        ResultHint.Text = $"Stock resultante: {FormatQty(result)} {_product.Unit}" +
                          (type == "ajuste" && QtyBox.Text.Length > 0
                              ? $" (ajuste {delta:+0.###;-0.###;0})"
                              : "");
        ResultHint.Foreground = result < 0
            ? (Brush)FindResource("DangerBrush")
            : (Brush)FindResource("MutedBrush");
    }

    private string CurrentType() =>
        (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "entrada";

    private void Type_Changed(object sender, SelectionChangedEventArgs e) => UpdateTypeFields();

    private void SaveMovement_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var qty = Money.ParseQty(QtyBox.Text);
        if (qty is null)
        {
            ErrorText.Text = "Escribe una cantidad válida.";
            return;
        }
        if (MovementDate.SelectedDate is null)
        {
            ErrorText.Text = "Selecciona la fecha.";
            return;
        }

        var type = CurrentType();
        var input = new InventoryMovementInput
        {
            ProductId = _productId,
            Date = MovementDate.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Type = type,
            Qty = qty.Value,
            UnitCostCents = Money.ParseAmount(CostBox.Text) ?? 0,
            IvaRate = IvaBox.SelectedValue is double rate ? rate : _product.IvaRate,
            Reason = ReasonBox.Text.Trim(),
            Reference = ReferenceBox.Text.Trim(),
            AccountId = type == "entrada" && AccountBox.SelectedValue is long accountId && accountId > 0
                ? accountId
                : null,
        };

        try
        {
            InventoryService.AddMovement(input, AppState.UserId);
            ReasonBox.Text = "";
            ReferenceBox.Text = "";
            LoadProduct();
        }
        catch (Exception ex)
        {
            AppLog.Error("Movimiento de inventario", ex);
            ErrorText.Text = ex.Message;
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.Can(Permissions.ProductsWrite) && !AppState.Can(Permissions.InventoryWrite)) return;
        var dialog = new ProductDialog(_product) { Owner = this };
        if (dialog.ShowDialog() == true) LoadProduct();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static string FormatQty(double qty) =>
        qty.ToString("#,0.###", CultureInfo.GetCultureInfo("es-MX"));
}
