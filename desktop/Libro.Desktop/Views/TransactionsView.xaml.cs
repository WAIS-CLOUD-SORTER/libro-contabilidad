using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;

namespace Libro.Desktop.Views;

public partial class TransactionsView : UserControl, IRefreshable
{
    private bool _initialized;

    public TransactionsView()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            InitializeFilters();
            _initialized = true;
        }

        LoadTransactions();
    }

    private void InitializeFilters()
    {
        var (start, end) = Dates.CurrentMonthRange();
        FromPicker.SelectedDate = DateTime.ParseExact(start, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        ToPicker.SelectedDate = DateTime.ParseExact(end, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        CategoryFilter.Items.Clear();
        CategoryFilter.Items.Add(new ComboBoxItem { Content = "Todas las categorías", Tag = 0L });
        foreach (var category in CompanyService.GetCategories(AppState.Db, includeArchived: true))
            CategoryFilter.Items.Add(new ComboBoxItem { Content = category.Name, Tag = category.Id });
        CategoryFilter.SelectedIndex = 0;

        var canWrite = AppState.Can(Permissions.TxWrite);
        NewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
        EmptyNewButton.Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
        if (RowsGrid.Columns.Count > 0)
            RowsGrid.Columns[^1].Visibility = canWrite ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadTransactions()
    {
        try
        {
            var filters = ReadFilters();
            var page = TransactionService.GetTransactions(filters, limit: 200);
            var totals = TransactionService.GetTotals(filters);

            RowsGrid.ItemsSource = page.Rows;
            EmptyPanel.Visibility = page.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RowsGrid.Visibility = page.Rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            IncomeValue.Text = Money.Format(totals.IncomeSubtotal);
            ExpenseValue.Text = Money.Format(totals.ExpenseSubtotal);
            NetValue.Text = Money.Format(totals.Net);
            NetValue.Foreground = totals.Net >= 0
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("DangerBrush");
            IvaNetValue.Text = Money.Format(totals.NetIva);
            IvaNetValue.Foreground = totals.NetIva > 0
                ? (Brush)FindResource("WarnBrush")
                : (Brush)FindResource("InkBrush");

            CountText.Text = page.Total == page.Rows.Count
                ? $"{page.Total} movimiento{(page.Total == 1 ? "" : "s")}"
                : $"{page.Total} movimientos · mostrando {page.Rows.Count}";
            SumText.Text = $"Neto: {Money.Format(totals.Net)}";
        }
        catch (Exception ex)
        {
            AppLog.Error("Movimientos", ex);
            MessageBox.Show(
                "No se pudieron cargar los movimientos.\n\n" + ex.Message,
                "Libro · Movimientos",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private TransactionFilters ReadFilters()
    {
        var type = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string;
        long? categoryId = null;
        if (CategoryFilter.SelectedItem is ComboBoxItem category && category.Tag is long id && id > 0)
            categoryId = id;

        return new TransactionFilters
        {
            From = FromPicker.SelectedDate?.ToString("yyyy-MM-dd"),
            To = ToPicker.SelectedDate?.ToString("yyyy-MM-dd"),
            Type = string.IsNullOrWhiteSpace(type) ? null : type,
            CategoryId = categoryId,
            Query = SearchBox.Text.Trim(),
        };
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => LoadTransactions();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var (start, end) = Dates.CurrentMonthRange();
        FromPicker.SelectedDate = DateTime.ParseExact(start, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        ToPicker.SelectedDate = DateTime.ParseExact(end, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        TypeFilter.SelectedIndex = 0;
        CategoryFilter.SelectedIndex = 0;
        SearchBox.Text = "";
        LoadTransactions();
    }

    private void CurrentMonth_Click(object sender, RoutedEventArgs e)
    {
        var (start, end) = Dates.CurrentMonthRange();
        FromPicker.SelectedDate = DateTime.ParseExact(start, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        ToPicker.SelectedDate = DateTime.ParseExact(end, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        LoadTransactions();
    }

    private void AllTime_Click(object sender, RoutedEventArgs e)
    {
        FromPicker.SelectedDate = null;
        ToPicker.SelectedDate = null;
        TypeFilter.SelectedIndex = 0;
        CategoryFilter.SelectedIndex = 0;
        SearchBox.Text = "";
        LoadTransactions();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.Can(Permissions.TxWrite)) return;
        var dialog = new TransactionDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadTransactions();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TransactionRow row }) return;
        if (!AppState.Can(Permissions.TxWrite) || row.Source != "manual") return;
        var dialog = new TransactionDialog(row) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadTransactions();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TransactionRow row }) return;
        if (!AppState.Can(Permissions.TxWrite) || row.Source != "manual") return;

        var confirm = MessageBox.Show(
            $"¿Eliminar el movimiento “{row.Description}” por {Money.Format(row.TotalCents)}?\n\nEsta acción no se puede deshacer.",
            "Libro · Eliminar movimiento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            TransactionService.Delete(row.Id, AppState.UserId);
            LoadTransactions();
        }
        catch (Exception ex)
        {
            AppLog.Error("Eliminar movimiento", ex);
            MessageBox.Show(ex.Message, "Libro · Movimientos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RowsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (RowsGrid.SelectedItem is not TransactionRow row) return;
        if (!AppState.Can(Permissions.TxWrite) || row.Source != "manual") return;

        var dialog = new TransactionDialog(row) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadTransactions();
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
