using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DariaTech.PcDoctor.UI.Controls;

/// <summary>
/// Einfaches Live-Liniendiagramm für zwei Sensor-Zeitreihen (z. B. CPU- und
/// GPU-Temperatur) mit dezentem Raster. Bindet an <see cref="IEnumerable"/> von
/// <see cref="double"/> und zeichnet bei Änderungen neu. Ohne Fremdbibliothek.
/// </summary>
public partial class SensorGraph : UserControl
{
    private static readonly Brush CpuBrush = Frozen("#6FE0A8");
    private static readonly Brush GpuBrush = Frozen("#F2B441");
    private static readonly Brush GridBrush = Frozen("#22FFFFFF");

    public SensorGraph()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    public static readonly DependencyProperty CpuSeriesProperty = DependencyProperty.Register(
        nameof(CpuSeries), typeof(IEnumerable), typeof(SensorGraph),
        new PropertyMetadata(null, OnSeriesChanged));

    public static readonly DependencyProperty GpuSeriesProperty = DependencyProperty.Register(
        nameof(GpuSeries), typeof(IEnumerable), typeof(SensorGraph),
        new PropertyMetadata(null, OnSeriesChanged));

    public static readonly DependencyProperty YMaxProperty = DependencyProperty.Register(
        nameof(YMax), typeof(double), typeof(SensorGraph),
        new PropertyMetadata(100.0, (d, _) => ((SensorGraph)d).Redraw()));

    public IEnumerable? CpuSeries { get => (IEnumerable?)GetValue(CpuSeriesProperty); set => SetValue(CpuSeriesProperty, value); }
    public IEnumerable? GpuSeries { get => (IEnumerable?)GetValue(GpuSeriesProperty); set => SetValue(GpuSeriesProperty, value); }
    public double YMax { get => (double)GetValue(YMaxProperty); set => SetValue(YMaxProperty, value); }

    private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var graph = (SensorGraph)d;
        if (e.OldValue is INotifyCollectionChanged oldN) oldN.CollectionChanged -= graph.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newN) newN.CollectionChanged += graph.OnCollectionChanged;
        graph.Redraw();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        if (Plot is null) return;
        Plot.Children.Clear();

        double w = ActualWidth, h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        // Raster: 4 horizontale Linien
        for (var i = 1; i < 4; i++)
        {
            var y = h * i / 4;
            Plot.Children.Add(new Line
            {
                X1 = 0, X2 = w, Y1 = y, Y2 = y,
                Stroke = GridBrush, StrokeThickness = 1
            });
        }

        DrawSeries(GpuSeries, GpuBrush, w, h);
        DrawSeries(CpuSeries, CpuBrush, w, h);
    }

    private void DrawSeries(IEnumerable? series, Brush brush, double w, double h)
    {
        if (series is null) return;

        var values = new List<double>();
        foreach (var v in series)
            if (v is double d) values.Add(d);

        if (values.Count < 2) return;

        var max = YMax > 0 ? YMax : 100;
        var poly = new Polyline { Stroke = brush, StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
        var points = new PointCollection();
        for (var i = 0; i < values.Count; i++)
        {
            var x = w * i / (values.Count - 1);
            var y = h - Math.Clamp(values[i] / max, 0, 1) * h;
            points.Add(new Point(x, y));
        }
        poly.Points = points;
        Plot.Children.Add(poly);
    }

    private static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
