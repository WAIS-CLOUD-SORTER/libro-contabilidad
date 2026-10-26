using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop.Views;

public partial class EmployeesView : UserControl, IRefreshable
{
    private bool _initialized;

    public EmployeesView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            var canWrite = AppState.Can(Permissions.PayrollWrite);
            NewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            EmptyNewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            WriteActionsColumn.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
            _initialized = true;
        }
        LoadEmployees();
    }

    private void LoadEmployees()
    {
        try
        {
            var includeInactive = InactiveBox.IsChecked == true;
            var employees = EmployeeService.GetEmployees(includeInactive);
            var summary = EmployeeService.GetSummary();
            var query = SearchBox.Text.Trim();

            if (query.Length > 0)
            {
                employees = employees.Where(employee =>
                    employee.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    employee.Rfc.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    employee.Position.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    employee.Department.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
            }

            EmployeesGrid.ItemsSource = employees;
            EmptyPanel.Visibility = employees.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmployeesGrid.Visibility = employees.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            ActiveValue.Text = summary.ActiveCount.ToString();
            InactiveValue.Text = summary.InactiveCount.ToString();
            PayrollValue.Text = Money.Format(summary.MonthlyPayrollCents);
            CountText.Text = employees.Count == 1 ? "1 empleado" : $"{employees.Count} empleados";
        }
        catch (Exception ex)
        {
            AppLog.Error("Empleados", ex);
            MessageBox.Show("No se pudo cargar la plantilla.\n\n" + ex.Message,
                "Libro · Empleados", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => LoadEmployees();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        InactiveBox.IsChecked = false;
        LoadEmployees();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) LoadEmployees();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.Can(Permissions.PayrollWrite)) return;
        var dialog = new EmployeeDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadEmployees();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Employee employee }) return;
        if (!AppState.Can(Permissions.PayrollWrite)) return;
        var dialog = new EmployeeDialog(employee) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadEmployees();
    }

    private void EmployeesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (EmployeesGrid.SelectedItem is Employee employee) EditEmployee(employee);
    }

    private void EditEmployee(Employee employee)
    {
        if (!AppState.Can(Permissions.PayrollWrite)) return;
        var dialog = new EmployeeDialog(employee) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadEmployees();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Employee employee }) return;
        if (!AppState.Can(Permissions.PayrollWrite)) return;

        var activate = employee.Status != "activo";
        var message = activate
            ? "¿Reactivar a este empleado? Volverá a aparecer en nóminas nuevas."
            : "¿Dar de baja a este empleado? Su historial de nómina se conserva.";
        if (MessageBox.Show(message, "Libro · Empleados", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            EmployeeService.ToggleEmployee(employee.Id, activate);
            LoadEmployees();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cambiar estatus de empleado", ex);
            MessageBox.Show(ex.Message, "Libro · Empleados", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
