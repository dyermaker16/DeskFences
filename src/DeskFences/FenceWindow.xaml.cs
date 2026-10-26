using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DeskFences.Models;
using DeskFences.Native;
using DeskFences.Services;
using Wpf.Ui.Controls;

namespace DeskFences;

public partial class FenceWindow : FluentWindow
{
    private const char Quote = '"';

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

    private void Tile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
            return;

        if (TileOf(sender) is { } tile)
            OpenItem(tile);
        e.Handled = true;
    }

    private void Tile_Open_Click(object sender, RoutedEventArgs e)
    {
        if (TileOf(sender) is { } tile)
            OpenItem(tile);
    }

    private void Tile_OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (TileOf(sender) is not { } tile)
            return;

        // For a shortcut, select the real target; fall back to the shortcut itself.
        var path = tile.Item.Path;
        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            path = ShortcutResolver.GetTarget(path) ?? path;

        var target = File.Exists(path) || Directory.Exists(path) ? path : tile.Item.Path;
        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            Arguments = $"/select,{Quote}{target}{Quote}",
            UseShellExecute = false
        });
    }

    private void Tile_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (TileOf(sender) is not { } tile)
            return;

        // Removes the tile from this fence only. The file itself is never touched.
        Model.Items.Remove(tile.Item);
        Tiles.Remove(tile);
    }

    private void OpenItem(ItemTile tile)
    {
        try
        {
            Process.Start(new ProcessStartInfo(tile.Item.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not open {tile.Name}.\n\n{ex.Message}", "Desk Fences");
        }
    }

    // The tile is the DataContext of the element that was clicked, or of the menu item's owner.
    private static ItemTile? TileOf(object sender)
    {
        return (sender as FrameworkElement)?.DataContext as ItemTile;
    }
}
