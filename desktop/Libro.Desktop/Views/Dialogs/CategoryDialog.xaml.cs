using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class CategoryDialog : Window
{
    private readonly Category? _category;

    public CategoryDialog(Category? category = null)
    {
        InitializeComponent();
        _category = category;
        KindBox.SelectedIndex = 0;
        if (category is null) return;
        TitleText.Text = "Editar categoría";
        NameBox.Text = category.Name;
        KindBox.SelectedIndex = category.Kind == "ingreso" ? 1 : 0;
        ArchivedBox.IsChecked = category.Archived == 1;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Escribe el nombre de la categoría.";
            return;
        }
        var category = new Category
        {
            Id = _category?.Id ?? 0,
            Name = NameBox.Text.Trim(),
            Kind = (KindBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "gasto",
            Archived = ArchivedBox.IsChecked == true ? 1 : 0,
        };
        try
        {
            CompanyService.SaveCategory(AppState.Db, category, AppState.UserId);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar categoría", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
