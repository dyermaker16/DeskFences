using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DeskFences.Native;

namespace DeskFences.Services;

// Keeps a fence on the desktop layer: above the wallpaper, below every normal window.
// The fence stays a top-level window (no SetParent), so it keeps its blur.
public static class DesktopAttach
{
    public static void Attach(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;

        // Hook first, so nothing (not even Show) can raise the window before it's in place.
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);

        // Owned by the desktop window: stays above the wallpaper and below normal windows,
        // and is not hidden by Show desktop on most builds.
        var progman = Win32.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
            Win32.SetWindowLongPtr(hwnd, Win32.GWLP_HWNDPARENT, progman);

        Win32.SetWindowPos(hwnd, Win32.HWND_BOTTOM, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }

    // Whenever something tries to change the fence's z-order (a click, activation),
    // send it back to the bottom instead.
    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
            if ((pos.flags & Win32.SWP_NOZORDER) == 0)
            {
                pos.hwndInsertAfter = Win32.HWND_BOTTOM;
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        return IntPtr.Zero;
    }
}
