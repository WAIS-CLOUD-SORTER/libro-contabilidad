using System.Windows;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class PasswordResetDialog : Window
{
    private readonly User _user;

    public PasswordResetDialog(User user)
    {
        InitializeComponent();
        _user = user;
        SubtitleText.Text = $"{user.Name} · {user.Email}";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (string.IsNullOrEmpty(PasswordBox.Password))
        {
            ErrorText.Text = "Escribe la nueva contraseña.";
            return;
        }
        if (PasswordBox.Password != ConfirmBox.Password)
        {
            ErrorText.Text = "Las contraseñas no coinciden.";
            return;
        }
        try
        {
            UserService.ResetPassword(_user.Id, PasswordBox.Password);
            MessageBox.Show("Contraseña actualizada. El usuario puede iniciar sesión con ella.",
                "Libro · Usuarios", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Restablecer contraseña", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
