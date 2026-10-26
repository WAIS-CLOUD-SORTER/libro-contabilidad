using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop.Views;

public sealed class UserListRow
{
    public required User User { get; init; }
    public string Name => User.Name;
    public string Email => User.Email;
    public string RoleLabel => Permissions.RoleLabel(User.Role);
    public string StatusLabel => User.Active == 1 ? "Activo" : "Inactivo";
    public string LastLoginLabel => string.IsNullOrWhiteSpace(User.LastLoginAt)
        ? "Nunca"
        : Dates.FormatDateTime(User.LastLoginAt);
    public string ToggleLabel => User.Active == 1 ? "Desactivar" : "Activar";
}

public sealed class AuditListRow
{
    public required AuditEntry Entry { get; init; }
    public string CreatedLabel => Dates.FormatDateTime(Entry.CreatedAt);
    public string UserLabel => string.IsNullOrWhiteSpace(Entry.UserName) ? "Sistema" : Entry.UserName;
    public string ActionLabel => AuditText.Action(Entry.Action);
    public string EntityLabel => AuditText.Entity(Entry.Entity);
    public string Detail => AuditText.Detail(Entry);
}

public static class AuditText
{
    public static string Action(string action) => action switch
    {
        "setup" => "Configuró",
        "create" => "Creó",
        "update" => "Actualizó",
        "delete" => "Eliminó",
        "archive" => "Archivó",
        "restore" => "Restauró",
        "activate" => "Activó",
        "deactivate" => "Desactivó",
        "reset_password" => "Cambió contraseña",
        "login" => "Inició sesión",
        "logout" => "Cerró sesión",
        "backup" => "Creó respaldo",
        "emit" => "Emitió",
        "cancel" => "Canceló",
        "payment" => "Registró pago",
        "delete_payment" => "Eliminó pago",
        "pay" => "Pagó nómina",
        "revert_payment" => "Revirtió pago",
        "recalculate" => "Recalculó",
        "entrada" => "Entrada",
        "salida" => "Salida",
        "ajuste" => "Ajuste",
        "sale" => "Venta",
        "purchase" => "Compra",
        _ => action,
    };

    public static string Entity(string entity) => entity switch
    {
        "user" => "Usuario",
        "company" => "Negocio",
        "account" => "Cuenta",
        "category" => "Categoría",
        "transaction" => "Movimiento",
        "invoice" => "Factura",
        "payment" => "Pago",
        "contact" => "Contacto",
        "product" => "Producto",
        "inventory" => "Inventario",
        "employee" => "Empleado",
        "payroll" => "Nómina",
        "database" => "Base de datos",
        _ => entity,
    };

    public static string Detail(AuditEntry entry)
    {
        var detail = entry.Detail ?? "";
        if (long.TryParse(detail, out var cents) &&
            entry.Action is "payment" or "delete_payment" or "create" or "update" or "delete" &&
            entry.Entity is "invoice" or "transaction")
            return Money.Format(cents);

        if (detail.StartsWith("Total ", StringComparison.Ordinal) &&
            long.TryParse(detail[6..], out var totalCents))
            return "Total " + Money.Format(totalCents);

        if (detail.StartsWith("Neto ", StringComparison.Ordinal))
        {
            var comma = detail.IndexOf(',');
            if (comma > 5 && long.TryParse(detail[5..comma], out var netCents))
                return "Neto " + Money.Format(netCents) + detail[comma..];
        }

        return detail;
    }
}

public partial class UsersView : UserControl, IRefreshable
{
    private bool _initialized;
    private bool _optionsLoaded;
    private bool _canUsers;
    private bool _canAudit;

    public UsersView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            _canUsers = AppState.Can(Permissions.UsersManage);
            _canAudit = AppState.Can(Permissions.AuditView);

            UsersTab.Visibility = _canUsers ? Visibility.Visible : Visibility.Collapsed;
            AuditTab.Visibility = _canAudit ? Visibility.Visible : Visibility.Collapsed;
            NewUserButton.Visibility = _canUsers ? Visibility.Visible : Visibility.Collapsed;
            UserActionsColumn.Visibility = _canUsers ? Visibility.Visible : Visibility.Collapsed;
            RoleSummaryText.Text = "Administrador, Contador, Capturista y Consulta";

            if (!_canAudit && AuditTab.IsSelected) AuditTab.IsSelected = false;
            if (!_canUsers && UsersTab.IsSelected) UsersTab.IsSelected = false;
            if (_canUsers) UsersTab.IsSelected = true;
            else if (_canAudit)
            {
                AuditTab.IsSelected = true;
                TabList.SelectedItem = AuditTab;
            }

            if (_canAudit && !_optionsLoaded) LoadAuditOptions();
            _initialized = true;
        }

        if (_canUsers) LoadUsers();
        if (_canAudit) LoadAudit();
        UpdatePanels();
    }

    private void UpdatePanels()
    {
        var audit = ReferenceEquals(TabList.SelectedItem, AuditTab);
        UsersPanel.Visibility = audit ? Visibility.Collapsed : Visibility.Visible;
        AuditPanel.Visibility = audit ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePanels();

    private void LoadUsers()
    {
        try
        {
            var users = UserService.GetUsers();
            var summary = UserService.GetSummary();
            UsersGrid.ItemsSource = users.Select(user => new UserListRow { User = user }).ToList();
            TotalValue.Text = summary.Total.ToString();
            ActiveValue.Text = summary.Active.ToString();
            AdminValue.Text = summary.Admins.ToString();
        }
        catch (Exception ex)
        {
            AppLog.Error("Usuarios", ex);
            MessageBox.Show("No se pudieron cargar los usuarios.\n\n" + ex.Message,
                "Libro · Usuarios", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NewUser_Click(object sender, RoutedEventArgs e)
    {
        if (!_canUsers) return;
        var dialog = new UserDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadUsers();
    }

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (!_canUsers || sender is not Button { Tag: UserListRow row }) return;
        var dialog = new UserDialog(row.User) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadUsers();
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (!_canUsers || sender is not Button { Tag: UserListRow row }) return;
        var dialog = new PasswordResetDialog(row.User) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadUsers();
    }

    private void ToggleUser_Click(object sender, RoutedEventArgs e)
    {
        if (!_canUsers || sender is not Button { Tag: UserListRow row }) return;
        var activate = row.User.Active != 1;
        var message = activate
            ? $"¿Reactivar a {row.Name}? Podrá iniciar sesión de nuevo."
            : $"¿Desactivar a {row.Name}? No podrá iniciar sesión, pero su historial se conserva.";
        if (MessageBox.Show(message, "Libro · Usuarios", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            UserService.SetActive(row.User.Id, activate);
            LoadUsers();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cambiar estado de usuario", ex);
            MessageBox.Show(ex.Message, "Libro · Usuarios", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadAuditOptions()
    {
        try
        {
            var actions = AuditService.GetActions();
            var actionItems = new List<ComboBoxItem> { new() { Content = "Todas", Tag = "" } };
            actionItems.AddRange(actions
                .Select(action => new ComboBoxItem { Content = AuditText.Action(action), Tag = action })
                .OrderBy(item => item.Content?.ToString()));
            AuditActionBox.ItemsSource = actionItems;
            AuditActionBox.SelectedIndex = 0;

            var entities = AuditService.GetEntities();
            var entityItems = new List<ComboBoxItem> { new() { Content = "Todas", Tag = "" } };
            entityItems.AddRange(entities
                .Select(entity => new ComboBoxItem { Content = AuditText.Entity(entity), Tag = entity })
                .OrderBy(item => item.Content?.ToString()));
            AuditEntityBox.ItemsSource = entityItems;
            AuditEntityBox.SelectedIndex = 0;
            _optionsLoaded = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Opciones de auditoría", ex);
        }
    }

    private void LoadAudit()
    {
        if (!_canAudit) return;
        try
        {
            var filters = ReadAuditFilters();
            var entries = AuditService.GetEntries(filters);
            AuditGrid.ItemsSource = entries.Select(entry => new AuditListRow { Entry = entry }).ToList();
            AuditCountText.Text = entries.Count == 1 ? "1 registro" : $"{entries.Count} registros";
        }
        catch (Exception ex)
        {
            AppLog.Error("Auditoría", ex);
            MessageBox.Show("No se pudo cargar la auditoría.\n\n" + ex.Message,
                "Libro · Auditoría", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private AuditFilters ReadAuditFilters() => new()
    {
        Query = AuditSearchBox.Text.Trim(),
        Action = (AuditActionBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
        Entity = (AuditEntityBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
        From = AuditFromField.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
        To = AuditToField.SelectedDate?.ToString("yyyy-MM-dd") ?? "",
    };

    private void AuditFilter_Click(object sender, RoutedEventArgs e) => LoadAudit();

    private void AuditClear_Click(object sender, RoutedEventArgs e)
    {
        AuditSearchBox.Text = "";
        AuditActionBox.SelectedIndex = 0;
        AuditEntityBox.SelectedIndex = 0;
        AuditFromField.SelectedDate = null;
        AuditToField.SelectedDate = null;
        LoadAudit();
    }
}
