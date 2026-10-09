using System.Windows;
using System.Windows.Media;

namespace EnigmaDisk.Controls;

/// <summary>Тонкая полоска-индикатор с закруглёнными краями.</summary>
public sealed class Bar : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(Bar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(Bar),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x00)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(Brush), typeof(Bar),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C)), FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush Track { get => (Brush)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    public Bar()
    {
        Height = 4;
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double r = h / 2;
        dc.DrawRoundedRectangle(Track, null, new Rect(0, 0, w, h), r, r);
        double f = Math.Clamp(double.IsNaN(Fraction) ? 0 : Fraction, 0, 1);
        if (f <= 0) return;
        double fw = Math.Max(h, w * f);
        dc.DrawRoundedRectangle(Fill, null, new Rect(0, 0, fw, h), r, r);
    }
}
