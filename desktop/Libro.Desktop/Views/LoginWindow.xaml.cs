using System.Windows;
using Libro.Desktop.Services;

namespace Libro.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        CompanyNameText.Text = string.IsNullOrWhiteSpace(AppState.Company.Name)
            ? "Mi negocio"
            : AppState.Company.Name;
        Loaded += (_, _) => EmailBox.Focus();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(EmailBox.Text))
        {
            ShowError("Escribe tu correo.");
            EmailBox.Focus();
            return;
        }
        if (string.IsNullOrEmpty(PasswordBox.Password))
        {
            ShowError("Escribe tu contraseña.");
            PasswordBox.Focus();
            return;
        }

        try
        {
            LoginButton.IsEnabled = false;
            var result = AuthService.Login(AppState.Db, EmailBox.Text, PasswordBox.Password);
            if (result.User is null)
            {
                ShowError(result.Error ?? "Correo o contraseña incorrectos.");
                PasswordBox.Clear();
                PasswordBox.Focus();
                return;
            }

            AppState.RefreshCompany();
            AppState.SetCurrentUser(result.User);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError("No se pudo iniciar sesión: " + ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
