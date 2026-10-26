using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class AccountDialog : Window
{
    private readonly Account? _account;

    public AccountDialog(Account? account = null)
    {
        InitializeComponent();
        _account = account;
        if (account is null)
        {
            TypeBox.SelectedIndex = 0;
            return;
        }

        TitleText.Text = "Editar cuenta";
        NameBox.Text = account.Name;
        TypeBox.SelectedIndex = account.Type switch
        {
            "banco" => 1,
            "tarjeta" => 2,
            "otro" => 3,
            _ => 0,
        };
        OpeningBox.Text = Money.CentsToInput(account.OpeningBalanceCents);
        ActiveBox.IsChecked = account.Active == 1;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Escribe el nombre de la cuenta.";
            return;
        }
        var opening = Money.ParseAmount(OpeningBox.Text) ?? 0;
        var type = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "efectivo";
        var account = new Account
        {
            Id = _account?.Id ?? 0,
            Name = NameBox.Text.Trim(),
            Type = type,
            OpeningBalanceCents = opening,
            Active = ActiveBox.IsChecked == true ? 1 : 0,
        };

        try
        {
            CompanyService.SaveAccount(AppState.Db, account, AppState.UserId);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar cuenta", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
