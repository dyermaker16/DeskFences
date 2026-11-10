using System.Diagnostics;
using System.IO;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace DeskFences.Services;

// The notification-area icon and its menu. Actions are raised as events; App does the work.
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _toggleItem;
    private readonly WinForms.ToolStripMenuItem _startupItem;

    public event Action? NewFenceClicked;
    public event Action? ToggleFencesClicked;
    public event Action? QuitClicked;
    public event Action<bool>? StartWithWindowsChanged;
    public event Action<bool>? DoubleClickDesktopChanged;
    public event Action<string>? ThemeChanged;

    public TrayIcon(bool startWithWindows, bool doubleClickDesktop, string theme)
    {
        // One checked item at a time, like radio buttons.
        var themeMenu = new WinForms.ToolStripMenuItem("Theme");
        foreach (var name in App.ThemeNames)
        {
            var item = new WinForms.ToolStripMenuItem(name) { Checked = name == theme };
            item.Click += (_, _) =>
            {
                foreach (WinForms.ToolStripMenuItem other in themeMenu.DropDownItems)
                    other.Checked = other == item;
                ThemeChanged?.Invoke(name);
            };
            themeMenu.DropDownItems.Add(item);
        }

        _toggleItem = new WinForms.ToolStripMenuItem("Hide all fences");
        _toggleItem.Click += (_, _) => ToggleFencesClicked?.Invoke();

        var doubleClickItem = new WinForms.ToolStripMenuItem("Double-click desktop hides fences")
        {
            CheckOnClick = true,
            Checked = doubleClickDesktop
        };
        doubleClickItem.CheckedChanged += (_, _) => DoubleClickDesktopChanged?.Invoke(doubleClickItem.Checked);

        _startupItem = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = startWithWindows
        };
        _startupItem.CheckedChanged += (_, _) => StartWithWindowsChanged?.Invoke(_startupItem.Checked);

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("New fence", null, (_, _) => NewFenceClicked?.Invoke());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(doubleClickItem);
        menu.Items.Add(themeMenu);
        menu.Items.Add(_startupItem);
        menu.Items.Add("Open settings folder", null, (_, _) =>
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            Process.Start(new ProcessStartInfo(SettingsStore.Folder) { UseShellExecute = true });
        });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => QuitClicked?.Invoke());

        _icon = new WinForms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Desk Fences",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ToggleFencesClicked?.Invoke();
    }

    // The small-size frame of the app icon (the .ico has a simpler drawing for 16-24 px),
    // instead of the exe's 32 px icon scaled down.
    private static Drawing.Icon LoadTrayIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/DeskFences.ico");
        using var stream = System.Windows.Application.GetResourceStream(uri).Stream;
        return new Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
    }

    public void SetFencesVisible(bool visible)
    {
        _toggleItem.Text = visible ? "Hide all fences" : "Show all fences";
    }

    public void Dispose()
    {
        _icon.Visible = false;   // otherwise a ghost icon stays until the mouse passes over it
        _icon.Dispose();
    }
}
