using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Libro.Desktop.Controls;

public partial class DateField : UserControl
{
    private static readonly string[] Formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
    private DateTime? _selectedDate;
    private bool _updating;

    public DateField()
    {
        InitializeComponent();
    }

    public event EventHandler? DateChanged;

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set => SetDate(value, raiseEvent: false);
    }

    public void FocusDate() => DateText.Focus();

    private void SetDate(DateTime? date, bool raiseEvent)
    {
        _updating = true;
        try
        {
            _selectedDate = date?.Date;
            DateText.Text = _selectedDate?.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-MX")) ?? "";
            if (_selectedDate is not null)
                CalendarDisplay.SelectedDate = _selectedDate;
            else
                CalendarDisplay.SelectedDates.Clear();
        }
        finally
        {
            _updating = false;
        }

        if (raiseEvent) DateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        if (CalendarPopup.IsOpen)
        {
            CalendarPopup.IsOpen = false;
            return;
        }

        _updating = true;
        CalendarDisplay.SelectedDate = _selectedDate ?? DateTime.Today;
        _updating = false;
        CalendarPopup.IsOpen = true;
    }

    private void CalendarDisplay_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        SetDate(CalendarDisplay.SelectedDate, raiseEvent: true);
        CalendarPopup.IsOpen = false;
    }

    private void DateText_LostFocus(object sender, RoutedEventArgs e)
    {
        FieldBorder.BorderBrush = (Brush)FindResource("LineBrush");
        CommitText();
    }

    private void DateText_GotFocus(object sender, RoutedEventArgs e)
    {
        FieldBorder.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void DateText_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitText();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitText()
    {
        if (string.IsNullOrWhiteSpace(DateText.Text))
        {
            if (_selectedDate is not null) SetDate(null, raiseEvent: true);
            return;
        }

        if (DateTime.TryParseExact(DateText.Text.Trim(), Formats, CultureInfo.GetCultureInfo("es-MX"),
                DateTimeStyles.None, out var parsed) ||
            DateTime.TryParse(DateText.Text.Trim(), CultureInfo.GetCultureInfo("es-MX"),
                DateTimeStyles.None, out parsed))
        {
            SetDate(parsed, raiseEvent: true);
            return;
        }

        SetDate(_selectedDate, raiseEvent: false);
    }
}
