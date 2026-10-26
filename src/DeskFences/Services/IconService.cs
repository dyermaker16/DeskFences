using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskFences.Native;

namespace DeskFences.Services;

// Gets the 48px Windows icon for a file, folder or shortcut. Call on the UI thread.
public static class IconService
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
            return cached;

        var icon = LoadIcon(path);
        Cache[path] = icon;
        return icon;
    }

    private static ImageSource? LoadIcon(string path)
    {
        var info = new Win32.SHFILEINFO();
        if (Win32.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), Win32.SHGFI_SYSICONINDEX) == IntPtr.Zero)
            return null;

        var iid = Win32.IID_IImageList;
        if (Win32.SHGetImageList(Win32.SHIL_EXTRALARGE, ref iid, out var imageList) != 0)
            return null;

        var hIcon = Win32.ImageList_GetIcon(imageList, info.iIcon, Win32.ILD_TRANSPARENT);
        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            Win32.DestroyIcon(hIcon);
        }
    }
}
