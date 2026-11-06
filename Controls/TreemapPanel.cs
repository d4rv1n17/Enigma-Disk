using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EnigmaDisk.Core;

namespace EnigmaDisk.Controls;

/// <summary>
/// Панель treemap: плитки папок/файлов, двойной клик — внутрь, Esc — назад.
/// Папки нейтрально-серые, файлы тёплые (акцентные), при наведении плитка светлеет и обводится.
/// </summary>
public sealed class TreemapPanel : FrameworkElement
{
    private List<Treemap.Tile>? _tiles;
    private Treemap.Tile? _hover;
    private Treemap.Tile? _pressed;

    public event Action<string, bool>? TileClick;       // (path, isFile)
    public event Action<string>? TileOpen;              // двойной клик — зайти внутрь

    public TreemapPanel()
    {
        // тексты рисуем сами — перерисовываемся при смене языка
        Loc.Changed += () => Dispatcher.Invoke(InvalidateVisual);
    }

    private static readonly Typeface Face = new(
        new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"),
        FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private static Brush Freeze(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    private static readonly Brush FolderFill = Freeze("#262626");
    private static readonly Brush FolderHover = Freeze("#3A3A3A");
    private static readonly Brush FileFill = Freeze("#4A3F1D");
    private static readonly Brush FileHover = Freeze("#66552a");
    private static readonly Brush GapBrush = Freeze("#181818");
    private static readonly Brush HoverStroke = Freeze("#FFD200");
    private static readonly Brush FolderStroke = Freeze("#303030");
    private static readonly Brush ParentFill = Freeze("#2E2E2E");
    private static readonly Brush ParentStroke = Freeze("#3A3A3A");
    private static readonly Brush Text = Freeze("#F2F2F2");
    private static readonly Brush Dim = Freeze("#A6A6A6");
    private static readonly Brush SmallText = Freeze("#D8D8D8");
    private static readonly Brush IconDim = Freeze("#8A8A8A");
    private static readonly Brush HoverBadge = Freeze("#111111");

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, 200);

    public void SetTiles(List<Treemap.Tile> tiles)
    {
        _tiles = tiles;
        _hover = null;
        _pressed = null;
        InvalidateVisual();
    }

    private static string ClipText(string name, double width, double size)
    {
        if (width >= name.Length * size * 0.62) return name;
        while (name.Length > 1 && name.Length * size * 0.62 > width) name = name[..^1];
        return name + "…";
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (_tiles == null || ActualWidth < 2) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var hoverPen = new Pen(HoverStroke, 2);
        var kidsPen = new Pen(FolderStroke, 1);

        foreach (var t in _tiles)
        {
            bool isHover = _hover == t;
            // крупная папка с показанным содержимым: шапка + вложенные плитки
            if (t.Kids is { Count: > 0 })
            {
                DrawParent(dc, t, isHover, hoverPen, dpi, nested: false);
                continue;
            }

            DrawTile(dc, t, isHover, hoverPen, dpi);
        }
    }

    /// <summary>Обычная плитка: рамка, заливка, текст (крупная / средняя / узкая).</summary>
    private void DrawTile(DrawingContext dc, Treemap.Tile t, bool isHover, Pen hoverPen, double dpi)
    {
        // отступ между плитками: 1px со всех сторон, чтобы сетка была ровной
        var r = new Rect(t.R.X + 1, t.R.Y + 1, Math.Max(1, t.R.W - 2), Math.Max(1, t.R.H - 2));
        double radius = Math.Min(6, Math.Min(r.Width, r.Height) / 4);

        var fill = t.IsFile ? (isHover ? FileHover : FileFill) : (isHover ? FolderHover : FolderFill);
        // тонкая рамка у плиток папок: отделяет соседей без «прожилок»
        var stroke = t.IsFile ? null : (isHover ? null : new Pen(FolderStroke, 1));
        dc.DrawRoundedRectangle(fill, stroke, r, radius, radius);
        if (isHover)
            dc.DrawRoundedRectangle(null, hoverPen, r, radius, radius);

        if (r.Width < 30 || r.Height < 14) return;

        // крупная плитка: имя, размер
        if (r.Height >= 44 && r.Width >= 60)
        {
            double x = r.X + 9;
            var ftName = new FormattedText(ClipText(t.Name, r.Width - 18, 13), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 13, Text, dpi);
            var ftSize = new FormattedText(t.SizeText, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 11, Dim, dpi);
            dc.DrawText(ftName, new Point(x, r.Y + 6));
            dc.DrawText(ftSize, new Point(x, r.Y + 6 + ftName.Height));
            // бейдж «открыть» у папок с детьми при наведении
            if (isHover && t.CanOpen)
            {
                var open = new FormattedText(Loc.T("tm.badge"), System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Face, 10, IconDim, dpi);
                dc.DrawRoundedRectangle(HoverBadge, null, new Rect(x, r.Bottom - open.Height - 9, open.Width + 12, open.Height + 5), 4, 4);
                dc.DrawText(open, new Point(x + 6, r.Bottom - open.Height - 7));
            }
        }
        // средняя плитка: имя + размер в две строки без иконки
        else if (r.Height >= 30)
        {
            var ftName = new FormattedText(ClipText(t.Name, r.Width - 12, 12), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 12, Text, dpi);
            var ftSize = new FormattedText(t.SizeText, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 10, Dim, dpi);
            dc.DrawText(ftName, new Point(r.X + 6, r.Y + 4));
            dc.DrawText(ftSize, new Point(r.X + 6, r.Y + 4 + ftName.Height));
        }
        // узкая плитка: одна строка по центру
        else
        {
            var ft = new FormattedText(ClipText(t.Name, Math.Max(1, r.Width - 10), 11), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 11, SmallText, dpi);
            dc.DrawText(ft, new Point(r.X + 5, r.Y + (r.Height - ft.Height) / 2));
        }
    }

    /// <summary>Раскрываенная крупная папка: шапка с именем и размером + вложенные плитки содержимого.
    /// nested=true — это второй уровень: шапка тоньше, текст мельче.</summary>
    private void DrawParent(DrawingContext dc, Treemap.Tile t, bool isHover, Pen hoverPen, double dpi, bool nested)
    {
        var r = new Rect(t.R.X + 1, t.R.Y + 1, Math.Max(1, t.R.W - 2), Math.Max(1, t.R.H - 2));
        double radius = Math.Min(8, Math.Min(r.Width, r.Height) / 4);
        double headerH = nested ? 20 : 26;
        var kids = t.Kids!;

        // корпус: чуть светлее обычной папки, чтобы читалась как «контейнер»
        dc.DrawRoundedRectangle(isHover ? FolderHover : ParentFill, isHover ? hoverPen : new Pen(ParentStroke, 1), r, radius, radius);

        // шапка: имя слева, размер справа
        double nameSize = nested ? 10 : 12, sizeSize = nested ? 9 : 10;
        var ftName = new FormattedText(ClipText(t.Name, r.Width - 80, nameSize), System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Face, nameSize, Text, dpi);
        var ftSize = new FormattedText(t.SizeText, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Face, sizeSize, IconDim, dpi);
        dc.DrawText(ftName, new Point(r.X + (nested ? 6 : 8), r.Y + (headerH - ftName.Height) / 2));
        dc.DrawText(ftSize, new Point(r.Right - ftSize.Width - (nested ? 6 : 8), r.Y + (headerH - ftSize.Height) / 2));

        // вложенные плитки: поле внутри корпуса, 1px зазор
        double ix = r.X + 1, iy = r.Y + headerH, iw = r.Width - 2, ih = r.Height - headerH - 1;
        dc.DrawRectangle(GapBrush, null, new Rect(ix, iy, iw, ih));

        foreach (var k in kids)
        {
            // вложенная папка со своим содержимым — рисуем как мини-контейнер
            if (k.Kids is { Count: > 0 })
            {
                DrawParent(dc, k, k == _hover, hoverPen, dpi, nested: true);
                continue;
            }
            DrawKid(dc, k, k == _hover, hoverPen, dpi);
        }

        // бейдж «открыть» — в шапку, при наведении на корпус
        if (isHover && t.CanOpen)
        {
            var open = new FormattedText(Loc.T("tm.badge"), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 10, IconDim, dpi);
            double bw = open.Width + 12, bh = open.Height + 5;
            double bx = r.Right - bw - 8, by = r.Y + (headerH - bh) / 2;
            dc.DrawRoundedRectangle(HoverBadge, null, new Rect(bx, by, bw, bh), 4, 4);
            dc.DrawText(open, new Point(bx + 6, by + 2.5));
        }
    }

    /// <summary>Плитка внутри раскрытой папки: компактный текст.</summary>
    private void DrawKid(DrawingContext dc, Treemap.Tile k, bool kh, Pen hoverPen, double dpi)
    {
        var kr = new Rect(k.R.X + 1, k.R.Y + 1, Math.Max(1, k.R.W - 2), Math.Max(1, k.R.H - 2));
        double krad = Math.Min(4, Math.Min(kr.Width, kr.Height) / 4);
        var fill = k.IsFile ? (kh ? FileHover : FileFill) : (kh ? FolderHover : FolderFill);
        dc.DrawRoundedRectangle(fill, kh ? null : new Pen(FolderStroke, 1), kr, krad, krad);
        if (kh)
            dc.DrawRoundedRectangle(null, hoverPen, kr, krad, krad);

        if (kr.Width < 26 || kr.Height < 11) return;
        if (kr.Height >= 26 && kr.Width >= 44)
        {
            var ftKidName = new FormattedText(ClipText(k.Name, kr.Width - 10, 11), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 11, SmallText, dpi);
            var ftKidSize = new FormattedText(k.SizeText, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 9, IconDim, dpi);
            dc.DrawText(ftKidName, new Point(kr.X + 5, kr.Y + 3));
            dc.DrawText(ftKidSize, new Point(kr.X + 5, kr.Y + 3 + ftKidName.Height));
        }
        else
        {
            var ft = new FormattedText(ClipText(k.Name, Math.Max(1, kr.Width - 8), 10), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Face, 10, SmallText, dpi);
            dc.DrawText(ft, new Point(kr.X + 4, kr.Y + (kr.Height - ft.Height) / 2));
        }
    }

    /// <summary>Hit-test изнутри наружу: сначала дети раскрытых папок (глубже — точнее), потом сами плитки.</summary>
    private Treemap.Tile? HitTest(Point p)
    {
        if (_tiles == null) return null;
        foreach (var t in _tiles)
        {
            var hit = HitNested(t, p);
            if (hit != null) return hit;
        }
        foreach (var t in _tiles)
            if (t.R.W > 2 && t.R.H > 2 && t.R.X <= p.X && p.X <= t.R.X + t.R.W && t.R.Y <= p.Y && p.Y <= t.R.Y + t.R.H)
                return t;
        return null;
    }

    private static Treemap.Tile? HitNested(Treemap.Tile t, Point p)
    {
        if (t.Kids == null) return null;
        foreach (var k in t.Kids)
        {
            var deep = HitNested(k, p);
            if (deep != null) return deep;
            if (k.R.W > 0 && k.R.H > 0 && k.R.X <= p.X && p.X <= k.R.X + k.R.W && k.R.Y <= p.Y && p.Y <= k.R.Y + k.R.H)
                return k;
        }
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var t = HitTest(e.GetPosition(this));
        if (t != _hover) { _hover = t; InvalidateVisual(); }
        if (t != null)
        {
            // подсказка: имя и понятный размер, а не сырой путь
            ToolTip = $"{t.Name}\n{t.SizeText}" + (t.CanOpen ? "\n↔ " + Loc.T("tm.openHint") : "");
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _pressed = null;
        _hover = null; InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _pressed = HitTest(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var t = HitTest(e.GetPosition(this));
        var pressed = _pressed;
        _pressed = null;
        if (t == null || t != pressed) return;          // клик засчитываем только если нажали и отпустили на той же плитке
        if (e.ClickCount >= 2)
        {
            if (t.CanOpen) TileOpen?.Invoke(t.Path);
        }
        else TileClick?.Invoke(t.Path, t.IsFile);
    }
}
