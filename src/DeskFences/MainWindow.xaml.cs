using System.Runtime.InteropServices;
using System.Windows.Interop;
using Wpf.Ui.Controls;

namespace DeskFences;

public partial class MainWindow : FluentWindow
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => HideFromAltTab();
    }

    // A tool window never shows in Alt+Tab or the taskbar.
    private void HideFromAltTab()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, GwlExStyle, GetWindowLong(hwnd, GwlExStyle) | WsExToolWindow);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
