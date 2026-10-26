using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DeskFences.Native;

namespace DeskFences.Services;

// Keeps a fence on the desktop layer: above the wallpaper, below every normal window.
// The fence stays a top-level window (no SetParent), so it keeps its blur.
public static class DesktopAttach
{
    // Explorer broadcasts this to every top-level window when it (re)creates the taskbar,
    // e.g. after Explorer restarts. By then the old Progman, our owner, is gone.
    private static readonly int TaskbarCreated = (int)Win32.RegisterWindowMessage("TaskbarCreated");

    public static void Attach(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        // Hook first, so nothing (not even Show) can raise the window before it's in place.
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);

        SetOwner(hwnd);
        SendToBottom(hwnd);
    }

    // Owned by the desktop window: stays above the wallpaper and below normal windows,
    // and is not hidden by Show desktop on most builds.
    private static bool SetOwner(IntPtr hwnd)
    {
        var progman = Win32.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            return false;

        Win32.SetWindowLongPtr(hwnd, Win32.GWLP_HWNDPARENT, progman);
        return true;
    }

    private static void SendToBottom(IntPtr hwnd)
    {
        Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }

    // The new Progman may not exist yet when the taskbar is announced: retry for ~5 seconds.
    private static void Reattach(IntPtr hwnd, int triesLeft = 10)
    {
        if (SetOwner(hwnd))
        {
            SendToBottom(hwnd);
            return;
        }
        if (triesLeft == 0)
            return;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Reattach(hwnd, triesLeft - 1);
        };
        timer.Start();
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Whenever something tries to change the fence's z-order (a click, activation),
        // send it back to the bottom instead.
        if (msg == Win32.WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
            if ((pos.flags & Win32.SWP_NOZORDER) == 0)
            {
                pos.hwndInsertAfter = Win32.HWND_BOTTOM;
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        else if (msg == TaskbarCreated)
        {
            Reattach(hwnd);
        }
        return IntPtr.Zero;
    }
}
