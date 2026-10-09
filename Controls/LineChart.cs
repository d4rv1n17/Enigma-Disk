using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EnigmaDisk.Core;

namespace EnigmaDisk.Controls;

/// <summary>Простой график занятого места во времени с подсказкой при наведении.</summary>
public sealed class LineChart : FrameworkElement
{
    private List<(DateTime t, long v)> _points = new();
    private int _hover = -1;

    private static readonly Brush Accent = Frozen(Color.FromRgb(0xFF, 0xD2, 0x00));
    private static readonly Brush Grid = Frozen(Color.FromRgb(0x26, 0x26, 0x26));
    private static readonly Brush Muted = Frozen(Color.FromRgb(0x8E, 0x8E, 0x8E));
    private static readonly Brush Text = Frozen(Color.FromRgb(0xF2, 0xF2, 0xF2));
    private static readonly Brush TipBg = Frozen(Color.FromRgb(0x2A, 0x2A, 0x2A));
    private static readonly Brush PanelBg = Frozen(Color.FromRgb(0x18, 0x18, 0x18));
    private static readonly Typeface Face = new("Segoe UI");

    private const double Left = 64, Right = 16, Top = 14, Bottom = 28;

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public void SetData(IEnumerable<(DateTime t, long v)> points)
    {
        _points = points.OrderBy(p => p.t).ToList();
        _hover = -1;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, 200);

    private (double min, double max) Range()
    {
        double min = _points.Min(p => p.v), max = _points.Max(p => p.v);
        double pad = Math.Max((max - min) * 0.15, 512.0 * 1024 * 1024);
        return (Math.Max(0, min - pad), max + pad);
    }

    private Point Pos(int i, double min, double max)
    {
        double w = ActualWidth - Left - Right, h = ActualHeight - Top - Bottom;
        var t0 = _points[0].t; var t1 = _points[^1].t;
        double span = (t1 - t0).TotalSeconds;
        double x = _points.Count == 1 || span <= 0 ? Left + w / 2 : Left + w * (_points[i].t - t0).TotalSeconds / span;
        double y = Top + h * (1 - (_points[i].v - min) / (max - min));
        return new Point(x, y);
    }

    private FormattedText Txt(string s, Brush b, double size = 11) =>
        new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Face, size, b,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (_points.Count == 0)
        {
            var e = Txt(Loc.T("side.noSnapshots"), Muted, 13);
            dc.DrawText(e, new Point((ActualWidth - e.Width) / 2, (ActualHeight - e.Height) / 2));
            return;
        }

        var (min, max) = Range();
        double h = ActualHeight - Top - Bottom;

        // сетка и подписи по оси Y: столько знаков после запятой, чтобы подписи различались
        double stepGb = (max - min) / 3.0 / (1024.0 * 1024 * 1024);
        int dec = stepGb >= 1 ? 0 : stepGb >= 0.1 ? 1 : 2;
        for (int i = 0; i <= 3; i++)
        {
            double y = Top + h * i / 3.0;
            dc.DrawRectangle(Grid, null, new Rect(Left, Math.Round(y), ActualWidth - Left - Right, 1));
            var v = max - (max - min) * i / 3.0;
            var ft = Txt(Format.Gb(v, dec), Muted);
            dc.DrawText(ft, new Point(Left - ft.Width - 10, y - ft.Height / 2));
        }

        var pts = Enumerable.Range(0, _points.Count).Select(i => Pos(i, min, max)).ToList();

        // заливка под линией
        if (pts.Count > 1)
        {
            var area = new StreamGeometry();
            using (var g = area.Open())
            {
                g.BeginFigure(new Point(pts[0].X, Top + h), true, true);
                foreach (var p in pts) g.LineTo(p, true, true);
                g.LineTo(new Point(pts[^1].X, Top + h), true, true);
            }
            area.Freeze();
            var fill = new LinearGradientBrush(Color.FromArgb(0x40, 0xFF, 0xD2, 0x00), Color.FromArgb(0x00, 0xFF, 0xD2, 0x00), 90);
            fill.Freeze();
            dc.DrawGeometry(fill, null, area);

            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(pts[0], false, false);
                foreach (var p in pts.Skip(1)) g.LineTo(p, true, true);
            }
            line.Freeze();
            var pen = new Pen(Accent, 2) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            dc.DrawGeometry(null, pen, line);
        }

        // точки (если их не слишком много)
        if (pts.Count <= 60)
            foreach (var p in pts) dc.DrawEllipse(PanelBg, new Pen(Accent, 2), p, 3.5, 3.5);

        // подписи по оси X: первая, средняя и последняя дата
        var idx = new SortedSet<int> { 0, _points.Count / 2, _points.Count - 1 };
        bool sameDay = (_points[^1].t - _points[0].t).TotalHours < 36;
        string fmt = sameDay ? "HH:mm" : Loc.En ? "MMM d" : "d MMM";
        double lastRight = -1;
        foreach (var i in idx)
        {
            var ft = Txt(_points[i].t.ToString(fmt, Loc.Culture), Muted);
            double x = Math.Clamp(pts[i].X - ft.Width / 2, Left - 10, ActualWidth - ft.Width);
            if (x < lastRight + 12) continue;
            dc.DrawText(ft, new Point(x, Top + h + 8));
            lastRight = x + ft.Width;
        }

        // подсказка
        if (_hover >= 0 && _hover < pts.Count)
        {
            var p = pts[_hover];
            dc.DrawRectangle(Grid, null, new Rect(p.X, Top, 1, h));
            dc.DrawEllipse(Accent, null, p, 5, 5);
            var a = Txt(Format.Size(_points[_hover].v), Text, 13);
            var b = Txt(_points[_hover].t.ToString(Loc.En ? "MMM d, HH:mm" : "d MMMM, HH:mm", Loc.Culture), Muted);
            double tw = Math.Max(a.Width, b.Width) + 20, th = a.Height + b.Height + 14;
            double tx = p.X + 12 + tw > ActualWidth ? p.X - 12 - tw : p.X + 12;
            double ty = Math.Clamp(p.Y - th / 2, 0, ActualHeight - th);
            dc.DrawRoundedRectangle(TipBg, null, new Rect(tx, ty, tw, th), 6, 6);
            dc.DrawText(a, new Point(tx + 10, ty + 6));
            dc.DrawText(b, new Point(tx + 10, ty + 8 + a.Height));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_points.Count == 0) return;
        var (min, max) = Range();
        var x = e.GetPosition(this).X;
        int best = -1; double bestD = double.MaxValue;
        for (int i = 0; i < _points.Count; i++)
        {
            double d = Math.Abs(Pos(i, min, max).X - x);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best != _hover) { _hover = best; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        InvalidateVisual();
    }
}
