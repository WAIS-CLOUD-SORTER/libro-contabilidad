using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Libro.Desktop.Services;
using Libro.Desktop.Views;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop;

public partial class MainWindow : Window
{
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _suppressHistory;
    private List<NavItem> _navigation = new();
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

        _navigation = BuildNavigation();
        NavList.ItemsSource = _navigation;
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
        var item = _navigation.FirstOrDefault(candidate => candidate.Key == key);
        if (item is null) return;
        NavList.SelectedItem = item;
    }

    private void ShowPage(string key)
    {
        if (!_suppressHistory && (_history.Count == 0 || _history[^1] != key))
        {
            // Al navegar después de retroceder, se descarta la rama futura.
            if (_historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(key);
            _historyIndex = _history.Count - 1;
        }

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

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (modifiers == ModifierKeys.Control && DigitIndex(key) is { } index)
        {
            if (index < _navigation.Count)
            {
                Navigate(_navigation[index].Key);
                e.Handled = true;
            }
            return;
        }

        if (modifiers == ModifierKeys.Alt && key is Key.Left or Key.Right)
        {
            TravelHistory(key == Key.Left ? -1 : 1);
            e.Handled = true;
            return;
        }

        // Las teclas de función no chocan con la captura de texto, así que se
        // aceptan sin modificadores desde cualquier pantalla.
        switch (key)
        {
            case Key.F1 when modifiers == ModifierKeys.None:
                ShowShortcuts();
                e.Handled = true;
                break;
            case Key.F5 when modifiers == ModifierKeys.None:
                RefreshCurrentPage();
                e.Handled = true;
                break;
        }
    }

    private static int? DigitIndex(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D1,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1,
        _ => null,
    };

    private void TravelHistory(int delta)
    {
        var next = _historyIndex + delta;
        if (next < 0 || next >= _history.Count) return;
        _historyIndex = next;

        _suppressHistory = true;
        try
        {
            Navigate(_history[next]);
        }
        finally
        {
            _suppressHistory = false;
        }
    }

    private void RefreshCurrentPage()
    {
        if (PageHost.Content is IRefreshable refreshable) refreshable.Refresh();
    }

    private void ShowShortcuts() => new ShortcutsDialog { Owner = this }.ShowDialog();

    private void ShortcutsButton_Click(object sender, RoutedEventArgs e) => ShowShortcuts();

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

    private sealed record NavItem(string Key, string Label, string Icon)
    {
        // WPF expone ToString() como nombre de accesibilidad. Sin esto un lector
        // de pantalla anuncia el volcado del record en lugar del módulo.
        public override string ToString() => Label;
    }
}
