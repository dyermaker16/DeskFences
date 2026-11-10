namespace DeskFences.Models;

// What a fence shows: icons (a normal fence) or a widget.
public static class FenceKinds
{
    public const string Icons = "Icons";
    public const string Cpu = "Cpu";
    public const string Memory = "Memory";
    public const string Network = "Network";
    public const string Drives = "Drives";
    public const string Notes = "Notes";

    // The tray's "New widget" menu, in this order, with a size that fits each widget.
    public static readonly (string Kind, string Title, double Width, double Height)[] Widgets =
    {
        (Cpu, "CPU", 300, 170),
        (Memory, "Memory", 300, 170),
        (Network, "Network", 300, 170),
        (Drives, "Drives", 300, 220),
        (Notes, "Notes", 300, 180),
    };
}
