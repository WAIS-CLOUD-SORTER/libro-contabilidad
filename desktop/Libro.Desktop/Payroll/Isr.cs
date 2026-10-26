using System.Text.Json;
using System.Text.Json.Serialization;
using Libro.Desktop.Util;

namespace Libro.Desktop.Payroll;

public sealed class IsrBracket
{
    public double Lower { get; set; }
    public double? Upper { get; set; }
    public double Rate { get; set; }
    public double Fixed { get; set; }
}

public static class Isr
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static readonly IReadOnlyList<IsrBracket> DefaultBrackets = new List<IsrBracket>
    {
        new() { Lower = 0.01, Upper = 746.04, Rate = 0.0, Fixed = 0 },
        new() { Lower = 746.05, Upper = 6332.05, Rate = 0.064, Fixed = 0 },
        new() { Lower = 6332.06, Upper = 11128.01, Rate = 0.1088, Fixed = 357.69 },
        new() { Lower = 11128.02, Upper = 12935.82, Rate = 0.16, Fixed = 879.25 },
        new() { Lower = 12935.83, Upper = 15487.71, Rate = 0.1792, Fixed = 1168.51 },
        new() { Lower = 15487.72, Upper = 18599.48, Rate = 0.2136, Fixed = 1625.93 },
        new() { Lower = 18599.49, Upper = 37443.06, Rate = 0.2352, Fixed = 2371.29 },
        new() { Lower = 37443.07, Upper = 59049.0, Rate = 0.3, Fixed = 6531.66 },
        new() { Lower = 59049.01, Upper = 89299.0, Rate = 0.32, Fixed = 13113.44 },
        new() { Lower = 89299.01, Upper = 181998.0, Rate = 0.34, Fixed = 22793.44 },
        new() { Lower = 181998.01, Upper = null, Rate = 0.35, Fixed = 54331.1 },
    };

    public static string DefaultJson => JsonSerializer.Serialize(DefaultBrackets, JsonOptions);

    public static List<IsrBracket> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultBrackets.ToList();
        try
        {
            var parsed = JsonSerializer.Deserialize<List<IsrBracket>>(raw, JsonOptions);
            return parsed is { Count: > 0 } ? parsed : DefaultBrackets.ToList();
        }
        catch (JsonException)
        {
            return DefaultBrackets.ToList();
        }
    }

    public static long Compute(long taxableCents, IReadOnlyList<IsrBracket> brackets, string periodType)
    {
        var factor = periodType == "quincenal" ? 0.5 : 1.0;
        var taxable = Math.Max(0, taxableCents / 100d);
        var scaled = brackets.Select(bracket => new IsrBracket
        {
            Lower = bracket.Lower * factor,
            Upper = bracket.Upper * factor,
            Rate = bracket.Rate,
            Fixed = bracket.Fixed * factor,
        }).ToList();

        var selected = scaled.FirstOrDefault(item => item.Upper is not null && taxable <= item.Upper)
                       ?? scaled.LastOrDefault();
        if (selected is null) return 0;
        if (taxable < selected.Lower) return (long)Math.Round(selected.Fixed * 100d, MidpointRounding.AwayFromZero);
        var isr = selected.Fixed + (taxable - selected.Lower) * selected.Rate;
        return Math.Max(0, (long)Math.Round(isr * 100d, MidpointRounding.AwayFromZero));
    }
}
