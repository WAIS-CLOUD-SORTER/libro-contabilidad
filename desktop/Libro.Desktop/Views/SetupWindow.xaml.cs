using System.Windows;
using Libro.Desktop.Services;

namespace Libro.Desktop.Views;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => CompanyBox.Focus();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(CompanyBox.Text))
        {
            ShowError("Escribe el nombre de tu negocio.");
            CompanyBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ShowError("Escribe tu nombre.");
            NameBox.Focus();
            return;
        }
        if (Libro.Desktop.Util.Validation.ValidateEmail(EmailBox.Text) is { } emailError)
        {
            ShowError(emailError);
            EmailBox.Focus();
            return;
        }
        if (Libro.Desktop.Util.Validation.ValidatePassword(PasswordBox.Password) is { } passwordError)
        {
            ShowError(passwordError);
            PasswordBox.Focus();
            return;
        }
        if (PasswordBox.Password != ConfirmBox.Password)
        {
            ShowError("Las contraseñas no coinciden.");
            ConfirmBox.Focus();
            return;
        }

        try
        {
            CreateButton.IsEnabled = false;
            var user = AuthService.Setup(AppState.Db, CompanyBox.Text, NameBox.Text, EmailBox.Text, PasswordBox.Password);
            AppState.RefreshCompany();
            AppState.SetCurrentUser(user);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
