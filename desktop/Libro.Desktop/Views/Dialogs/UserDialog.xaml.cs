using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class UserDialog : Window
{
    private readonly User? _user;

    public UserDialog(User? user = null)
    {
        InitializeComponent();
        _user = user;
        if (user is null)
        {
            RoleBox.SelectedIndex = 2;
            UpdateRoleHint();
            return;
        }

        TitleText.Text = "Editar usuario";
        SubtitleText.Text = "Actualiza los datos y el acceso de esta cuenta.";
        NameBox.Text = user.Name;
        EmailBox.Text = user.Email;
        ActiveBox.IsChecked = user.Active == 1;
        RoleBox.SelectedIndex = user.Role switch
        {
            "admin" => 0,
            "contador" => 1,
            "capturista" => 2,
            _ => 3,
        };
        PasswordPanel.Visibility = Visibility.Collapsed;
        UpdateRoleHint();
    }

    private void RoleBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateRoleHint();

    private void Save_Click(object sender, RoutedEventArgs e)    {
        ErrorText.Text = "";
        if (RoleBox.SelectedItem is not ComboBoxItem { Tag: string role }) return;
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(EmailBox.Text))
        {
            ErrorText.Text = "Escribe el nombre y el correo del usuario.";
            return;
        }

        string? password = null;
        if (_user is null)
        {
            password = PasswordBox.Password;
            if (string.IsNullOrEmpty(password))
            {
                ErrorText.Text = "Escribe una contraseña inicial.";
                return;
            }
            if (password != ConfirmBox.Password)
            {
                ErrorText.Text = "Las contraseñas no coinciden.";
                return;
            }
        }

        try
        {
            UserService.SaveUser(_user?.Id ?? 0, NameBox.Text, EmailBox.Text, role,
                ActiveBox.IsChecked == true, password);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar usuario", ex);
            ErrorText.Text = ex.Message;
        }
    }

    private void UpdateRoleHint()
    {
        if (RoleBox.SelectedItem is ComboBoxItem { Tag: string role })
            RoleHintText.Text = Permissions.RoleDescription(role);
    }
}
