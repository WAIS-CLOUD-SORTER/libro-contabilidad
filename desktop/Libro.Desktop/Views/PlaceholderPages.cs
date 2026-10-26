using System.Windows.Controls;

namespace Libro.Desktop.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView(string title, string subtitle)
    {
        InitializeComponent();
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
    }
}

