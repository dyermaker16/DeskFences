using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskFences.Models;
using DeskFences.Services;

namespace DeskFences.Widgets;

// A plain text box straight on the glass. Saved with the fence.
public sealed class NotesWidget : TextBox
{
    public NotesWidget(FenceModel model, SettingsStore store)
    {
        // An explicit empty style skips WPF UI's boxed text-field look.
        Style = new Style(typeof(TextBox));
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        FontSize = 14;
        AcceptsReturn = true;
        AcceptsTab = true;
        TextWrapping = TextWrapping.Wrap;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        SetResourceReference(ForegroundProperty, "FenceForeground");
        SetResourceReference(CaretBrushProperty, "FenceForeground");
        SetResourceReference(FontFamilyProperty, "FenceFontFamily");

        Text = model.Notes ?? "";
        TextChanged += (_, _) =>
        {
            model.Notes = Text;
            store.RequestSave();
        };
    }
}
