using System.IO;
using System.Windows.Media;
using DeskFences.Models;
using DeskFences.Services;

namespace DeskFences;

// One item shown in a fence: its name, icon and whether the file still exists.
public class ItemTile
{
    public ItemTile(FenceItem item)
    {
        Item = item;
        IsMissing = !File.Exists(item.Path) && !Directory.Exists(item.Path);
        Name = Directory.Exists(item.Path)
            ? Path.GetFileName(item.Path.TrimEnd('\\'))
            : Path.GetFileNameWithoutExtension(item.Path);
        Icon = IconService.GetIcon(item.Path);
    }

    public FenceItem Item { get; }
    public string Name { get; }
    public bool IsMissing { get; }
    public ImageSource? Icon { get; }
}
