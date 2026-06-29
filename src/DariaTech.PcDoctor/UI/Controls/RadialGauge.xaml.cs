using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DariaTech.PcDoctor.UI.Controls;

/// <summary>
/// Radialer Tacho (270°-Bogen) für einen Sensorwert. Färbt den Wertbogen
/// grün/gelb/rot anhand optionaler Warn-/Kritisch-Schwellen. Ohne Fremdlib –
/// zeichnet den Bogen selbst, daher robust und voll im Markenlook.
/// </summary>
public partial class RadialGauge : UserControl
{
    private const double StartAngle = 135;   // unten links
    private const double TotalSweep = 270;    // im Uhrzeigersinn bis unten rechts

    private static readonly Brush Green = Frozen("#2FA86A");
    private static readonly Brush Amber = Frozen("#E0B000");
    private static readonly Brush Red = Frozen("#D83A34");

    public RadialGauge()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
        SizeChanged += (_, _) => Redraw();
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.0, OnChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(100.0, OnChanged));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(RadialGauge),
        new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(RadialGauge),
        new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty HasValueProperty = DependencyProperty.Register(
        nameof(HasValue), typeof(bool), typeof(RadialGauge),
        new PropertyMetadata(true, OnChanged));

    public static readonly DependencyProperty WarnThresholdProperty = DependencyProperty.Register(
        nameof(WarnThreshold), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.0, OnChanged));

    public static readonly DependencyProperty CriticalThresholdProperty = DependencyProperty.Register(
        nameof(CriticalThreshold), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.0, OnChanged));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public bool HasValue { get => (bool)GetValue(HasValueProperty); set => SetValue(HasValueProperty, value); }
    public double WarnThreshold { get => (double)GetValue(WarnThresholdProperty); set => SetValue(WarnThresholdProperty, value); }
    public double CriticalThreshold { get => (double)GetValue(CriticalThresholdProperty); set => SetValue(CriticalThresholdProperty, value); }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RadialGauge)d).Redraw();

    private void Redraw()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var thickness = TrackPath.StrokeThickness;
        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2;
        var r = Math.Min(ActualWidth, ActualHeight) / 2 - thickness;
        if (r <= 0) return;

        TrackPath.Data = BuildArc(cx, cy, r, StartAngle, TotalSweep);

        var fraction = Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) : 0;
        ValuePath.Data = HasValue && fraction > 0
            ? BuildArc(cx, cy, r, StartAngle, TotalSweep * fraction)
            : null;
        ValuePath.Stroke = ColorFor(Value);

        ValueLabel.Text = HasValue ? $"{Value:0}{Unit}" : "—";
        CaptionLabel.Text = Caption;
    }

    private Brush ColorFor(double value)
    {
        if (CriticalThreshold > 0 && value >= CriticalThreshold) return Red;
        if (WarnThreshold > 0 && value >= WarnThreshold) return Amber;
        return Green;
    }

    private static Geometry BuildArc(double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        var start = Polar(cx, cy, r, startDeg);
        var end = Polar(cx, cy, r, startDeg + sweepDeg);
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(r, r),
            IsLargeArc = sweepDeg > 180,
            SweepDirection = SweepDirection.Clockwise
        });
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    private static Point Polar(double cx, double cy, double r, double deg)
    {
        var rad = deg * Math.PI / 180;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    private static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
