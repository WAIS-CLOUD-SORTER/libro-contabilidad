using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Libro.Desktop.Util;

namespace Libro.Desktop.Converters;

public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var cents = value switch
        {
            long l => l,
            int i => i,
            decimal d => (long)d,
            double db => (long)db,
            _ => 0L,
        };
        return Money.Format(cents);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class DateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Dates.FormatDate(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class PercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double rate ? Money.FormatPercent(rate) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "borrador" => "Borrador",
            "emitida" => "Emitida",
            "pagada" => "Pagada",
            "cancelada" => "Cancelada",
            "activo" => "Activo",
            "inactivo" => "Inactivo",
            "ingreso" => "Ingreso",
            "gasto" => "Gasto",
            "entrada" => "Entrada",
            "salida" => "Salida",
            "ajuste" => "Ajuste",
            "cliente" => "Cliente",
            "proveedor" => "Proveedor",
            "ambos" => "Cliente y proveedor",
            "efectivo" => "Efectivo",
            "banco" => "Banco",
            "tarjeta" => "Tarjeta",
            _ => value as string ?? "",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class TypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value as string == "ingreso" || value as string == "entrada"
            ? new SolidColorBrush(Color.FromRgb(6, 118, 71))
            : new SolidColorBrush(Color.FromRgb(194, 65, 12));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "pagada" or "activo" or "entrada" => new SolidColorBrush(Color.FromRgb(6, 118, 71)),
            "emitida" => new SolidColorBrush(Color.FromRgb(23, 92, 211)),
            "borrador" or "ajuste" => new SolidColorBrush(Color.FromRgb(181, 71, 8)),
            "cancelada" or "inactivo" or "salida" => new SolidColorBrush(Color.FromRgb(180, 35, 24)),
            _ => new SolidColorBrush(Color.FromRgb(102, 112, 133)),
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class ManualVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value as string == "manual" ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class AutomaticVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value as string == "manual" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class SourceLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "nomina" => "Nómina",
            "inventario" => "Inventario",
            "factura" => "Factura",
            "pago" => "Pago",
            _ => "Automático",
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
