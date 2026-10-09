namespace EnigmaDisk.Core;

public enum Safety { Safe, Careful }

public sealed class CleanTarget
{
    public string Id { get; init; } = "";
    public string Title => Loc.T($"clean.{Id}.t");
    public string Description => Loc.T($"clean.{Id}.d");
    public Safety Safety { get; init; }
    public bool DefaultOn { get; init; }
    public bool NeedsAdmin { get; init; }
    public bool IsRecycleBin { get; init; }
    public double MinAgeHours { get; init; }
    public string[] Patterns { get; init; } = Array.Empty<string>();
}

public sealed class CleanResult
{
    public long Freed;
    public int Deleted;
    public int Skipped;
}

/// <summary>
/// Очистка только заранее известных кешей. Удаляются файлы внутри папок, сами папки-кеши остаются.
/// Занятые файлы (открытые программой) пропускаются. Ссылки и junction-папки не обходятся.
/// </summary>
public static class Cleaner
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string Win => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static List<CleanTarget> Targets() => new()
    {
        new()
        {
            Id = "usertemp", Safety = Safety.Safe, DefaultOn = true, MinAgeHours = 24,
            Patterns = new[] { System.IO.Path.GetTempPath() },
        },
        new()
        {
            Id = "browsers", Safety = Safety.Safe, DefaultOn = true,
            Patterns = new[]
            {
                Local + @"\Google\Chrome\User Data\*\Cache",
                Local + @"\Google\Chrome\User Data\*\Code Cache",
                Local + @"\Google\Chrome\User Data\*\GPUCache",
                Local + @"\Microsoft\Edge\User Data\*\Cache",
                Local + @"\Microsoft\Edge\User Data\*\Code Cache",
                Local + @"\Microsoft\Edge\User Data\*\GPUCache",
                Local + @"\Yandex\YandexBrowser\User Data\*\Cache",
                Local + @"\Yandex\YandexBrowser\User Data\*\Code Cache",
                Local + @"\Yandex\YandexBrowser\User Data\*\GPUCache",
                Local + @"\BraveSoftware\Brave-Browser\User Data\*\Cache",
                Local + @"\BraveSoftware\Brave-Browser\User Data\*\Code Cache",
                Local + @"\Opera Software\*\Cache",
                Roaming + @"\Opera Software\*\Code Cache",
                Local + @"\Mozilla\Firefox\Profiles\*\cache2",
            },
        },
        new()
        {
            Id = "apps", Safety = Safety.Safe, DefaultOn = true,
            Patterns = new[]
            {
                Roaming + @"\discord\Cache",
                Roaming + @"\discord\Code Cache",
                Roaming + @"\discord\GPUCache",
                Local + @"\Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default\Cache",
            },
        },
        new()
        {
            Id = "shaders", Safety = Safety.Careful, DefaultOn = false,
            Patterns = new[]
            {
                Local + @"\NVIDIA\DXCache", Local + @"\NVIDIA\GLCache", Local + @"\NVIDIA Corporation\NV_Cache",
                Local + @"\AMD\DxCache", Local + @"\AMD\DxcCache", Local + @"\AMD\GLCache", Local + @"\AMD\VkCache",
                Local + @"\Intel\ShaderCache", Local + @"\D3DSCache",
            },
        },
        new()
        {
            Id = "dumps", Safety = Safety.Safe, DefaultOn = true,
            Patterns = new[]
            {
                Local + @"\CrashDumps",
                Local + @"\Microsoft\Windows\WER\ReportArchive",
                Local + @"\Microsoft\Windows\WER\ReportQueue",
                ProgramData + @"\Microsoft\Windows\WER\ReportArchive",
                ProgramData + @"\Microsoft\Windows\WER\ReportQueue",
                Win + @"\Minidump",
            },
        },
        new()
        {
            Id = "wintemp", Safety = Safety.Safe, DefaultOn = true, MinAgeHours = 24,
            NeedsAdmin = true,
            Patterns = new[] { Win + @"\Temp" },
        },
        new()
        {
            Id = "updates", Safety = Safety.Safe, DefaultOn = false, NeedsAdmin = true,
            Patterns = new[]
            {
                Win + @"\SoftwareDistribution\Download",
                Win + @"\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache",
            },
        },
        new()
        {
            Id = "dev", Safety = Safety.Careful, DefaultOn = false,
            Patterns = new[]
            {
                Home + @"\.gradle\caches",
                Local + @"\npm-cache",
                Local + @"\pip\cache",
                Local + @"\Yarn\Cache",
                Home + @"\.nuget\packages",
                Local + @"\NuGet\v3-cache",
            },
        },
        new()
        {
            Id = "recycle", Safety = Safety.Careful, DefaultOn = false, IsRecycleBin = true,
        },
    };

    /// <summary>Разворачивает шаблоны вида ...\User Data\*\Cache в существующие папки.</summary>
    public static List<string> Expand(CleanTarget t)
    {
        var result = new List<string>();
        foreach (var pattern in t.Patterns)
        {
            var parts = pattern.TrimEnd('\\').Split('\\');
            var current = new List<string> { parts[0] + "\\" };
            for (int i = 1; i < parts.Length && current.Count > 0; i++)
            {
                var next = new List<string>();
                foreach (var dir in current)
                {
                    try
                    {
                        if (parts[i].Contains('*'))
                            next.AddRange(Directory.EnumerateDirectories(dir, parts[i]));
                        else
                        {
                            var p = System.IO.Path.Combine(dir, parts[i]);
                            if (Directory.Exists(p)) next.Add(p);
                        }
                    }
                    catch { }
                }
                current = next;
            }
            foreach (var c in current)
                if (!result.Contains(c, StringComparer.OrdinalIgnoreCase)) result.Add(c);
        }
        return result;
    }

    private static readonly EnumerationOptions Recurse = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public static long Measure(CleanTarget t, CancellationToken ct)
    {
        if (t.IsRecycleBin) return Native.RecycleBinSize().bytes;
        long total = 0;
        var cutoff = DateTime.UtcNow.AddHours(-t.MinAgeHours);
        foreach (var dir in Expand(t))
        {
            try
            {
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", Recurse))
                {
                    ct.ThrowIfCancellationRequested();
                    if (t.MinAgeHours > 0 && f.LastWriteTimeUtc > cutoff) continue;
                    total += f.Length;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
        return total;
    }

    public static CleanResult Clean(CleanTarget t, CancellationToken ct)
    {
        var r = new CleanResult();
        if (t.IsRecycleBin)
        {
            var size = Native.RecycleBinSize();
            if (Native.EmptyRecycleBin()) { r.Freed = size.bytes; r.Deleted = (int)size.items; }
            return r;
        }

        var cutoff = DateTime.UtcNow.AddHours(-t.MinAgeHours);
        foreach (var dir in Expand(t))
        {
            IEnumerable<FileInfo> files;
            try { files = new DirectoryInfo(dir).EnumerateFiles("*", Recurse).ToList(); }
            catch { continue; }

            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (t.MinAgeHours > 0 && f.LastWriteTimeUtc > cutoff) continue;
                    long len = f.Length;
                    if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes &= ~FileAttributes.ReadOnly;
                    f.Delete();
                    r.Freed += len;
                    r.Deleted++;
                }
                catch { r.Skipped++; }
            }
            RemoveEmptyDirs(dir, isRoot: true);
        }
        return r;
    }

    private static void RemoveEmptyDirs(string dir, bool isRoot)
    {
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var info = new DirectoryInfo(sub);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                RemoveEmptyDirs(sub, false);
            }
            if (!isRoot && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { }
    }
}
