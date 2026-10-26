using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DeskFences.Models;
using DeskFences.Services;

namespace DeskFences;

public partial class App : Application
{
    // Kept as a field so the mutex lives as long as the app does.
    private Mutex? _mutex;

    // Only the instance that owns the mutex creates these. A second launch must never save over the file.
    private SettingsStore? _store;
    private TrayIcon? _tray;
    private DesktopDoubleClick? _desktopClick;
    private readonly List<FenceWindow> _fences = new();
    private bool _fencesVisible = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "DeskFences.SingleInstance", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        _store = new SettingsStore();
        _store.Load();
        if (_store.Settings.Fences.Count == 0)
        {
            // First run: two starter fences.
            _store.Settings.Fences.Add(new FenceModel { Title = "Games" });
            _store.Settings.Fences.Add(new FenceModel { Title = "Dev", Left = 520, Top = 120 });
            _store.SaveNow();
        }

        foreach (var model in _store.Settings.Fences)
            OpenFence(model);

        _tray = new TrayIcon(_store.Settings.StartWithWindows, _store.Settings.DoubleClickDesktopToggles);
        _tray.NewFenceClicked += NewFence;
        _tray.ToggleFencesClicked += ToggleFences;
        _tray.StartWithWindowsChanged += SetStartWithWindows;
        _tray.DoubleClickDesktopChanged += SetDoubleClickDesktop;
        _tray.QuitClicked += Shutdown;

        _desktopClick = new DesktopDoubleClick(ToggleFences);
        if (_store.Settings.DoubleClickDesktopToggles)
            _desktopClick.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store?.SaveNow();
        _desktopClick?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }

    private void OpenFence(FenceModel model)
    {
        var window = new FenceWindow(model, _store!);
        window.NewFenceRequested += NewFence;
        window.DeleteRequested += DeleteFence;
        _fences.Add(window);
        window.Show();
    }

    private void DeleteFence(FenceWindow window)
    {
        _fences.Remove(window);
        _store!.Settings.Fences.Remove(window.Model);
        _store.SaveNow();
        window.Close();
    }

    private void NewFence()
    {
        // A new fence while the others are hidden would look lonely: show them all first.
        if (!_fencesVisible)
            ToggleFences();

        // Starts in the middle of the work area, so it isn't hidden behind the taskbar.
        var area = SystemParameters.WorkArea;
        var model = new FenceModel { Title = "New fence" };
        model.Left = area.Left + (area.Width - model.Width) / 2;
        model.Top = area.Top + (area.Height - model.Height) / 2;

        _store!.Settings.Fences.Add(model);
        _store.SaveNow();
        OpenFence(model);
    }

    private void ToggleFences()
    {
        _fencesVisible = !_fencesVisible;
        foreach (var window in _fences)
        {
            if (_fencesVisible)
                window.Show();
            else
                window.Hide();
        }
        _tray?.SetFencesVisible(_fencesVisible);
    }

    // A desktop widget should not vanish over one bad message: write the error to
    // %AppData%\DeskFences\crash.log and keep running.
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.AppendAllText(Path.Combine(SettingsStore.Folder, "crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException) { }
        e.Handled = true;
    }

    private void SetDoubleClickDesktop(bool enabled)
    {
        _store!.Settings.DoubleClickDesktopToggles = enabled;
        _store.RequestSave();
        if (enabled)
            _desktopClick!.Start();
        else
            _desktopClick!.Stop();
    }

    private void SetStartWithWindows(bool enabled)
    {
        _store!.Settings.StartWithWindows = enabled;
        _store.RequestSave();
        StartupRegistration.Set(enabled);
    }
}
