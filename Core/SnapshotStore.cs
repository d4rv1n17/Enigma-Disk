using System.IO.Compression;
using System.Text.Json;

namespace EnigmaDisk.Core;

/// <summary>
/// Хранит снимки в %LOCALAPPDATA%\EnigmaDisk\snapshots в виде сжатого JSON
/// и ведёт короткую историю (index.json) для графиков.
/// </summary>
public sealed class SnapshotStore
{
    public static string DataDir { get; } =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnigmaDisk");

    public static string SnapshotDir { get; } = System.IO.Path.Combine(DataDir, "snapshots");
    private static string IndexPath => System.IO.Path.Combine(DataDir, "index.json");
    private static string SettingsPath => System.IO.Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions JsonPretty = new() { WriteIndented = true };

    private readonly object _lock = new();
    private List<SnapshotInfo> _history = new();
    private readonly Dictionary<string, Snapshot> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SnapshotStore()
    {
        Directory.CreateDirectory(SnapshotDir);
        LoadIndex();
    }

    public IReadOnlyList<SnapshotInfo> History
    {
        get { lock (_lock) return _history.ToList(); }
    }

    public List<SnapshotInfo> HistoryFor(string drive)
    {
        lock (_lock)
            return _history.Where(h => h.Drive.Equals(drive, StringComparison.OrdinalIgnoreCase))
                           .OrderBy(h => h.Time).ToList();
    }

    public SnapshotInfo? Latest(string drive) => HistoryFor(drive).LastOrDefault();

    public void Save(Snapshot snap)
    {
        var letter = snap.Drive.TrimEnd('\\', ':');
        var file = $"{letter}_{snap.Time:yyyyMMdd_HHmmss}.json.gz";
        var full = System.IO.Path.Combine(SnapshotDir, file);

        using (var fs = File.Create(full))
        using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            JsonSerializer.Serialize(gz, snap, Json);

        lock (_lock)
        {
            _history.Add(new SnapshotInfo
            {
                Version = snap.Version, Drive = snap.Drive, Time = snap.Time, TotalBytes = snap.TotalBytes, FreeBytes = snap.FreeBytes,
                ScannedBytes = snap.ScannedBytes, FileCount = snap.FileCount, File = file,
            });
            _cache[file] = snap;
            SaveIndex();
        }
    }

    public Snapshot? Load(SnapshotInfo info)
    {
        lock (_lock)
            if (_cache.TryGetValue(info.File, out var cached)) return cached;

        var full = System.IO.Path.Combine(SnapshotDir, info.File);
        if (!File.Exists(full)) return null;
        try
        {
            using var fs = File.OpenRead(full);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            var snap = JsonSerializer.Deserialize<Snapshot>(gz, Json);
            if (snap == null) return null;
            lock (_lock)
            {
                // держим в памяти не больше 6 снимков
                if (_cache.Count >= 6) _cache.Remove(_cache.Keys.First());
                _cache[info.File] = snap;
            }
            return snap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Снимок, ближайший к моменту (target), но строго раньше latest.</summary>
    public SnapshotInfo? Baseline(string drive, DateTime target, SnapshotInfo latest)
    {
        var older = HistoryFor(drive).Where(h => h.Time < latest.Time && h.Version == latest.Version).ToList();
        if (older.Count == 0) return null;
        // ближайший снимок, сделанный не позже target; если таких нет — самый старый
        var before = older.Where(h => h.Time <= target).LastOrDefault();
        return before ?? older.First();
    }

    /// <summary>Удаляет снимки старше retentionDays, но всегда оставляет последний по каждому диску
    /// и прореживает старые: после 14 дней — по одному в день, после 60 — по одному в неделю.</summary>
    public void Prune(int retentionDays)
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var keep = new List<SnapshotInfo>();
            foreach (var group in _history.GroupBy(h => h.Drive, StringComparer.OrdinalIgnoreCase))
            {
                var list = group.OrderByDescending(h => h.Time).ToList();
                var seenBuckets = new HashSet<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    var h = list[i];
                    var age = (now - h.Time).TotalDays;
                    bool keepIt;
                    if (i == 0) keepIt = true;
                    else if (age > retentionDays) keepIt = false;
                    else if (age <= 14) keepIt = true;
                    else if (age <= 60) keepIt = seenBuckets.Add("d" + h.Time.ToString("yyyyMMdd"));
                    else keepIt = seenBuckets.Add("w" + h.Time.Year + "_" + System.Globalization.ISOWeek.GetWeekOfYear(h.Time));

                    if (keepIt) keep.Add(h);
                    else TryDelete(h.File);
                }
            }
            _history = keep.OrderBy(h => h.Time).ToList();
            SaveIndex();
        }
    }

    public void DeleteAll()
    {
        lock (_lock)
        {
            foreach (var h in _history) TryDelete(h.File);
            _history.Clear();
            _cache.Clear();
            SaveIndex();
        }
    }

    public long DiskUsage()
    {
        try { return new DirectoryInfo(SnapshotDir).EnumerateFiles().Sum(f => f.Length); }
        catch { return 0; }
    }

    private void TryDelete(string file)
    {
        try { File.Delete(System.IO.Path.Combine(SnapshotDir, file)); } catch { }
        _cache.Remove(file);
    }

    private void LoadIndex()
    {
        try
        {
            if (File.Exists(IndexPath))
                _history = JsonSerializer.Deserialize<List<SnapshotInfo>>(File.ReadAllText(IndexPath)) ?? new();
        }
        catch { _history = new(); }

        // убираем записи, файлы которых удалили вручную
        _history = _history.Where(h => File.Exists(System.IO.Path.Combine(SnapshotDir, h.File))).ToList();
    }

    private void SaveIndex()
    {
        try
        {
            var tmp = IndexPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_history, JsonPretty));
            File.Move(tmp, IndexPath, true);
        }
        catch { }
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, JsonPretty));
        }
        catch { }
    }
}
