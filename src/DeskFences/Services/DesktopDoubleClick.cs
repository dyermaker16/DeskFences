using System.Runtime.InteropServices;
using System.Windows.Threading;
using DeskFences.Native;

namespace DeskFences.Services;

// Notices a double-click on empty desktop space (not on an icon, not on a window).
// Uses a low-level mouse hook on its own thread: if the UI thread is ever busy for a moment,
// Windows would silently remove a hook that answers too slowly.
public sealed class DesktopDoubleClick : IDisposable
{
    private readonly Action _onDoubleClick;
    private readonly Dispatcher _dispatcher;
    private readonly Win32.LowLevelMouseProc _proc;   // kept in a field so the GC can't collect it

    private Thread? _thread;
    private uint _threadId;
    private bool _hasLastClick;
    private uint _lastClickTime;
    private Win32.POINT _lastClickPoint;

    // onDoubleClick runs on the thread that creates this object (the UI thread).
    public DesktopDoubleClick(Action onDoubleClick)
    {
        _onDoubleClick = onDoubleClick;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _proc = HookProc;
    }

    public void Start()
    {
        if (_thread != null)
            return;

        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "DesktopDoubleClick" };
        _thread.Start();
        ready.Wait();
    }

    public void Stop()
    {
        if (_thread == null)
            return;

        Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
        _hasLastClick = false;
    }

    public void Dispose() => Stop();

    private void Run(ManualResetEventSlim ready)
    {
        _threadId = Win32.GetCurrentThreadId();
        Win32.PeekMessage(out _, IntPtr.Zero, 0, 0, Win32.PM_NOREMOVE);   // creates the message queue for WM_QUIT
        var hook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc, Win32.GetModuleHandle(null), 0);
        ready.Set();

        // The hook is called from inside GetMessage; there is nothing else to dispatch.
        while (Win32.GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }

        if (hook != IntPtr.Zero)
            Win32.UnhookWindowsHookEx(hook);
    }

    // Must return fast: only the timing check happens here, the rest on the thread pool.
    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == Win32.WM_LBUTTONDOWN)
        {
            var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            if (IsSecondClick(info))
            {
                _hasLastClick = false;   // a third click starts over instead of toggling again
                var point = info.pt;
                ThreadPool.QueueUserWorkItem(_ => CheckDesktop(point));
            }
            else
            {
                _hasLastClick = true;
                _lastClickTime = info.time;
                _lastClickPoint = info.pt;
            }
        }
        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // Same rules as Windows: within the double-click time, and inside the double-click rectangle.
    private bool IsSecondClick(Win32.MSLLHOOKSTRUCT info)
    {
        return _hasLastClick
            && info.time - _lastClickTime <= Win32.GetDoubleClickTime()
            && Math.Abs(info.pt.X - _lastClickPoint.X) <= Win32.GetSystemMetrics(Win32.SM_CXDOUBLECLK) / 2
            && Math.Abs(info.pt.Y - _lastClickPoint.Y) <= Win32.GetSystemMetrics(Win32.SM_CYDOUBLECLK) / 2;
    }

    private void CheckDesktop(Win32.POINT point)
    {
        // Hook points are physical pixels: read windows and icons in the same units on every monitor.
        var oldContext = Win32.SetThreadDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            var hwnd = Win32.WindowFromPoint(point);
            if (IsDesktop(hwnd) && !IsOverIcon(hwnd, point))
                _dispatcher.BeginInvoke(_onDoubleClick);
        }
        catch (COMException) { }
        finally
        {
            Win32.SetThreadDpiAwarenessContext(oldContext);
        }
    }

    // The desktop is Explorer's Progman or WorkerW window, or anything inside it
    // (the icon list, or an animated wallpaper placed behind the icons).
    private static bool IsDesktop(IntPtr hwnd)
    {
        var root = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
        var rootClass = Win32.ClassName(root);
        if (rootClass != "Progman" && rootClass != "WorkerW")
            return false;

        Win32.GetWindowThreadProcessId(root, out var rootProcess);
        Win32.GetWindowThreadProcessId(Win32.GetShellWindow(), out var shellProcess);
        return rootProcess == shellProcess;
    }

    // Only the icon list can have icons. Ask it which item is under the point: 0 = empty space.
    // (Desktop icons hidden: the list is hidden too, so the point never lands on it.)
    private static bool IsOverIcon(IntPtr hwnd, Win32.POINT point)
    {
        if (Win32.ClassName(hwnd) != "SysListView32" || Win32.ClassName(Win32.GetParent(hwnd)) != "SHELLDLL_DefView")
            return false;

        var iid = Win32.IID_IAccessible;
        if (Win32.AccessibleObjectFromWindow(hwnd, Win32.OBJID_CLIENT, ref iid, out var list) != 0 || list == null)
            return true;   // can't tell: better to do nothing than to hide fences on an icon double-click

        var hit = list.accHitTest(point.X, point.Y);
        return hit is not int id || id != 0;
    }
}
