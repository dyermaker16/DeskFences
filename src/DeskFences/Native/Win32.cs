using System.Runtime.InteropServices;

namespace DeskFences.Native;

// Every Win32 call the app makes lives here.
internal static class Win32
{
    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_MAXIMIZEBOX = 0x00010000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    // Clicks on the title bar arrive as non-client messages, because the title bar
    // answers WM_NCHITTEST with HTCAPTION (that's what makes it draggable).
    internal const int WM_NCLBUTTONDBLCLK = 0x00A3;
    internal const int WM_NCRBUTTONUP = 0x00A5;
    internal const int HTCAPTION = 2;

    internal const uint SHGFI_SYSICONINDEX = 0x4000;
    internal const int SHIL_EXTRALARGE = 2;          // 48x48
    internal const int ILD_TRANSPARENT = 1;
    internal static readonly Guid IID_IImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    internal const int GWLP_HWNDPARENT = -8;         // for a top-level window this is the owner
    internal static readonly IntPtr HWND_BOTTOM = 1;
    internal const int WM_WINDOWPOSCHANGING = 0x0046;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("user32.dll")]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    // SetWindowLongPtrW only exists in 64-bit user32.
    internal static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32());
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll")]
    internal static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

    [DllImport("comctl32.dll")]
    internal static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, int flags);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}
