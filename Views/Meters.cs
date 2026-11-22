using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ConsuntivoOre.Views;

/// <summary>
/// Indicatore a semicerchio della dashboard: arco grigio di fondo e arco colorato fino a Value/Maximum.
/// Oltre il massimo resta pieno. Il testo al centro lo mette chi lo usa (sopra, in un Panel).
/// </summary>
public sealed class Gauge : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<Gauge, double>(nameof(Value));
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<Gauge, double>(nameof(Maximum), 1);
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<Gauge, IBrush?>(nameof(Stroke), Brushes.DodgerBlue);
    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<Gauge, IBrush?>(nameof(Track), new SolidColorBrush(Color.FromRgb(229, 231, 235)));
    public static readonly StyledProperty<double> ThicknessProperty = AvaloniaProperty.Register<Gauge, double>(nameof(Thickness), 22);

    static Gauge() => AffectsRender<Gauge>(ValueProperty, MaximumProperty, StrokeProperty, TrackProperty, ThicknessProperty);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    /// <summary>Quota riempita (0–1): senza ore previste, qualsiasi ora registrata riempie l'arco.</summary>
    public static double Fraction(double value, double maximum) =>
        maximum > 0 ? Math.Clamp(value / maximum, 0, 1) : value > 0 ? 1 : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsFinite(availableSize.Width) ? availableSize.Width : 240;
        return new Size(w, w / 2 + Thickness / 2);
    }

    public override void Render(DrawingContext context)
    {
        double t = Thickness;
        double r = Math.Max(1, Math.Min(Bounds.Width - t, (Bounds.Height - t) * 2) / 2);
        var center = new Point(Bounds.Width / 2, t / 2 + r);

        Draw(context, center, r, 1, Track, t);
        double f = Fraction(Value, Maximum);
        if (f > 0) Draw(context, center, r, f, Stroke, t);
    }

    /// <summary>Arco da sinistra, passando in alto, verso destra (frazione 1 = semicerchio intero).</summary>
    private static void Draw(DrawingContext context, Point c, double r, double fraction, IBrush? brush, double thickness)
    {
        if (brush is null) return;
        double end = Math.PI - fraction * Math.PI;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(c.X - r, c.Y), isFilled: false);
            g.ArcTo(new Point(c.X + r * Math.Cos(end), c.Y - r * Math.Sin(end)), new Size(r, r), 0,
                isLargeArc: false, SweepDirection.Clockwise);
            g.EndFigure(isClosed: false);
        }
        context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Round), geometry);
    }
}

/// <summary>Barra orizzontale arrotondata per le percentuali della dashboard.</summary>
public sealed class BarMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<BarMeter, double>(nameof(Value));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<BarMeter, IBrush?>(nameof(Fill), Brushes.DodgerBlue);
    public static readonly StyledProperty<IBrush?> TrackProperty =
        AvaloniaProperty.Register<BarMeter, IBrush?>(nameof(Track), new SolidColorBrush(Color.FromRgb(229, 231, 235)));

    static BarMeter() => AffectsRender<BarMeter>(ValueProperty, FillProperty, TrackProperty);

    /// <summary>Da 0 a 1.</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 100, 10);

    public override void Render(DrawingContext context)
    {
        double h = Bounds.Height, radius = h / 2;
        context.DrawRectangle(Track, null, new Rect(0, 0, Bounds.Width, h), radius, radius);
        double w = Bounds.Width * Math.Clamp(Value, 0, 1);
        // Anche una quota minima resta visibile come un pallino.
        if (Value > 0) context.DrawRectangle(Fill, null, new Rect(0, 0, Math.Max(w, h), h), radius, radius);
    }
}
