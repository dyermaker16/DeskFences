using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskFences.Services;

namespace DeskFences.Widgets;

// One bar per fixed drive (used space), and the total drive usage underneath.
public sealed class DrivesWidget : StackPanel, IDisposable
{
    private static readonly Brush TrackBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)));

    private readonly SystemMonitor _monitor;
    private readonly StackPanel _rows = new();
    private readonly TextBlock _total = GraphWidget.ThemedText(12, 0.75);
    private IReadOnlyList<DriveSample>? _shown;

    public DrivesWidget(SystemMonitor monitor)
    {
        _monitor = monitor;
        _total.Margin = new Thickness(0, 6, 0, 0);
        Children.Add(_rows);
        Children.Add(_total);

        _monitor.Updated += Refresh;
        _monitor.AddUser();
        Refresh();
    }

    public void Dispose()
    {
        _monitor.Updated -= Refresh;
        _monitor.RemoveUser();
    }

    // Drives are only re-read every 30 s, so rebuild only when there is a new list.
    private void Refresh()
    {
        var drives = _monitor.Drives;
        if (ReferenceEquals(drives, _shown))
            return;
        _shown = drives;

        _rows.Children.Clear();
        foreach (var drive in drives)
            _rows.Children.Add(Row(drive));

        long used = drives.Sum(d => d.Used), total = drives.Sum(d => d.Total);
        _total.Text = total > 0 ? $"Drive usage {100.0 * used / total:0}%" : "No drives";
    }

    private static Grid Row(DriveSample drive)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = GraphWidget.ThemedText(12, 1.0);
        name.Text = drive.Name;
        name.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(name);

        // The fill takes the used share of the track's width.
        var used = Math.Clamp((double)drive.Used / drive.Total, 0, 1);
        var bar = new Grid { Height = 8, VerticalAlignment = VerticalAlignment.Center };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(used, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - used, GridUnitType.Star) });
        var track = new Border { CornerRadius = new CornerRadius(4), Background = TrackBrush };
        Grid.SetColumnSpan(track, 2);
        bar.Children.Add(track);
        var fill = new Border { CornerRadius = new CornerRadius(4) };
        fill.SetResourceReference(Border.BackgroundProperty, "FenceAccent");
        bar.Children.Add(fill);
        Grid.SetColumn(bar, 1);
        row.Children.Add(bar);

        var size = GraphWidget.ThemedText(11, 0.75);
        size.Text = $"{Units.Size(drive.Used)} / {Units.Size(drive.Total)}";
        size.Margin = new Thickness(8, 0, 0, 0);
        size.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(size, 2);
        row.Children.Add(size);
        return row;
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
