using System.Globalization;

namespace Libro.Desktop.Util;

public static class Money
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    public static string Format(long cents) => (cents / 100m).ToString("C2", Mx);

    public static string FormatCompact(long cents)
    {
        var value = cents / 100m;
        if (Math.Abs(value) >= 1_000_000) return (value / 1_000_000m).ToString("C1", Mx) + "M";
        if (Math.Abs(value) >= 1_000) return (value / 1_000m).ToString("C1", Mx) + "k";
        return value.ToString("C0", Mx);
    }

    public static string CentsToInput(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    public static long? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = new string(raw.Trim().Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (string.IsNullOrWhiteSpace(value)) return null;

        var hasComma = value.Contains(',');
        var hasDot = value.Contains('.');
        if (hasComma && hasDot)
        {
            if (value.LastIndexOf(',') > value.LastIndexOf('.'))
                value = value.Replace(".", "").Replace(',', '.');
            else
                value = value.Replace(",", "");
        }
        else if (hasComma)
        {
            var parts = value.Split(',');
            value = parts.Length == 2 && parts[1].Length <= 2
                ? parts[0] + "." + parts[1]
                : value.Replace(",", "");
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return null;
        return (long)Math.Round(parsed * 100m, MidpointRounding.AwayFromZero);
    }

    public static double? ParseQty(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = new string(raw.Trim().Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray())
            .Replace(',', '.');
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return null;
        return Math.Round(parsed, 3, MidpointRounding.AwayFromZero);
    }

    public static string FormatQty(double qty) => qty.ToString("#,##0.###", Mx);

    public static string FormatPercent(double rate) => (rate * 100).ToString("0.##", Mx) + "%";

    public static (long Subtotal, long Iva, long Total) LineTotals(double qty, long unitPriceCents, double ivaRate)
    {
        var subtotal = (long)Math.Round(qty * unitPriceCents, MidpointRounding.AwayFromZero);
        var iva = (long)Math.Round(subtotal * ivaRate, MidpointRounding.AwayFromZero);
        return (subtotal, iva, subtotal + iva);
    }
}
