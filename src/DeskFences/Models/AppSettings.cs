namespace DeskFences.Models;

public class AppSettings
{
    public int Version { get; set; } = 1;
    public string Theme { get; set; } = "Clean";
    public bool StartWithWindows { get; set; }
    public List<FenceModel> Fences { get; set; } = new();
}
