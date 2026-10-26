using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskFences.Models;
using DeskFences.Native;
using DeskFences.Services;
using Wpf.Ui.Controls;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using RoutedEventHandler = System.Windows.RoutedEventHandler;

namespace DeskFences;

public partial class FenceWindow : FluentWindow
{
    private const char Quote = '"';

    // Same six presets as the plan. Null = no colour.
    private static readonly (string Name, string? Tint)[] ColourPresets =
    {
        ("Default", null),
        ("Blue", "#3060A0FF"),
        ("Green", "#3040C070"),
        ("Purple", "#30A060FF"),
        ("Red", "#30FF5050"),
        ("Orange", "#30FFA040"),
    };

    private readonly SettingsStore _store;
    private bool _renaming;
    private readonly ContextMenu _titleMenu;

    public FenceModel Model { get; }
    public ObservableCollection<ItemTile> Tiles { get; } = new();

    // App owns the fence list, so these ask it to act.
    public event Action? NewFenceRequested;
    public event Action<FenceWindow>? DeleteRequested;

    public FenceWindow(FenceModel model, SettingsStore store)
    {
        Model = model;
        _store = store;
        InitializeComponent();

        Title = model.Title;
        TitleBarControl.Title = model.Title;
        Left = model.Left;
        Top = model.Top;
        Width = model.Width;
        Height = model.Height;

        _titleMenu = BuildTitleMenu();
        ApplyTint();

        foreach (var item in model.Items)
            Tiles.Add(new ItemTile(item));
        TileList.ItemsSource = Tiles;

        SourceInitialized += (_, _) =>
        {
            HideFromAltTab();
            DesktopAttach.Attach(this);
        };

        // WPF-UI's TitleBar adds its own hook when it loads, and shows the system menu on a
        // right-click. Hooks run newest first, so add ours after it to get those clicks first.
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(TitleBarHook));

        // Only a normal (not maximized/minimized) position is worth saving.
        LocationChanged += (_, _) =>
        {
            if (WindowState != WindowState.Normal)
                return;
            Model.Left = Left;
            Model.Top = Top;
            _store.RequestSave();
        };
        SizeChanged += (_, _) =>
        {
            if (WindowState != WindowState.Normal)
                return;
            Model.Width = Width;
            Model.Height = Height;
            _store.RequestSave();
        };

        Loaded += (_, _) => EnsureOnScreen();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    // A tool window never shows in Alt+Tab or the taskbar.
    // No maximize box either, so Windows never maximizes a fence (e.g. on a title double-click).
    private void HideFromAltTab()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE,
            Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE) | Win32.WS_EX_TOOLWINDOW);
        Win32.SetWindowLong(hwnd, Win32.GWL_STYLE,
            Win32.GetWindowLong(hwnd, Win32.GWL_STYLE) & ~Win32.WS_MAXIMIZEBOX);
    }

    // WPF never sees mouse clicks on the title bar (they are non-client), so handle them here:
    // double-click = rename, right-click = fence menu.
    private IntPtr TitleBarHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (wParam.ToInt32() != Win32.HTCAPTION || _renaming)
            return IntPtr.Zero;

        if (msg == Win32.WM_NCLBUTTONDBLCLK)
        {
            BeginRename();
            handled = true;
        }
        else if (msg == Win32.WM_NCRBUTTONUP)
        {
            _titleMenu.PlacementTarget = this;
            _titleMenu.IsOpen = true;
            handled = true;
        }
        return IntPtr.Zero;
    }

    private const double TitleBarHeight = 32;

    // Keeps the fence on the visible desktop: shrinks it if it's bigger than the screen,
    // and moves it so at least its title bar is visible.
    private void EnsureOnScreen()
    {
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;

        Width = Math.Min(Width, screenWidth);
        Height = Math.Min(Height, screenHeight);
        Left = Math.Clamp(Left, screenLeft, screenLeft + screenWidth - Width);
        Top = Math.Clamp(Top, screenTop, screenTop + screenHeight - TitleBarHeight);
    }

    // Monitor added, removed or moved: the fence may now be off-screen.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(EnsureOnScreen);
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
        _store.RequestSave();

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
        _store.RequestSave();
    }

    // The menu is built in code and opened from TitleBarHook.
    private ContextMenu BuildTitleMenu()
    {
        var colour = new MenuItem { Header = "Colour" };
        colour.SubmenuOpened += ColourMenu_SubmenuOpened;
        foreach (var (name, tint) in ColourPresets)
        {
            var item = new MenuItem { Header = name, IsCheckable = true, Tag = tint };
            item.Click += (_, _) => SetTint(tint);
            colour.Items.Add(item);
        }

        var menu = new ContextMenu();
        menu.Items.Add(Item("Rename", Rename_Click));
        menu.Items.Add(colour);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(Item("New fence", NewFence_Click));
        menu.Items.Add(Item("Delete fence", Delete_Click));
        return menu;
    }

    private static MenuItem Item(string header, RoutedEventHandler onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += onClick;
        return item;
    }

    private void Rename_Click(object sender, RoutedEventArgs e) => BeginRename();

    private void BeginRename()
    {
        _renaming = true;
        RenameBox.Text = Model.Title;
        RenameBox.Visibility = Visibility.Visible;
        Activate();
        // Focus fails until the box has been laid out, so wait for that first.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            RenameBox.Focus();
            RenameBox.SelectAll();
        });
    }

    // The flag stops LostFocus (fired when the box is hidden) from saving a cancelled rename.
    private void CommitRename()
    {
        if (!_renaming)
            return;

        _renaming = false;
        RenameBox.Visibility = Visibility.Collapsed;

        var title = RenameBox.Text.Trim();
        if (title.Length == 0)
            return;

        Model.Title = title;
        Title = title;
        TitleBarControl.Title = title;
        _store.RequestSave();
    }

    private void CancelRename()
    {
        if (!_renaming)
            return;

        _renaming = false;
        RenameBox.Visibility = Visibility.Collapsed;
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CommitRename();
        else if (e.Key == Key.Escape)
            CancelRename();
        else
            return;

        e.Handled = true;
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e) => CommitRename();

    private void ColourMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        foreach (MenuItem item in ((MenuItem)sender).Items)
            item.IsChecked = string.Equals((string?)item.Tag, Model.Tint, StringComparison.OrdinalIgnoreCase);
    }

    private void SetTint(string? tint)
    {
        Model.Tint = tint;
        ApplyTint();
        _store.RequestSave();
    }

    // A hand-edited fences.json can hold a bad colour: fall back to no colour instead of crashing.
    private void ApplyTint()
    {
        Brush brush = Brushes.Transparent;
        if (!string.IsNullOrEmpty(Model.Tint))
        {
            try { brush = (Brush)new BrushConverter().ConvertFromString(Model.Tint)!; }
            catch (FormatException) { }
        }
        TintLayer.Background = brush;
    }

    private void NewFence_Click(object sender, RoutedEventArgs e) => NewFenceRequested?.Invoke();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        // Only the fence goes. Shortcuts and files on the desktop are never touched.
        var answer = System.Windows.MessageBox.Show(
            $"Delete fence '{Model.Title}'?\n\nThe shortcuts stay on your desktop; only the fence is removed.",
            "Desk Fences", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == System.Windows.MessageBoxResult.Yes)
            DeleteRequested?.Invoke(this);
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
