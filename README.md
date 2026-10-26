# Desk Fences

See-through "fences" for the Windows 11 desktop: labeled boxes that group your shortcuts
(Games, Dev, Create...) without hiding them in folders. They sit on the desktop layer, behind every
window, and blur the wallpaper when you click them.

<!-- Screenshot: docs/screenshot.png (to be added) -->

## Features

- **Drag shortcuts onto a fence.** The original file stays where it is; a fence only stores the path.
  Real Windows icons, double-click to open, right-click for *Open file location* or *Remove from fence*.
- **Stays on the desktop.** Behind all windows, never in Alt+Tab or the taskbar, survives Win+D and
  an Explorer restart.
- **Double-click the empty desktop** to hide or show all fences. Double-clicking an icon still opens it.
- **Roll up** a fence to just its title with the chevron button.
- **Per-fence menu** (right-click the title): rename, colour, new fence, delete. Double-click the title
  to rename.
- **Themes:** *Clean* (Windows 11) and *Terminal* (green monospace), switchable live from the tray.
- **Snaps** to other fences' edges and to a 10 px grid when you move it.
- **Tray icon:** new fence, hide/show all, theme, start with Windows, open the settings folder, quit.
- Everything is saved to `%AppData%\DeskFences\fences.json` (written atomically, so a crash can't
  corrupt it). Sharp on every monitor (per-monitor DPI aware).

Tip: to see only the fences, hide the normal desktop icons (right-click the desktop > View > untick
*Show desktop icons*). Nothing is deleted; untick it again to bring them back.

## Requirements

- Windows 11 (built and tested there; Windows 10 is untested)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build and run

```bash
dotnet build src/DeskFences
dotnet run --project src/DeskFences
```

Release build (one `DeskFences.exe` in `publish/`):

```bash
dotnet publish src/DeskFences -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## How it works

Each fence is a borderless WPF window ([WPF UI](https://github.com/lepoco/wpfui) for the acrylic
backdrop). It stays a top-level window **owned** by the desktop (`Progman`) and is kept at the bottom
of the z-order, instead of being re-parented into the desktop, so it keeps its blur and works with
animated wallpapers. The desktop double-click uses a low-level mouse hook on its own thread and asks
the desktop's icon list (MSAA hit test) whether the click was on an icon.

## Credits

Inspired by [NoFences](https://github.com/Twometer/NoFences) by Twometer and by Stardock Fences.
Built with [WPF UI](https://github.com/lepoco/wpfui).
