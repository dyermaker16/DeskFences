using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using DeskFences.Models;

namespace DeskFences.Services;

// Loads and saves %AppData%\DeskFences\fences.json. Call from the UI thread.
public sealed class SettingsStore
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskFences");
    private static readonly string FilePath = Path.Combine(Folder, "fences.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly DispatcherTimer _saveTimer;

    public SettingsStore()
    {
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(FilePath))
            return;

        try
        {
            Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Never silently throw away a layout: keep the broken file next to the new one.
            var backup = Path.Combine(Folder, $"fences.json.broken-{DateTime.Now:yyyyMMdd-HHmmss}");
            File.Move(FilePath, backup);
            Settings = new AppSettings();
        }
    }

    // Saves a moment later, so dragging a fence doesn't write on every pixel.
    public void RequestSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        Directory.CreateDirectory(Folder);

        // Write to a temp file first, so a crash mid-write can't corrupt fences.json.
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
