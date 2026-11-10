using System.Windows;
using System.Windows.Media;
using DeskFences.Services;

namespace DeskFences.Widgets;

// A small line graph of the last minute: newest point at the right edge, a soft fill under the
// first series, an optional second series (network out) on top. The brushes are theme resources.
public sealed class LineGraph : FrameworkElement
{
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(LineGraph),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondStrokeProperty = DependencyProperty.Register(
        nameof(SecondStroke), typeof(Brush), typeof(LineGraph),
        new FrameworkPropertyMetadata(Brushes.LightBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Pen GridPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), 1));

    private History? _first;
    private History? _second;
    private double _maximum = 100;

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush SecondStroke
    {
        get => (Brush)GetValue(SecondStrokeProperty);
        set => SetValue(SecondStrokeProperty, value);
    }

    public void Show(History first, History? second, double maximum)
    {
        _first = first;
        _second = second;
        _maximum = maximum > 0 ? maximum : 1;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        // Faint quarter lines, like a HUD grid.
        for (var i = 1; i < 4; i++)
        {
            var y = Math.Round(height * i / 4) + 0.5;
            dc.DrawLine(GridPen, new Point(0, y), new Point(width, y));
        }

        if (_first != null)
            DrawSeries(dc, _first, Stroke, width, height, fill: true);
        if (_second != null)
            DrawSeries(dc, _second, SecondStroke, width, height, fill: false);
    }

    private void DrawSeries(DrawingContext dc, History data, Brush stroke, double width, double height, bool fill)
    {
        if (data.Count < 2)
            return;

        var step = width / (SystemMonitor.HistoryLength - 1);
        var left = width - (data.Count - 1) * step;
        Point PointAt(int i) =>
            new(left + i * step, height - 1 - Math.Clamp(data[i] / _maximum, 0, 1) * (height - 2));

        if (fill)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(left, height), isFilled: true, isClosed: true);
                for (var i = 0; i < data.Count; i++)
                    ctx.LineTo(PointAt(i), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(width, height), isStroked: false, isSmoothJoin: false);
            }
            area.Freeze();

            var color = (stroke as SolidColorBrush)?.Color ?? Colors.White;
            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(0x55, color.R, color.G, color.B),
                Color.FromArgb(0x05, color.R, color.G, color.B), 90);
            fillBrush.Freeze();
            dc.DrawGeometry(fillBrush, null, area);
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(PointAt(0), isFilled: false, isClosed: false);
            for (var i = 1; i < data.Count; i++)
                ctx.LineTo(PointAt(i), isStroked: true, isSmoothJoin: true);
        }
        line.Freeze();
        dc.DrawGeometry(null, new Pen(stroke, 1.5) { LineJoin = PenLineJoin.Round }, line);
    }

    private static Pen Frozen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }
}
