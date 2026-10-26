using System.Globalization;
using System.Windows;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Desktop.Views.Dialogs;

public partial class PayrollItemDialog : Window
{
    private readonly PayrollPeriod _period;
    private readonly PayrollItem _item;
    private bool _ready;

    public PayrollItemDialog(PayrollPeriod period, PayrollItem item)
    {
        InitializeComponent();
        _period = period;
        _item = item;

        NameText.Text = item.EmployeeName ?? "Concepto de nómina";
        SubtitleText.Text = $"{period.Label} · {item.EmployeePosition}";
        DaysBox.Text = item.Days.ToString("0.###", CultureInfo.InvariantCulture);
        BaseBox.Text = Money.CentsToInput(item.BaseCents);
        ExtraBox.Text = Money.CentsToInput(item.ExtraPerceptionsCents);
        IsrBox.Text = Money.CentsToInput(item.IsrCents);
        ImssBox.Text = Money.CentsToInput(item.ImssCents);
        InfonavitBox.Text = Money.CentsToInput(item.InfonavitCents);
        OtherBox.Text = Money.CentsToInput(item.OtherDeductionsCents);
        NotesBox.Text = item.Notes;
        _ready = true;
        UpdateNet();
    }

    private void Amount_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateNet();

    private void UpdateNet()
    {
        if (!_ready) return;
        var baseCents = Money.ParseAmount(BaseBox.Text) ?? 0;
        var extra = Money.ParseAmount(ExtraBox.Text) ?? 0;
        var isr = Money.ParseAmount(IsrBox.Text) ?? 0;
        var imss = Money.ParseAmount(ImssBox.Text) ?? 0;
        var infonavit = Money.ParseAmount(InfonavitBox.Text) ?? 0;
        var other = Money.ParseAmount(OtherBox.Text) ?? 0;
        var net = Math.Max(0, baseCents + extra - isr - imss - infonavit - other);
        NetText.Text = Money.Format(net);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (!double.TryParse(DaysBox.Text.Trim(), NumberStyles.Number,
                CultureInfo.CurrentCulture, out var days) || days < 0)
        {
            ErrorText.Text = "Escribe los días trabajados.";
            return;
        }

        var amounts = new[]
        {
            BaseBox.Text, ExtraBox.Text, IsrBox.Text, ImssBox.Text, InfonavitBox.Text, OtherBox.Text,
        };
        var values = new long[amounts.Length];
        for (var i = 0; i < amounts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(amounts[i]))
            {
                values[i] = 0;
                continue;
            }
            var parsed = Money.ParseAmount(amounts[i]);
            if (parsed is null)
            {
                ErrorText.Text = "Revisa los importes: usa números con hasta dos decimales.";
                return;
            }
            values[i] = parsed.Value;
        }

        try
        {
            PayrollService.UpdateItem(_item.Id, _period.Id, days, values[0], values[1],
                values[2], values[3], values[4], values[5], NotesBox.Text);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar concepto de nómina", ex);
            ErrorText.Text = ex.Message;
        }
    }
}
