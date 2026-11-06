using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EnigmaDisk.Core;

namespace EnigmaDisk.Controls;

/// <summary>Пончик-диаграмма категорий типов файлов с подсказкой при наведении.</summary>
public sealed class TypeDonut : FrameworkElement
{
    public sealed class Slice
    {
        public string Name { get; init; } = "";
        public long Size { get; init; }
        public int Count { get; init; }
        public string Color { get; init; } = "#94A3B8";
        public string SizeText => Format.Size(Size);
        public string CountText => Format.Count(Count);
    }

    private List<Slice> _slices = new();
    private List<double> _sweeps = new();   // углы срезов в градусах — ровно те же, что рисуются
    private int _hover = -1;

    public event Action<int>? SliceClick;

    public void SetData(IEnumerable<Slice> slices)
    {
        _slices = slices.OrderByDescending(s => s.Size).ToList();
        _hover = -1;
        // углы: крошечные срезы поднимаем до 0.5°, остальное пропорционально ужимаем, чтобы сумма была ровно 360°.
        // и рендер, и хит-тест пользуются одним и тем же списком — расхождений между нарисованным и находимым нет.
        long total = _slices.Sum(s => s.Size);
        var raw = _slices.Select(s => total > 0 ? (double)s.Size / total * 360 : 0).ToList();
        var floored = new bool[raw.Count];
        double sumFree = 0;
        int nFloor = 0;
        for (int i = 0; i < raw.Count; i++)
        {
            if (raw[i] < 0.5) { floored[i] = true; nFloor++; }
            else sumFree += raw[i];
        }
        double k = sumFree > 0 ? (360 - nFloor * 0.5) / sumFree : 0;
        _sweeps = raw.Select((r, i) => floored[i] ? 0.5 : r * k).ToList();
        InvalidateVisual();
    }

    private static Brush Freeze(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    private static Brush FreezeWithOpacity(string hex, double opacity)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) { Opacity = opacity };
        b.Freeze();
        return b;
    }
    private static readonly Brush Text = Freeze("#F2F2F2");
    private static readonly Brush SubText = Freeze("#8E8E8E");
    private static readonly Brush Panel = Freeze("#181818");
    private static readonly Brush LabelDark = Freeze("#141414");
    private static readonly Brush LabelLight = Freeze("#FFFFFF");
    private static readonly Typeface BigFace = new(new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface SmallFace = new(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface LabelFace = new(new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.DemiBold, FontStretches.Normal);

    private static double RelLuma(byte r, byte g, byte b)
    {
        double F(int v) { double d = v / 255.0; return d <= 0.03928 ? d / 12.92 : Math.Pow((d + 0.055) / 1.055, 2.4); }
        return 0.2126 * F(r) + 0.7152 * F(g) + 0.0722 * F(b);
    }

    /// <summary>Подпись на срезе: тёмная или белая — что контрастнее по WCAG.</summary>
    private static Brush LabelBrush(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        double l = RelLuma(c.R, c.G, c.B);
        double cDark = (l + 0.05) / (0.0088 + 0.05);   // против #141414
        double cLight = (1.0 + 0.05) / (l + 0.05);     // против белого
        return cDark >= cLight ? LabelDark : LabelLight;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // пончик всегда квадрат: желаемая высота равна ширине (зажатой в 40..300),
        // иначе желаемая высота расходится со слотом и кольцо уезжает/обрезается
        double s = double.IsInfinity(availableSize.Width) ? 300 : Math.Max(40, Math.Min(300, availableSize.Width));
        return new Size(s, s);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size < 40 || _slices.Count == 0) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // центр по фактической высоте, радиус по квадратной стороне: кольцо целиком внутри бокса
        double cx = ActualWidth / 2, cy = ActualHeight / 2;
        double r = size / 2 - 10;
        double thick = r * 0.36;
        double rMid = r - thick / 2;
        long total = _slices.Sum(s => s.Size);
        double start = -90;

        for (int i = 0; i < _slices.Count; i++)
        {
            var s = _slices[i];
            double sweep = _sweeps[i];
            double frac = total > 0 ? (double)s.Size / total : 0;
            bool isHover = i == _hover;

            // зазор между срезами задаём уменьшением угла, а не обводкой: перо 2.5px съедало тонкие
            // срезы целиком. ~1.2px с каждой стороны, но не больше 1/5 среза — даже 0.7° остаётся видимой жилкой
            double inset = Math.Min(1.2 / rMid, sweep / 5);
            double a0 = (start + inset) * Math.PI / 180;
            double a1 = (start + sweep - inset) * Math.PI / 180;
            start += sweep;
            if (a1 - a0 < 0.0005) continue;

            // наведение: срез плавно выезжает наружу по своему центру, толщина чуть растёт
            double push = isHover ? 5 : 0;
            double mid = (a0 + a1) / 2;
            double ox = push * Math.Cos(mid), oy = push * Math.Sin(mid);
            double rOut = r + (isHover ? 2 : 0);
            double rIn = rOut - (thick + (isHover ? 3 : 0));

            var p0 = new Point(cx + ox + rOut * Math.Cos(a0), cy + oy + rOut * Math.Sin(a0));
            var p1 = new Point(cx + ox + rOut * Math.Cos(a1), cy + oy + rOut * Math.Sin(a1));
            var p2 = new Point(cx + ox + rIn * Math.Cos(a1), cy + oy + rIn * Math.Sin(a1));
            var p3 = new Point(cx + ox + rIn * Math.Cos(a0), cy + oy + rIn * Math.Sin(a0));
            bool big = a1 - a0 > Math.PI;

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(p0, true, true);
                g.ArcTo(p1, new Size(rOut, rOut), 0, big, SweepDirection.Clockwise, true, false);
                g.LineTo(p2, true, false);
                g.ArcTo(p3, new Size(rIn, rIn), 0, big, SweepDirection.Counterclockwise, true, false);
            }
            geo.Freeze();
            // замороженную кисть менять нельзя: создаём новую с нужной прозрачностью
            var brush = isHover ? Freeze(s.Color) : FreezeWithOpacity(s.Color, 0.88);
            dc.DrawGeometry(brush, null, geo);

            // процент прямо на крупном срезе — в том же формате, что и в легенде
            if (frac >= 0.055 && !isHover)
            {
                double rm = (rOut + rIn) / 2;
                var pt = new Point(cx + rm * Math.Cos(mid), cy + rm * Math.Sin(mid));
                var ftPct = new FormattedText($"{(frac * 100).ToString("0.#", Loc.Culture)}%", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, LabelFace, size * 0.052, LabelBrush(s.Color), dpi);
                dc.DrawText(ftPct, new Point(pt.X - ftPct.Width / 2, pt.Y - ftPct.Height / 2));
            }
        }

        // центр: всего — блок «размер + подпись» центрируем целиком, а не вокруг cy по отдельности
        var ftTotal = new FormattedText(Format.Size(total), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, BigFace, size * 0.115, Text, dpi);
        var ftSub = new FormattedText(Loc.T("ty.total"), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, SmallFace, size * 0.044, SubText, dpi);
        double y0 = cy - (ftTotal.Height + 3 + ftSub.Height) / 2;
        dc.DrawText(ftTotal, new Point(cx - ftTotal.Width / 2, y0));
        dc.DrawText(ftSub, new Point(cx - ftSub.Width / 2, y0 + ftTotal.Height + 3));

        // подсказка при наведении
        if (_hover >= 0 && _hover < _slices.Count)
        {
            var s = _slices[_hover];
            var ftName = new FormattedText($"{s.Name} · {s.SizeText}", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, SmallFace, 13, Text, dpi);
            double tw = ftName.Width + 20;
            double tx = Math.Clamp(cx - tw / 2, 0, ActualWidth - tw);
            var bg = new Rect(tx, y0 - ftName.Height - 16, tw, ftName.Height + 10);
            dc.DrawRoundedRectangle(Panel, new Pen(Freeze("#333333"), 1), bg, 7, 7);
            dc.DrawText(ftName, new Point(tx + 10, bg.Y + 5));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_slices.Count == 0) return;
        var p = e.GetPosition(this);
        double cx = ActualWidth / 2, cy = ActualHeight / 2;
        double r = Math.Min(ActualWidth, ActualHeight) / 2 - 10, thick = r * 0.36;
        double d = Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2));
        // внешнюю границу держим на 7px шире нарисованной: наведённый срез выезжает на 5+2px,
        // иначе у кромки hover «спадает» и срез дёргается
        int hit = -1;
        if (d >= r - thick && d <= r + 7)
        {
            double ang = Math.Atan2(p.Y - cy, p.X - cx) * 180 / Math.PI + 90;
            if (ang < 0) ang += 360;
            double a = 0;
            for (int i = 0; i < _slices.Count; i++)
            {
                if (ang >= a && ang <= a + _sweeps[i]) { hit = i; break; }
                a += _sweeps[i];
            }
        }
        if (hit != _hover) { _hover = hit; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1; InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_hover >= 0) SliceClick?.Invoke(_hover);
    }
}
