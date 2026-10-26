using System.Collections.ObjectModel;
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
}
