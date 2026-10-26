using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop.Views;

public partial class ContactsView : UserControl, IRefreshable
{
    private bool _initialized;

    public ContactsView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            var canWrite = AppState.Can(Permissions.ContactsWrite);
            NewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            EmptyNewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            if (ContactsGrid.Columns.Count > 0)
                ContactsGrid.Columns[^1].Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            _initialized = true;
        }

        LoadContacts();
    }

    private void LoadContacts()
    {
        try
        {
            var type = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string;
            var rows = ContactService.GetContacts(new ContactFilters
            {
                Query = SearchBox.Text.Trim(),
                Type = string.IsNullOrWhiteSpace(type) ? null : type,
                IncludeArchived = ArchivedCheck.IsChecked == true,
            });
            var stats = ContactService.GetStats();

            ContactsGrid.ItemsSource = rows;
            EmptyPanel.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ContactsGrid.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            ClientCount.Text = stats.Clients.ToString();
            ProviderCount.Text = stats.Providers.ToString();
            ArchivedCount.Text = stats.Archived.ToString();
            ReceivableValue.Text = Money.Format(stats.ReceivableCents);
            ReceivableValue.Foreground = stats.ReceivableCents > 0
                ? (Brush)FindResource("AccentStrongBrush")
                : (Brush)FindResource("InkBrush");

            CountText.Text = $"{rows.Count} contacto{(rows.Count == 1 ? "" : "s")}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Contactos", ex);
            MessageBox.Show("No se pudieron cargar los contactos.\n\n" + ex.Message,
                "Libro · Contactos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => LoadContacts();

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.Can(Permissions.ContactsWrite)) return;
        var dialog = new ContactDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadContacts();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Contact contact }) return;
        if (!AppState.Can(Permissions.ContactsWrite)) return;
        var dialog = new ContactDialog(contact) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadContacts();
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Contact contact }) return;
        if (!AppState.Can(Permissions.ContactsWrite)) return;

        var archive = contact.Archived == 0;
        var question = archive
            ? $"¿Archivar a “{contact.Name}”?\n\nNo se borrará su historial."
            : $"¿Reactivar a “{contact.Name}”?";
        var confirm = MessageBox.Show(question, "Libro · Contactos", MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            ContactService.SetArchived(contact.Id, archive, AppState.UserId);
            LoadContacts();
        }
        catch (Exception ex)
        {
            AppLog.Error("Archivar contacto", ex);
            MessageBox.Show(ex.Message, "Libro · Contactos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ContactsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (ContactsGrid.SelectedItem is not Contact contact) return;
        if (!AppState.Can(Permissions.ContactsWrite)) return;

        var dialog = new ContactDialog(contact) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadContacts();
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
