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

    public TrayIcon(bool startWithWindows, bool doubleClickDesktop)
    {
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
            Icon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!,
            Text = "Desk Fences",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ToggleFencesClicked?.Invoke();
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
