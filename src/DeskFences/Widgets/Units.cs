namespace DeskFences.Widgets;

internal static class Units
{
    private const double KB = 1024;
    private const double MB = KB * 1024;
    private const double GB = MB * 1024;

    public static string Size(double bytes) =>
        bytes >= 1000 * GB ? $"{bytes / (GB * 1024):0.0} TB"
        : bytes >= GB ? $"{bytes / GB:0.0} GB"
        : $"{bytes / MB:0} MB";

    public static string Rate(double bytesPerSecond) =>
        bytesPerSecond >= MB ? $"{bytesPerSecond / MB:0.0} MB/s"
        : bytesPerSecond >= KB ? $"{bytesPerSecond / KB:0} KB/s"
        : $"{bytesPerSecond:0} B/s";
}
