using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public sealed class InvoiceLineRow : INotifyPropertyChanged
{
    private long? _productId;
    private string _description = "";
    private string _qtyText = "1";
    private string _priceText = "";
    private double _ivaRate = 0.16;

    public InvoiceLineRow(double defaultIva)
    {
        _ivaRate = defaultIva;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    public long? ProductId
    {
        get => _productId;
        set
        {
            if (_productId == value) return;
            _productId = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value) return;
            _description = value;
            OnPropertyChanged();
        }
    }

    public string QtyText
    {
        get => _qtyText;
        set
        {
            if (_qtyText == value) return;
            _qtyText = value;
            OnPropertyChanged();
            NotifyTotals();
        }
    }

    public string PriceText
    {
        get => _priceText;
        set
        {
            if (_priceText == value) return;
            _priceText = value;
            OnPropertyChanged();
            NotifyTotals();
        }
    }

    public double IvaRate
    {
        get => _ivaRate;
        set
        {
            if (Math.Abs(_ivaRate - value) < 0.00001) return;
            _ivaRate = value;
            OnPropertyChanged();
            NotifyTotals();
        }
    }

    public long SubtotalCents => Money.LineTotals(Qty, PriceCents, IvaRate).Subtotal;
    public long IvaCents => Money.LineTotals(Qty, PriceCents, IvaRate).Iva;
    public long TotalCents => Money.LineTotals(Qty, PriceCents, IvaRate).Total;
    public string SubtotalLabel => Money.Format(SubtotalCents);
    public string IvaLabel => Money.Format(IvaCents);
    public string TotalLabel => Money.Format(TotalCents);

    private double Qty => Money.ParseQty(QtyText) ?? 0;
    private long PriceCents => Money.ParseAmount(PriceText) ?? 0;

    public void ApplyProduct(Product product)
    {
        if (product.Id <= 0)
        {
            ProductId = 0;
            return;
        }

        ProductId = product.Id;
        Description = product.Name;
        PriceText = Money.CentsToInput(product.PriceCents);
        IvaRate = product.IvaRate;
        NotifyTotals();
    }

    public InvoiceLineInput ToInput() => new()
    {
        ProductId = ProductId is > 0 ? ProductId : null,
        Description = Description.Trim(),
        Qty = Money.ParseQty(QtyText) ?? 0,
        UnitPriceCents = Money.ParseAmount(PriceText) ?? 0,
        IvaRate = IvaRate,
    };

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(SubtotalCents));
        OnPropertyChanged(nameof(IvaCents));
        OnPropertyChanged(nameof(TotalCents));
        OnPropertyChanged(nameof(SubtotalLabel));
        OnPropertyChanged(nameof(IvaLabel));
        OnPropertyChanged(nameof(TotalLabel));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record InvoiceIvaOption(double Rate, string Label);
