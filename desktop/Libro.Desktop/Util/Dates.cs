using System.Globalization;

namespace Libro.Desktop.Util;

public static class Dates
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo Tz = ResolveTimeZone();

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Central Standard Time (Mexico)", "America/Mexico_City" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }

    public static string TodayIso() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string NowIso() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static bool IsValidIsoDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 10) return false;
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
    }

    public static (int Year, int Month) CurrentMonth()
    {
        var parts = TodayIso().Split('-');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public static (string Start, string End) MonthRange(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        return (start.ToString("yyyy-MM-dd"), start.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd"));
    }

    public static (string Start, string End) CurrentMonthRange()
    {
        var (year, month) = CurrentMonth();
        return MonthRange(year, month);
    }

    public static (string Start, string End) YearRange(int year) => ($"{year}-01-01", $"{year}-12-31");

    public static string FormatDate(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return "—";
        if (!DateTime.TryParse(iso.Length <= 10 ? iso : iso[..10], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) return "—";
        return date.ToString("dd MMM yyyy", Mx);
    }

    public static string FormatDateTime(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return "—";
        var normalized = iso.Contains('T') ? iso : iso.Replace(' ', 'T') + "Z";
        if (!DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal,
                out var utc)) return "—";
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tz);
        return local.ToString("dd MMM yyyy, HH:mm", Mx);
    }

    public static string MonthLabel(int month) =>
        Capitalize(new DateTime(2024, month, 1).ToString("MMMM", Mx));

    public static string ShortMonthLabel(int year, int month) =>
        Capitalize(new DateTime(year, month, 1).ToString("MMM", Mx));

    public static string PayrollPeriodLabel(long year, long month, string type, long quincena)
    {
        var label = $"{MonthLabel((int)month)} {year}";
        return type == "quincenal" ? $"{label} · {(quincena == 1 ? "1.ª" : "2.ª")} quincena" : label;
    }

    public static List<MonthInfo> LastMonths(int count)
    {
        var (year, month) = CurrentMonth();
        var current = new DateTime(year, month, 1);
        var result = new List<MonthInfo>();
        for (var i = count - 1; i >= 0; i--)
        {
            var date = current.AddMonths(-i);
            var range = MonthRange(date.Year, date.Month);
            result.Add(new MonthInfo(date.Year, date.Month, ShortMonthLabel(date.Year, date.Month), range.Start, range.End));
        }
        return result;
    }

    public static int DaysInMonth(int year, int month) => DateTime.DaysInMonth(year, month);

    public static int DiffDays(string from, string to)
    {
        var a = DateTime.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var b = DateTime.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (int)Math.Round((b - a).TotalDays);
    }

    public static string AddDays(string iso, int days) =>
        DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(days)
            .ToString("yyyy-MM-dd");

    public static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0], Mx) + value[1..];

    public sealed record MonthInfo(int Year, int Month, string Label, string Start, string End);
}
