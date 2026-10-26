using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Services;
using Libro.Desktop.Views;

namespace Libro.Desktop;

public partial class MainWindow : Window
{
    private bool _ready;

    public MainWindow()
    {
        InitializeComponent();
        if (AppState.CurrentUser is { } user)
        {
            UserNameLabel.Text = user.Name;
            UserRoleLabel.Text = Permissions.RoleLabel(user.Role);
            InitialsLabel.Text = Initials(user.Name);
        }
        CompanyLabel.Text = AppState.Company.Name;

        NavList.ItemsSource = BuildNavigation();
        _ready = true;
        NavList.SelectedIndex = 0;
    }

    public void RefreshCompanyChrome() => CompanyLabel.Text = AppState.Company.Name;

    private List<NavItem> BuildNavigation()
    {
        var items = new List<NavItem>
        {
            new("panel", "Panel", "\uE80F"),
            new("movimientos", "Movimientos", "\uE8FD"),
            new("facturas", "Facturas", "\uE8A5"),
            new("contactos", "Contactos", "\uE77B"),
            new("inventario", "Inventario", "\uE719"),
        };

        if (AppState.Can(Permissions.PayrollView))
        {
            items.Add(new("nomina", "Nómina", "\uE8C7"));
            items.Add(new("empleados", "Empleados", "\uE716"));
        }
        if (AppState.Can(Permissions.TaxView))
            items.Add(new("impuestos", "Impuestos", "\uE9D9"));
        if (AppState.Can(Permissions.ReportsView))
            items.Add(new("reportes", "Reportes", "\uE9D2"));

        items.Add(new("configuracion", "Configuración", "\uE713"));
        if (AppState.Can(Permissions.UsersManage) || AppState.Can(Permissions.AuditView))
            items.Add(new("usuarios", "Usuarios", "\uE7EF"));

        return items;
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || NavList.SelectedItem is not NavItem item) return;
        ShowPage(item.Key);
    }

    public void Navigate(string key)
    {
        if (NavList.ItemsSource is not IEnumerable<NavItem> items) return;
        var item = items.FirstOrDefault(candidate => candidate.Key == key);
        if (item is null) return;
        NavList.SelectedItem = item;
    }

    private void ShowPage(string key)
    {
        PageHost.Content = key switch
        {
            "panel" => new DashboardView(),
            "movimientos" => new TransactionsView(),
            "facturas" => new InvoicesView(),
            "contactos" => new ContactsView(),
            "inventario" => new InventoryView(),
            "nomina" => new PayrollView(),
            "empleados" => new EmployeesView(),
            "impuestos" => new TaxesView(),
            "reportes" => new ReportsView(),
            "configuracion" => new SettingsView(),
            "usuarios" => new UsersView(),
            _ => new PlaceholderView("Libro", "Módulo en construcción."),
        };
        if (PageHost.Content is IRefreshable refreshable) refreshable.Refresh();
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this, "¿Quieres cerrar sesión?", "Libro",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        AppState.SignOut();
        var login = new LoginWindow();
        if (login.ShowDialog() == true)
        {
            var main = new MainWindow();
            Application.Current.MainWindow = main;
            main.Show();
        }
        else
        {
            Application.Current.Shutdown();
        }
        Close();
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    private sealed record NavItem(string Key, string Label, string Icon);
}
