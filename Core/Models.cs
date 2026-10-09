using System.Text.Json.Serialization;

namespace EnigmaDisk.Core;

/// <summary>Одна папка в снимке. Хранится плоским списком: P — индекс родителя.</summary>
public sealed class FolderEntry
{
    [JsonPropertyName("p")] public int P { get; set; }
    [JsonPropertyName("n")] public string N { get; set; } = "";
    [JsonPropertyName("s")] public long S { get; set; }
    [JsonPropertyName("f")] public int F { get; set; }
}

public sealed class FileEntry
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("s")] public long S { get; set; }
    [JsonPropertyName("t")] public DateTime Modified { get; set; }
}

/// <summary>Полный снимок одного диска.</summary>
public sealed class Snapshot
{
    /// <summary>2 — жёсткие ссылки в папке Windows делятся между ссылками, а не считаются дважды.</summary>
    public const int CurrentVersion = 2;
    public int Version { get; set; } = 1;
    public string Drive { get; set; } = "";
    public DateTime Time { get; set; }
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long ScannedBytes { get; set; }
    public long FileCount { get; set; }
    public double DurationSec { get; set; }
    public List<FolderEntry> Folders { get; set; } = new();
    public List<FileEntry> TopFiles { get; set; } = new();

    [JsonIgnore] public long UsedBytes => TotalBytes - FreeBytes;

    private string[]? _paths;
    private Dictionary<string, int>? _index;
    private List<int>[]? _children;

    /// <summary>Полные пути всех папок (индексы совпадают с Folders).</summary>
    [JsonIgnore]
    public string[] Paths
    {
        get
        {
            if (_paths != null) return _paths;
            var p = new string[Folders.Count];
            for (int i = 0; i < Folders.Count; i++)
            {
                var f = Folders[i];
                p[i] = f.P < 0 ? Drive : System.IO.Path.Combine(p[f.P], f.N);
            }
            return _paths = p;
        }
    }

    [JsonIgnore]
    public Dictionary<string, int> Index
    {
        get
        {
            if (_index != null) return _index;
            var d = new Dictionary<string, int>(Folders.Count, StringComparer.OrdinalIgnoreCase);
            var paths = Paths;
            for (int i = 0; i < paths.Length; i++) d[paths[i]] = i;
            return _index = d;
        }
    }

    public List<int> ChildrenOf(int index)
    {
        if (_children == null)
        {
            var c = new List<int>[Folders.Count];
            for (int i = 0; i < c.Length; i++) c[i] = new List<int>();
            for (int i = 0; i < Folders.Count; i++)
                if (Folders[i].P >= 0) c[Folders[i].P].Add(i);
            _children = c;
        }
        return _children[index];
    }

    public long SizeOf(string path) => Index.TryGetValue(path, out var i) ? Folders[i].S : 0;
}

/// <summary>Короткая запись о снимке для истории и графиков.</summary>
public sealed class SnapshotInfo
{
    /// <summary>Сравнивать можно только снимки одной версии подсчёта.</summary>
    public int Version { get; set; } = 1;
    public string Drive { get; set; } = "";
    public DateTime Time { get; set; }
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long ScannedBytes { get; set; }
    public long FileCount { get; set; }
    public string File { get; set; } = "";

    [JsonIgnore] public long UsedBytes => TotalBytes - FreeBytes;
}

public sealed class AppSettings
{
    /// <summary>"ru", "en" или пусто — как в Windows.</summary>
    public string Language { get; set; } = "";
    public List<string> AutoDrives { get; set; } = new();
    public int AutoHour { get; set; } = 13;
    public int RetentionDays { get; set; } = 180;
    public int MinGrowthMb { get; set; } = 20;
}
