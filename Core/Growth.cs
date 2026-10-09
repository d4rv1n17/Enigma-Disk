namespace EnigmaDisk.Core;

public sealed class GrowthItem
{
    public string Path { get; init; } = "";
    public long Old { get; init; }
    public long New { get; init; }
    public long Delta => New - Old;
    public bool IsNew { get; init; }
    public bool IsGone { get; init; }
    public string Hint => Hints.Describe(Path);
    public bool Cleanable => Hints.IsCleanable(Path);
    public string HintDot => Hint.Length > 0 ? Hint + " · " : "";

    // для интерфейса
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\')) is { Length: > 0 } n ? n : Path;
    public string Parent => System.IO.Path.GetDirectoryName(Path.TrimEnd('\\')) ?? "";
    public double Fraction { get; set; }
    public string DeltaText => Format.Delta(Delta);
    public string FromTo => IsNew ? Loc.F("gr.new", Format.Size(New))
                          : IsGone ? Loc.F("gr.gone", Format.Size(Old))
                          : $"{Format.Size(Old)} → {Format.Size(New)}";
    public bool HasHint => Hint.Length > 0;
}

public sealed class GrowthResult
{
    public SnapshotInfo From { get; init; } = new();
    public SnapshotInfo To { get; init; } = new();
    public List<GrowthItem> Grew { get; init; } = new();
    public List<GrowthItem> Shrank { get; init; } = new();
    public long UsedDelta => To.UsedBytes - From.UsedBytes;
}

public static class Growth
{
    /// <summary>
    /// Сравнивает два снимка и возвращает «самые конкретные» изменения:
    /// папка попадает в список, только если её рост не объясняется одной дочерней папкой.
    /// Так вместо «Users +5 ГБ → user +5 ГБ → AppData +5 ГБ …» видна сама папка, которая растёт.
    /// </summary>
    public static GrowthResult Compare(Snapshot oldS, Snapshot newS, SnapshotInfo fromInfo, SnapshotInfo toInfo,
        long minDelta)
    {
        var union = new Dictionary<string, (long o, long n)>(StringComparer.OrdinalIgnoreCase);
        var newPaths = newS.Paths;
        for (int i = 0; i < newPaths.Length; i++) union[newPaths[i]] = (0, newS.Folders[i].S);
        var oldPaths = oldS.Paths;
        for (int i = 0; i < oldPaths.Length; i++)
        {
            var p = oldPaths[i];
            union[p] = union.TryGetValue(p, out var v) ? (oldS.Folders[i].S, v.n) : (oldS.Folders[i].S, 0);
        }

        // максимальный рост/уменьшение среди прямых детей
        var maxChildUp = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var maxChildDown = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        // сумма заметных изменений детей: если они сами попадут в список, родителя не дублируем
        var bigChildUp = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var bigChildDown = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, (o, n)) in union)
        {
            var parent = System.IO.Path.GetDirectoryName(path);
            if (parent == null) continue;
            var d = n - o;
            if (d > 0 && (!maxChildUp.TryGetValue(parent, out var u) || d > u)) maxChildUp[parent] = d;
            if (d < 0 && (!maxChildDown.TryGetValue(parent, out var w) || -d > w)) maxChildDown[parent] = -d;
            if (d >= minDelta) bigChildUp[parent] = bigChildUp.GetValueOrDefault(parent) + d;
            if (-d >= minDelta) bigChildDown[parent] = bigChildDown.GetValueOrDefault(parent) - d;
        }

        var oldIndex = oldS.Index;
        var newIndex = newS.Index;
        var grew = new List<GrowthItem>();
        var shrank = new List<GrowthItem>();

        foreach (var (path, (o, n)) in union)
        {
            var d = n - o;
            if (Math.Abs(d) < minDelta) continue;
            if (System.IO.Path.GetDirectoryName(path) == null) continue; // корень диска не показываем

            if (d > 0)
            {
                if (maxChildUp.GetValueOrDefault(path) >= d * 0.7) continue;
                if (bigChildUp.GetValueOrDefault(path) >= d * 0.5) continue;
            }
            else
            {
                if (maxChildDown.GetValueOrDefault(path) >= -d * 0.7) continue;
                if (bigChildDown.GetValueOrDefault(path) >= -d * 0.5) continue;
            }

            var item = new GrowthItem
            {
                Path = path, Old = o, New = n,
                IsNew = o == 0 && !oldIndex.ContainsKey(path),
                IsGone = !newIndex.ContainsKey(path) && n == 0 && !Directory.Exists(path),
            };
            (d > 0 ? grew : shrank).Add(item);
        }

        grew = grew.OrderByDescending(g => g.Delta).Take(300).ToList();
        shrank = shrank.OrderBy(g => g.Delta).Take(300).ToList();
        if (grew.Count > 0) { double m = grew[0].Delta; foreach (var g in grew) g.Fraction = g.Delta / m; }
        if (shrank.Count > 0) { double m = -shrank[0].Delta; foreach (var g in shrank) g.Fraction = -g.Delta / m; }

        return new GrowthResult { From = fromInfo, To = toInfo, Grew = grew, Shrank = shrank };
    }
}
