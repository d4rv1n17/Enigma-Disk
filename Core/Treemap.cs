namespace EnigmaDisk.Core;

/// <summary>
/// Раскладка treemap (алгоритм squarified): крупные прямоугольники располагаются так,
/// чтобы их соотношение сторон было ближе к квадрату.
/// </summary>
public static class Treemap
{
    public sealed class Tile
    {
        public string Path { get; init; } = "";
        public string Name { get; init; } = "";
        public long Size { get; init; }
        public Rect R { get; set; } = new(0, 0, 0, 0);
        public bool IsFile { get; init; }
        /// <summary>Есть ли внутри дети (можно зайти кликом).</summary>
        public bool CanOpen { get; set; }
        public double Fraction { get; set; }
        /// <summary>Уровень вложенности: 0 — верх карты, 1..2 — содержимое крупной папки.</summary>
        public int Depth { get; set; }
        /// <summary>Раскрываенные дети (содержимое крупной папки), уже разложенные внутри R.</summary>
        public List<Tile>? Kids { get; set; }
        public string SizeText => Format.Size(Size);
    }

    public sealed record Rect(double X, double Y, double W, double H);

    /// <summary>Возвращает плитки для списка (path, size) внутри прямоугольника w×h.</summary>
    public static List<Tile> Layout(IReadOnlyList<(string path, string name, long size, bool isFile)> items,
        double w, double h)
    {
        var sorted = items.Where(i => i.size > 0).OrderByDescending(i => i.size).ToList();
        if (sorted.Count == 0 || w <= 0 || h <= 0) return new();

        long total = sorted.Sum(i => i.size);
        long remaining = total;                 // сумма ещё не размещённых — от неё считается масштаб
        var result = new List<Tile>();
        double left = 0, top = 0, rw = w, rh = h;
        int i = 0;

        while (i < sorted.Count)
        {
            if (rw <= 0.5 || rh <= 0.5 || remaining <= 0) break;
            double area = rw * rh;
            // масштаб пикселей на размер: от ОСТАВШИХСя элементов — иначе ряды съедают
            // меньше площади, чем должны, и недостача копится в пустой угол карты
            double scale = area / remaining;
            // ряд кладём вдоль КОРОТКОЙ стороны: rw>rh → колонка слева (плитки сверху вниз),
            // rw<rh → полоса сверху (плитки слева направо). side — длина ряда.
            bool horizontal = rw < rh;
            double side = horizontal ? rw : rh;

            // набираем ряд, пока худшее соотношение сторон плитки улучшается
            int j = i;
            List<int> row = new();
            double rowArea = 0;
            double bestWorst = double.MaxValue;
            while (j < sorted.Count)
            {
                double a = sorted[j].size * scale;
                double newArea = rowArea + a;
                double thickness = newArea / Math.Max(side, 0.001);
                if (thickness > (horizontal ? rh : rw)) break;   // ряд не влезает по толщине
                double len = a / Math.Max(thickness, 0.001);
                double ar = Math.Max(len, thickness) / Math.Max(0.001, Math.Min(len, thickness));
                if (row.Count > 0 && ar > bestWorst) break;      // стало хуже — ряд закончен
                row.Add(j);
                rowArea = newArea;
                bestWorst = Math.Min(bestWorst, ar);
                j++;
            }
            if (row.Count == 0)
            {
                // один элемент не влезает — всё равно кладём, иначе цикл зависнет
                row.Add(i);
                rowArea = sorted[i].size * scale;
                j = i + 1;
            }

            double thick = Math.Min(rowArea / Math.Max(side, 0.001), horizontal ? rh : rw);
            double off = 0;
            long rowSize = 0;
            foreach (var idx in row) rowSize += sorted[idx].size;
            foreach (var idx in row)
            {
                // площадь плитки в пикселях: доля от size / remaining
                double a = sorted[idx].size * scale;
                double len = a / rowArea * side;
                Rect r = horizontal
                    ? new Rect(left + off, top, len, thick)      // полоса: плитки слева направо
                    : new Rect(left, top + off, thick, len);     // колонка: плитки сверху вниз
                off += len;
                result.Add(new Tile
                {
                    Path = sorted[idx].path,
                    Name = sorted[idx].name,
                    Size = sorted[idx].size,
                    IsFile = sorted[idx].isFile,
                    R = r,
                });
            }
            remaining -= rowSize;
            if (horizontal) { top += thick; rh -= thick; }
            else { left += thick; rw -= thick; }
            i = j;
        }

        foreach (var t in result) t.Fraction = total > 0 ? (double)t.Size / total : 0;
        return result;
    }

    /// <summary>
    /// Раскладка с вложенностью: у крупных папок (≥12% ширины и высоты, ≥3.5% площади)
    /// прямо на карте видно их содержимое, как в WinDirStat. Одинокие папки-посредники
    /// (Users → user → …) пропускаем, у очень крупных вложенных плиток показываем второй уровень.
    /// </summary>
    public static List<Tile> LayoutNested(IReadOnlyList<(string path, string name, long size, bool isFile)> items,
        double w, double h, Func<string, IReadOnlyList<(string path, string name, long size, bool isFile)>>? childrenOf)
    {
        var tiles = Layout(items, w, h);
        if (childrenOf == null || tiles.Count == 0) return tiles;
        foreach (var t in tiles)
        {
            if (t.IsFile) continue;
            // раскрываем только по-настоящему крупные плитки: внутри должно быть что-то видно
            if (t.R.W < w * 0.12 || t.R.H < h * 0.12 || t.R.W * t.R.H < w * h * 0.035) continue;
            Expand(t, t.R, 26, 4, childrenOf);
        }
        return tiles;
    }

    /// <summary>Показать содержимое плитки внутри её прямоугольника. depth — сколько уровней осталось раскрыть.</summary>
    private static void Expand(Tile t, Rect outer, double headerH, int depth,
        Func<string, IReadOnlyList<(string path, string name, long size, bool isFile)>> childrenOf)
    {
        var kids = childrenOf(t.Path);
        t.CanOpen = kids.Count > 0;
        // один ребёнок-папка — спускаемся к его детям, иначе внутри будет одна пустая плитка
        while (kids.Count == 1 && !kids[0].isFile)
            kids = childrenOf(kids[0].path);
        if (depth <= 0 || kids.Count < 2) return;
        // внутренняя область: минус шапка с именем
        double iw = outer.W - 2, ih = outer.H - headerH - 2;
        if (iw < 60 || ih < 40) return;
        var inner = Layout(kids, iw, ih);
        if (inner.Count == 0) return;
        foreach (var k in inner)
        {
            k.Depth = t.Depth + 1;
            k.R = new Rect(outer.X + 1 + k.R.X, outer.Y + headerH + 1 + k.R.Y, k.R.W, k.R.H);
            // достаточно крупная вложенная папка: показываем и её содержимое (следующий уровень)
            if (!k.IsFile && depth > 1 && k.R.W >= 140 && k.R.H >= 100)
                Expand(k, k.R, 20, depth - 1, childrenOf);
        }
        t.Kids = inner;
    }
}
