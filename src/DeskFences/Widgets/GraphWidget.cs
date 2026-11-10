using System.Windows;
using System.Windows.Controls;
using DeskFences.Models;
using DeskFences.Services;

namespace DeskFences.Widgets;

// CPU, Memory and Network: a big number, a detail line, and a one-minute graph.
public sealed class GraphWidget : Grid, IDisposable
{
    private const double NetworkFloor = 64 * 1024;   // the network graph never zooms in past 64 KB/s

    private readonly string _kind;
    private readonly SystemMonitor _monitor;
    private readonly TextBlock _value = ThemedText(26, 1.0);
    private readonly TextBlock _detail = ThemedText(12, 0.75);
    private readonly LineGraph _graph = new() { Margin = new Thickness(0, 6, 0, 0) };

    public GraphWidget(string kind, SystemMonitor monitor)
    {
        _kind = kind;
        _monitor = monitor;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _detail.VerticalAlignment = VerticalAlignment.Bottom;
        _detail.Margin = new Thickness(10, 0, 0, 5);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(_value);
        header.Children.Add(_detail);
        Children.Add(header);

        SetRow(_graph, 1);
        _graph.SetResourceReference(LineGraph.StrokeProperty, "FenceForeground");
        _graph.SetResourceReference(LineGraph.SecondStrokeProperty, "FenceAccent");
        Children.Add(_graph);

        _monitor.Updated += Refresh;
        _monitor.AddUser();
        Refresh();
    }

    public void Dispose()
    {
        _monitor.Updated -= Refresh;
        _monitor.RemoveUser();
    }

    internal static TextBlock ThemedText(double size, double opacity)
    {
        var text = new TextBlock { FontSize = size, Opacity = opacity };
        text.SetResourceReference(TextBlock.ForegroundProperty, "FenceForeground");
        return text;
    }

    private void Refresh()
    {
        switch (_kind)
        {
            case FenceKinds.Cpu:
                _value.Text = $"{_monitor.Cpu.Latest:0}%";
                _detail.Text = "CPU load";
                _graph.Show(_monitor.Cpu, null, 100);
                break;

            case FenceKinds.Memory:
                _value.Text = $"{_monitor.Memory.Latest:0}%";
                _detail.Text = $"{Units.Size(_monitor.MemoryUsed)} of {Units.Size(_monitor.MemoryTotal)}"
                               + $"  ·  swap {Units.Size(_monitor.CommitUsed)}";
                _graph.Show(_monitor.Memory, null, 100);
                break;

            case FenceKinds.Network:
                _value.Text = $"↓ {Units.Rate(_monitor.NetIn.Latest)}";
                _detail.Text = $"↑ {Units.Rate(_monitor.NetOut.Latest)}";
                var max = Math.Max(NetworkFloor, Math.Max(_monitor.NetIn.Max(), _monitor.NetOut.Max()));
                _graph.Show(_monitor.NetIn, _monitor.NetOut, max);
                break;
        }
    }
}
