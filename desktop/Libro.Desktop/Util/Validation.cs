using System.Text.RegularExpressions;

namespace Libro.Desktop.Util;

public static partial class Validation
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    public static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && EmailRegex().IsMatch(value.Trim());

    public static string? ValidateEmail(string? value, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value)) return required ? "Escribe el correo." : null;
        return IsValidEmail(value) ? null : "Escribe un correo válido.";
    }

    public static string? ValidatePassword(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "Escribe una contraseña.";
        return value.Length < 8 ? "La contraseña debe tener al menos 8 caracteres." : null;
    }

    public static string Trim(string? value, int max = int.MaxValue)
    {
        var text = (value ?? "").Trim();
        return text.Length <= max ? text : text[..max];
    }
}
