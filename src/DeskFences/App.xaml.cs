using System.Threading;
using System.Windows;
using DeskFences.Models;

namespace DeskFences;

public partial class App : Application
{
    // Kept as a field so the mutex lives as long as the app does.
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "DeskFences.SingleInstance", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Temporary until saving (Phase 4) and the tray "New fence" (Phase 5) exist.
        new FenceWindow(new FenceModel { Title = "Games" }).Show();
        new FenceWindow(new FenceModel { Title = "Dev", Left = 520, Top = 120 }).Show();
    }
}
