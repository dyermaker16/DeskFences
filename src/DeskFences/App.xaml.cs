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
        new FenceWindow(new FenceModel { Title = "Games" }).Show();
    }
}
