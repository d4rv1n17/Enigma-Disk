using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace EnigmaDisk.Controls;

/// <summary>Кольцевой индикатор заполненности диска с процентом в центре.</summary>
public sealed class Ring : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(Ring),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(Ring),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x00)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(Ring),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    private static readonly Brush TrackBrush = Frozen(Color.FromRgb(0x2C, 0x2C, 0x2C));
    private static readonly Brush TextBrush = Frozen(Color.FromRgb(0xF2, 0xF2, 0xF2));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x8E, 0x8E, 0x8E));
    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        double thick = size * 0.11;
        double r = (size - thick) / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);

        dc.DrawEllipse(null, new Pen(TrackBrush, thick), c, r, r);

        double f = Math.Clamp(double.IsNaN(Fraction) ? 0 : Fraction, 0, 0.9999);
        if (f > 0)
        {
            double a = f * 2 * Math.PI;
            var start = new Point(c.X, c.Y - r);
            var end = new Point(c.X + r * Math.Sin(a), c.Y - r * Math.Cos(a));
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(r, r), 0, f > 0.5, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(null, new Pen(Fill, thick) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, g);
        }

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var face = new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var pct = new FormattedText($"{Fraction * 100:0}%", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size * 0.22, TextBrush, dpi);
        double y = c.Y - pct.Height / 2;
        if (!string.IsNullOrEmpty(Caption))
        {
            var cap = new FormattedText(Caption, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size * 0.11, MutedBrush, dpi);
            y = c.Y - (pct.Height + cap.Height - 4) / 2;
            dc.DrawText(cap, new Point(c.X - cap.Width / 2, y + pct.Height - 4));
        }
        dc.DrawText(pct, new Point(c.X - pct.Width / 2, y));
    }
}
