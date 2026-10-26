using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using DeskFences.Models;
using DeskFences.Native;
using Wpf.Ui.Controls;

namespace DeskFences;

public partial class FenceWindow : FluentWindow
{
    public FenceModel Model { get; }
    public ObservableCollection<ItemTile> Tiles { get; } = new();

    public FenceWindow(FenceModel model)
    {
        Model = model;
        InitializeComponent();

        Title = model.Title;
        TitleBarControl.Title = model.Title;
        Left = model.Left;
        Top = model.Top;
        Width = model.Width;
        Height = model.Height;

        foreach (var item in model.Items)
            Tiles.Add(new ItemTile(item));
        TileList.ItemsSource = Tiles;

        SourceInitialized += (_, _) => HideFromAltTab();
    }

    // A tool window never shows in Alt+Tab or the taskbar.
    private void HideFromAltTab()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE,
            Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE) | Win32.WS_EX_TOOLWINDOW);
    }

    // Only accept files. Always "Link", never "Move": the original file stays where it is.
    protected override void OnDragOver(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var path in paths)
        {
            if (Tiles.Any(t => string.Equals(t.Item.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var item = new FenceItem { Path = path };
            Model.Items.Add(item);
            Tiles.Add(new ItemTile(item));
        }

        e.Effects = DragDropEffects.Link;
        e.Handled = true;
    }
}
