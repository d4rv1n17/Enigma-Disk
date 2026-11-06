namespace EnigmaDisk.Core;

/// <summary>Поиск папок и файлов в снимке.</summary>
public static class Search
{
    public sealed class Hit
    {
        public string Path { get; init; } = "";
        public string Name { get; init; } = "";
        public string Folder => System.IO.Path.GetDirectoryName(Path.TrimEnd('\\')) ?? "";
        public long Size { get; init; }
        public bool IsFile { get; init; }
        public string SizeText => Format.Size(Size);
        public string Icon => IsFile ? "" : "";
    }

    /// <summary>Ищет папки (всё дерево) и крупные файлы по подстроке, без учёта регистра.</summary>
    public static List<Hit> Find(Snapshot snap, string query, int limit = 120)
    {
        var q = query.Trim();
        if (q.Length == 0) return new();
        var hits = new List<Hit>();

        var paths = snap.Paths;
        for (int i = 0; i < paths.Length && hits.Count < limit; i++)
        {
            var f = snap.Folders[i];
            if (f.P < 0) continue;
            if (paths[i].Contains(q, StringComparison.OrdinalIgnoreCase))
                hits.Add(new Hit { Path = paths[i], Name = f.N, Size = f.S, IsFile = false });
        }
        foreach (var f in snap.TopFiles)
        {
            if (hits.Count >= limit) break;
            if (f.Path.Contains(q, StringComparison.OrdinalIgnoreCase))
                hits.Add(new Hit { Path = f.Path, Name = System.IO.Path.GetFileName(f.Path), Size = f.S, IsFile = true });
        }
        return hits;
    }
}
