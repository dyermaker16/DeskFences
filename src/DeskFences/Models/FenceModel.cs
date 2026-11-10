namespace DeskFences.Models;

public class FenceModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New fence";
    public double Left { get; set; } = 120;
    public double Top { get; set; } = 120;
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 260;
    public bool IsCollapsed { get; set; }
    public string? Tint { get; set; }   // e.g. "#3060A0FF", null = default
    public List<FenceItem> Items { get; set; } = new();

    // FenceKinds.Icons for a normal fence, otherwise a widget (older fences.json files have no
    // Kind, so they load as Icons).
    public string Kind { get; set; } = FenceKinds.Icons;
    public string? Notes { get; set; }   // text of a Notes widget
}
