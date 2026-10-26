using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Libro.Desktop.Views.Dialogs;

/// <summary>
/// Referencia rápida de atajos. Se llena desde código para no duplicar en XAML
/// una lista que debe coincidir con lo que realmente maneja MainWindow.
/// </summary>
public partial class ShortcutsDialog : Window
{
    public ShortcutsDialog()
    {
        InitializeComponent();

        AddSection("Navegación", new[]
        {
            ("Ir al módulo 1 … 9, en el orden de la barra lateral", "Ctrl + 1…9"),
            ("Volver al módulo anterior", "Alt + ←"),
            ("Ir al módulo siguiente", "Alt + →"),
            ("Actualizar los datos de la pantalla actual", "F5"),
        });

        AddSection("Ayuda y ventanas", new[]
        {
            ("Mostrar esta lista de atajos", "F1"),
            ("Cerrar el diálogo o cancelar", "Esc"),
            ("Aceptar el diálogo", "Enter"),
        });

        AddSection("Consejo", new[]
        {
            ("Los números siguen el orden visible de la barra lateral, así que se " +
             "ajustan solos a los permisos de tu rol.", "—"),
        });
    }

    private void AddSection(string title, IReadOnlyList<(string Description, string Keys)> rows)
    {
        ContentHost.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)FindResource("SectionTitle"),
            Margin = new Thickness(0, ContentHost.Children.Count == 0 ? 0 : 18, 0, 8),
        });

        var card = new Border
        {
            Style = (Style)FindResource("Card"),
            Padding = new Thickness(4, 4, 4, 4),
        };
        var list = new StackPanel();
        card.Child = list;

        foreach (var (description, keys) in rows)
        {
            list.Children.Add(BuildRow(description, keys));
        }

        ContentHost.Children.Add(card);
    }

    private static UIElement BuildRow(string description, string keys)
    {
        var grid = new Grid { Margin = new Thickness(12, 8, 12, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var badge = new Border
        {
            Background = (Brush)Application.Current.Resources["AccentSoftBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 4, 9, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge.Child = new TextBlock
        {
            Text = keys,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentStrongBrush"],
        };
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        return grid;
    }
}
