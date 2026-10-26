using System.Windows;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class ProductDialog : Window
{
    private readonly Product? _product;

    public ProductDialog(Product? existing = null)
    {
        InitializeComponent();
        _product = existing;

        IvaBox.ItemsSource = new List<InvoiceIvaOption>
        {
            new(0.16, "16%"),
            new(0.08, "8% frontera"),
            new(0, "0% / exento"),
        };

        if (existing is null)
        {
            TitleText.Text = "Nuevo producto";
            SubtitleText.Text = "Registra el producto y su saldo inicial de inventario.";
            UnitBox.Text = "pieza";
            IvaBox.SelectedValue = 0.16;
            TrackStockBox.IsChecked = true;
        }
        else
        {
            TitleText.Text = "Editar producto";
            SubtitleText.Text = "El stock se modifica con movimientos de inventario, no desde esta ficha.";
            NameBox.Text = existing.Name;
            SkuBox.Text = existing.Sku;
            UnitBox.Text = existing.Unit;
            PriceBox.Text = Money.CentsToInput(existing.PriceCents);
            CostBox.Text = Money.CentsToInput(existing.CostCents);
            MinStockBox.Text = existing.MinStock.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            DescriptionBox.Text = existing.Description;
            TrackStockBox.IsChecked = existing.TrackStock == 1;
            IvaBox.SelectedValue = existing.IvaRate;
            if (IvaBox.SelectedIndex < 0) IvaBox.SelectedIndex = 0;
            InitialStockPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void TrackStock_Changed(object sender, RoutedEventArgs e)
    {
        if (InitialStockPanel is null) return;
        InitialStockPanel.Visibility = TrackStockBox.IsChecked == true && _product is null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Escribe el nombre del producto.";
            return;
        }
        if (string.IsNullOrWhiteSpace(UnitBox.Text))
        {
            ErrorText.Text = "Escribe la unidad.";
            return;
        }

        var trackStock = TrackStockBox.IsChecked == true;
        var input = new ProductInput
        {
            Id = _product?.Id ?? 0,
            Name = NameBox.Text.Trim(),
            Sku = SkuBox.Text.Trim(),
            Description = DescriptionBox.Text.Trim(),
            Unit = UnitBox.Text.Trim(),
            PriceCents = Money.ParseAmount(PriceBox.Text) ?? 0,
            CostCents = Money.ParseAmount(CostBox.Text) ?? 0,
            IvaRate = IvaBox.SelectedValue is double rate ? rate : 0.16,
            MinStock = Money.ParseQty(MinStockBox.Text) ?? 0,
            TrackStock = trackStock,
            InitialStock = trackStock ? Money.ParseQty(InitialStockBox.Text) ?? 0 : 0,
        };

        try
        {
            InventoryService.SaveProduct(input, AppState.UserId);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar producto", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
