using System.Windows;

namespace Libro.Desktop.Views.Dialogs;

public partial class TextPromptDialog : Window
{
    public TextPromptDialog(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ValueBox.Focus();
    }

    public string Value => ValueBox.Text.Trim();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ValueBox.Text))
        {
            ErrorText.Text = "Escribe el motivo.";
            return;
        }
        DialogResult = true;
    }
}
