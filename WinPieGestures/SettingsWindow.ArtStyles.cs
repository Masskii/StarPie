using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinPieGestures.ArtStyles;

namespace WinPieGestures;

public partial class SettingsWindow
{
    private AppConfig? _artPreviewConfig;
    private AppConfig ArtPreviewConfig => _artPreviewConfig ?? ConfigManager.CurrentConfig;
    private readonly List<StackPanel> _artPreviewContents = new();

    private void RefreshArtPreviewText()
    {
        if (_previewStyleRenderer is not ArtStyleRenderer) return;
        for (int i = 0; i < _artPreviewContents.Count; i++)
        {
            foreach (FrameworkElement child in _artPreviewContents[i].Children)
            {
                bool highlighted = i == _lastHoveredSector;
                if (child is TextBlock text) text.Foreground = ArtStyleRenderer.ContentBrush(_previewStyleRenderer,text,highlighted,_previewTextBrush);
                else if (child is System.Windows.Shapes.Path icon) icon.Fill = ArtStyleRenderer.ContentBrush(_previewStyleRenderer,icon,highlighted,_previewTextBrush);
            }
        }
    }
}
