using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class ContactDialog : Window
{
    private readonly Contact _contact;

    public ContactDialog(Contact? existing = null)
    {
        InitializeComponent();
        _contact = existing ?? new Contact { Type = "cliente" };

        TitleText.Text = existing is null ? "Nuevo contacto" : "Editar contacto";
        SubtitleText.Text = existing is null
            ? "Clientes para facturar; proveedores para tus gastos."
            : existing.Name;
        SaveButton.Content = existing is null ? "Crear contacto" : "Guardar cambios";

        SelectType(_contact.Type);
        NameBox.Text = _contact.Name;
        RfcBox.Text = _contact.Rfc;
        EmailBox.Text = _contact.Email;
        PhoneBox.Text = _contact.Phone;
        AddressBox.Text = _contact.Address;
        NotesBox.Text = _contact.Notes;
        NameBox.Focus();
    }

    private void SelectType(string type)
    {
        foreach (var item in TypeBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag as string == type)
            {
                TypeBox.SelectedItem = item;
                return;
            }
        }
        TypeBox.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        _contact.Type = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "cliente";
        _contact.Name = NameBox.Text.Trim();
        _contact.Rfc = RfcBox.Text.Trim().ToUpperInvariant();
        _contact.Email = EmailBox.Text.Trim();
        _contact.Phone = PhoneBox.Text.Trim();
        _contact.Address = AddressBox.Text.Trim();
        _contact.Notes = NotesBox.Text.Trim();

        try
        {
            ContactService.Save(_contact, AppState.UserId);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar contacto", ex);
            ErrorText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
