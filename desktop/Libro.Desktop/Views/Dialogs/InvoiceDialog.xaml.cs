using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class InvoiceDialog : Window
{
    private readonly Invoice? _invoice;
    private readonly ObservableCollection<InvoiceLineRow> _lines = new();

    public InvoiceDialog(Invoice? existing = null)
    {
        InitializeComponent();
        _invoice = existing;

        Products.Add(new Product
        {
            Id = 0,
            Name = "Sin producto / servicio libre",
            TrackStock = 0,
        });
        foreach (var product in AppState.Db.All<Product>(
                     "SELECT * FROM products WHERE active = 1 OR $includeInactive = 1 ORDER BY name;",
                     new { includeInactive = existing is null ? 0 : 1 }))
            Products.Add(product);

        IvaOptions.Add(new InvoiceIvaOption(0.16, "16%"));
        IvaOptions.Add(new InvoiceIvaOption(0.08, "8% frontera"));
        IvaOptions.Add(new InvoiceIvaOption(0, "0% / exento"));
        EnsureIvaOption(AppState.Company.IvaRate);

        var contacts = ContactService.GetContacts(new ContactFilters
            {
                IncludeArchived = existing is not null,
            })
            .Where(contact => contact.Type is "cliente" or "ambos" ||
                              contact.Id == existing?.ContactId)
            .ToList();
        ClientBox.ItemsSource = contacts;
        NoClientsText.Visibility = contacts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmitButton.IsEnabled = contacts.Count > 0;
        SaveDraftButton.IsEnabled = contacts.Count > 0;

        LinesGrid.ItemsSource = _lines;

        if (existing is null)
        {
            TitleText.Text = "Nueva factura";
            SubtitleText.Text = "Documento interno de control. No es CFDI ni comprobante fiscal.";
            InvoiceDate.SelectedDate = DateTime.Today;
            DueDate.SelectedDate = DateTime.Today.AddDays(30);
            TermsBox.Text = "Pago a 30 días";
            AddBlankLine();
        }
        else
        {
            TitleText.Text = "Editar borrador";
            SubtitleText.Text = $"{existing.DisplayFolio} · {existing.ContactName}";
            EmitButton.Content = "Guardar y emitir";
            SaveDraftButton.Content = "Guardar cambios";
            ClientBox.SelectedValue = existing.ContactId;
            InvoiceDate.SelectedDate = ParseDate(existing.Date);
            DueDate.SelectedDate = ParseDate(existing.DueDate);
            TermsBox.Text = existing.PaymentTerms;
            NotesBox.Text = existing.Notes;

            foreach (var item in InvoiceService.GetItems(existing.Id))
            {
                var line = new InvoiceLineRow(item.IvaRate)
                {
                    ProductId = item.ProductId is > 0 ? item.ProductId : 0,
                    Description = item.Description,
                    QtyText = item.Qty.ToString("0.###", CultureInfo.InvariantCulture),
                    PriceText = Money.CentsToInput(item.UnitPriceCents),
                    IvaRate = item.IvaRate,
                };
                AddLine(line);
            }
            if (_lines.Count == 0) AddBlankLine();
        }

        UpdateSummary();
    }

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<InvoiceIvaOption> IvaOptions { get; } = new();

    private void AddBlankLine()
    {
        var line = new InvoiceLineRow(AppState.Company.IvaRate) { ProductId = 0 };
        AddLine(line);
    }

    private void AddLine(InvoiceLineRow line)
    {
        line.Changed += (_, _) => UpdateSummary();
        EnsureIvaOption(line.IvaRate);
        _lines.Add(line);
    }

    private void EnsureIvaOption(double rate)
    {
        if (IvaOptions.Any(option => Math.Abs(option.Rate - rate) < 0.00001)) return;
        IvaOptions.Add(new InvoiceIvaOption(rate, Money.FormatPercent(rate)));
    }

    private void UpdateSummary()
    {
        var subtotal = _lines.Sum(line => line.SubtotalCents);
        var iva = _lines.Sum(line => line.IvaCents);
        SubtotalValue.Text = Money.Format(subtotal);
        IvaValue.Text = Money.Format(iva);
        TotalValue.Text = Money.Format(subtotal + iva);
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        AddBlankLine();
        LinesGrid.ScrollIntoView(_lines[^1]);
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InvoiceLineRow line }) return;
        _lines.Remove(line);
        if (_lines.Count == 0) AddBlankLine();
        UpdateSummary();
    }

    private void Product_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: InvoiceLineRow line, SelectedItem: Product product }) return;
        line.ApplyProduct(product);
        EnsureIvaOption(line.IvaRate);
        UpdateSummary();
    }

    private void SaveDraft_Click(object sender, RoutedEventArgs e) => Save(emit: false);

    private void Emit_Click(object sender, RoutedEventArgs e) => Save(emit: true);

    private void Save(bool emit)
    {
        ErrorText.Text = "";
        if (ClientBox.SelectedValue is not long contactId)
        {
            ErrorText.Text = "Selecciona un cliente.";
            return;
        }
        if (InvoiceDate.SelectedDate is null || DueDate.SelectedDate is null)
        {
            ErrorText.Text = "Selecciona la fecha de emisión y el vencimiento.";
            return;
        }

        var lines = _lines
            .Select(line => line.ToInput())
            .Where(line => !string.IsNullOrWhiteSpace(line.Description) ||
                           line.Qty > 0 || line.UnitPriceCents > 0)
            .ToList();
        if (lines.Count == 0)
        {
            ErrorText.Text = "Agrega al menos un concepto.";
            return;
        }

        var input = new InvoiceInput
        {
            Id = _invoice?.Id ?? 0,
            ContactId = contactId,
            Date = InvoiceDate.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DueDate = DueDate.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Notes = NotesBox.Text.Trim(),
            PaymentTerms = TermsBox.Text.Trim(),
            Lines = lines,
        };

        try
        {
            InvoiceService.Save(input, emit, AppState.UserId);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error(emit ? "Emitir factura" : "Guardar factura", ex);
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static DateTime? ParseDate(string? iso) =>
        DateTime.TryParseExact(iso ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
}
