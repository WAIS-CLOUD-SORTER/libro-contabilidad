using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class TransactionDialog : Window
{
    private readonly TransactionRow _transaction;
    private bool _ready;

    public TransactionDialog(TransactionRow? existing = null)
    {
        InitializeComponent();

        _transaction = existing ?? new TransactionRow
        {
            Date = Dates.TodayIso(),
            Type = "gasto",
            IvaRate = AppState.Company.IvaRate,
            Source = "manual",
        };

        TitleText.Text = existing is null ? "Nuevo movimiento" : "Editar movimiento";
        SubtitleText.Text = existing is null
            ? "Registra un gasto o un ingreso que no pasa por facturaciÃ³n."
            : $"{existing.Description} Â· {(existing.Type == "ingreso" ? "Ingreso" : "Gasto")}";
        SaveButton.Content = existing is null ? "Guardar movimiento" : "Guardar cambios";

        LoadLookups();
        Populate();
        _ready = true;
    }

    private void LoadLookups()
    {
        var methods = new List<string>
        {
            "Transferencia", "Efectivo", "Tarjeta", "Cheque", "DomiciliaciÃ³n", "Otro",
        };
        if (!string.IsNullOrWhiteSpace(_transaction.Method) && !methods.Contains(_transaction.Method))
            methods.Add(_transaction.Method);
        MethodBox.ItemsSource = methods;

        var ivaOptions = new List<IvaOption>
        {
            new(0.16, "16%"),
            new(0.08, "8% frontera"),
            new(0.0, "0% / exento"),
        };
        if (ivaOptions.All(option => Math.Abs(option.Rate - _transaction.IvaRate) > 0.0001))
            ivaOptions.Insert(0, new IvaOption(_transaction.IvaRate, Money.FormatPercent(_transaction.IvaRate)));
        IvaBox.ItemsSource = ivaOptions;

        var contacts = new List<Contact> { new() { Id = 0, Name = "Sin contacto" } };
        contacts.AddRange(CompanyService.GetContacts(AppState.Db, _transaction.Id != 0));
        ContactBox.ItemsSource = contacts;

        AccountBox.ItemsSource = CompanyService.GetAccounts(AppState.Db, _transaction.Id != 0);
    }

    private void Populate()
    {
        if (_transaction.Type == "ingreso") IncomeRadio.IsChecked = true;
        else ExpenseRadio.IsChecked = true;

        LoadCategories();

        if (DateTime.TryParseExact(_transaction.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            MovementDate.SelectedDate = date;
        else
            MovementDate.SelectedDate = DateTime.Today;

        if (_transaction.CategoryId is > 0)
            CategoryBox.SelectedValue = _transaction.CategoryId;

        ContactBox.SelectedValue = _transaction.ContactId is > 0 ? _transaction.ContactId : 0;
        if (_transaction.AccountId is > 0)
            AccountBox.SelectedValue = _transaction.AccountId;
        if (AccountBox.SelectedIndex < 0 && AccountBox.Items.Count > 0)
            AccountBox.SelectedIndex = 0;

        DescriptionBox.Text = _transaction.Description;
        ReferenceBox.Text = _transaction.Reference;

        if (MethodBox.ItemsSource is List<string> methods)
        {
            if (!string.IsNullOrWhiteSpace(_transaction.Method) && !methods.Contains(_transaction.Method))
                methods.Add(_transaction.Method);
            MethodBox.SelectedItem = string.IsNullOrWhiteSpace(_transaction.Method)
                ? "Transferencia"
                : _transaction.Method;
        }

        var selectedIva = (IvaBox.ItemsSource as IEnumerable<IvaOption>)?.FirstOrDefault(
            option => Math.Abs(option.Rate - _transaction.IvaRate) < 0.0001);
        if (selectedIva is not null) IvaBox.SelectedItem = selectedIva;
        else if (IvaBox.Items.Count > 0) IvaBox.SelectedIndex = 0;

        SubtotalBox.Text = _transaction.SubtotalCents > 0
            ? Money.CentsToInput(_transaction.SubtotalCents)
            : "";

        UpdateTotals();
    }

    private void LoadCategories()
    {
        var type = IncomeRadio.IsChecked == true ? "ingreso" : "gasto";
        var selected = CategoryBox.SelectedValue is long id ? id : 0;
        var categories = CompanyService.GetCategories(AppState.Db, includeArchived: _transaction.Id != 0)
            .Where(category => type == "ingreso" ? category.Kind == "ingreso" : category.Kind != "ingreso")
            .ToList();

        CategoryBox.ItemsSource = categories;
        if (selected > 0 && categories.Any(category => category.Id == selected))
            CategoryBox.SelectedValue = selected;
        else if (_transaction.CategoryId is > 0 && categories.Any(category => category.Id == _transaction.CategoryId))
            CategoryBox.SelectedValue = _transaction.CategoryId;
        else
            CategoryBox.SelectedIndex = -1;
    }

    private void Type_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        LoadCategories();
        UpdateTotals();
    }

    private void SubtotalBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) UpdateTotals();
    }

    private void IvaBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) UpdateTotals();
    }

    private void UpdateTotals()
    {
        var subtotal = Money.ParseAmount(SubtotalBox.Text) ?? 0;
        var rate = IvaBox.SelectedValue is double selectedRate ? selectedRate : 0;
        var iva = (long)Math.Round(subtotal * rate, MidpointRounding.AwayFromZero);
        IvaValue.Text = Money.Format(iva);
        TotalValue.Text = Money.Format(subtotal + iva);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (MovementDate.SelectedDate is null)
        {
            ErrorText.Text = "Selecciona una fecha.";
            return;
        }

        var subtotal = Money.ParseAmount(SubtotalBox.Text);
        if (subtotal is null or <= 0)
        {
            ErrorText.Text = "Escribe un monto mayor a cero.";
            return;
        }
        if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
        {
            ErrorText.Text = "Escribe una descripciÃ³n.";
            return;
        }
        if (CategoryBox.SelectedValue is not long categoryId)
        {
            ErrorText.Text = "Selecciona una categorÃ­a.";
            return;
        }
        if (AccountBox.SelectedValue is not long accountId)
        {
            ErrorText.Text = "Selecciona una cuenta de efectivo.";
            return;
        }

        var contactId = ContactBox.SelectedValue is long id && id > 0 ? id : (long?)null;
        var rate = IvaBox.SelectedValue is double selectedRate ? selectedRate : 0;

        _transaction.Date = MovementDate.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _transaction.Type = IncomeRadio.IsChecked == true ? "ingreso" : "gasto";
        _transaction.CategoryId = categoryId;
        _transaction.ContactId = contactId;
        _transaction.AccountId = accountId;
        _transaction.Description = DescriptionBox.Text.Trim();
        _transaction.SubtotalCents = subtotal.Value;
        _transaction.IvaRate = rate;
        _transaction.Method = MethodBox.SelectedItem as string ?? "";
        _transaction.Reference = ReferenceBox.Text.Trim();

        try
        {
            TransactionService.Save(_transaction, AppState.UserId);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar movimiento", ex);
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public sealed record IvaOption(double Rate, string Label);
}

