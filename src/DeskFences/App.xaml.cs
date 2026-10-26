using System.Threading;
using System.Windows;
using DeskFences.Models;
using DeskFences.Services;

namespace DeskFences;

public partial class App : Application
{
    // Kept as a field so the mutex lives as long as the app does.
    private Mutex? _mutex;

    // Only the instance that owns the mutex creates this. A second launch must never save over the file.
    private SettingsStore? _store;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "DeskFences.SingleInstance", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _store = new SettingsStore();
        _store.Load();
        if (_store.Settings.Fences.Count == 0)
        {
            // First run: two starter fences. The tray "New fence" comes in Phase 5.
            _store.Settings.Fences.Add(new FenceModel { Title = "Games" });
            _store.Settings.Fences.Add(new FenceModel { Title = "Dev", Left = 520, Top = 120 });
            _store.SaveNow();
        }

        foreach (var model in _store.Settings.Fences)
            new FenceWindow(model, _store).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store?.SaveNow();
        base.OnExit(e);
    }
}
