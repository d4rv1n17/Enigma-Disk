using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EnigmaDisk.Core;

/// <summary>
/// Быстрый многопоточный обход диска. Не заходит в симлинки и junction-папки,
/// не считает облачные файлы OneDrive, которых нет на диске.
/// </summary>
public sealed class Scanner
{
    public const long KeepFolderBytes = 1L << 20;   // в снимок попадают папки от 1 МБ
    public const long BigFileBytes = 50L << 20;     // «крупные файлы» от 50 МБ
    private const int TopFilesCount = 300;
    private const int RecallOnOpen = 0x40000;
    private const int RecallOnDataAccess = 0x400000;

    // Жёсткие ссылки: в папке Windows один и тот же файл лежит сразу в WinSxS и System32.
    // Чтобы не считать его дважды, каждой ссылке достаётся своя доля размера (размер / число ссылок).
    private readonly string _windowsDir =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public uint CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation info);

    private static uint LinkCount(string path)
    {
        const uint readAttributes = 0x80, shareAll = 0x7, openExisting = 3;
        const uint backupSemantics = 0x02000000, openReparsePoint = 0x00200000;
        using var h = CreateFileW(@"\\?\" + path, readAttributes, shareAll, IntPtr.Zero, openExisting,
            backupSemantics | openReparsePoint, IntPtr.Zero);
        if (h.IsInvalid) return 1;
        return GetFileInformationByHandle(h, out var info) && info.NumberOfLinks > 1 ? info.NumberOfLinks : 1;
    }

    private long _files;
    private long _bytes;
    private volatile string _current = "";
    private readonly ConcurrentBag<FileEntry> _big = new();

    public long Files => Interlocked.Read(ref _files);
    public long Bytes => Interlocked.Read(ref _bytes);
    public string Current => _current;

    private sealed class Node
    {
        public string Name = "";
        public long Size;
        public int Files;
        public List<Node>? Children;
    }

    private readonly record struct Item(string Name, bool IsDir, long Length, FileAttributes Attr, DateTime Modified);

    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    public Snapshot Scan(string drive, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var root = new Node { Name = drive };
        ScanDir(drive, root, 0, ct);

        var di = new DriveInfo(drive);
        var snap = new Snapshot
        {
            Version = Snapshot.CurrentVersion,
            Drive = drive,
            Time = DateTime.Now,
            TotalBytes = di.TotalSize,
            FreeBytes = di.TotalFreeSpace,
            ScannedBytes = root.Size,
            FileCount = root.Files,
            DurationSec = sw.Elapsed.TotalSeconds,
        };

        // Плоский список в порядке обхода в ширину: родитель всегда раньше детей.
        snap.Folders.Add(new FolderEntry { P = -1, N = drive, S = root.Size, F = root.Files });
        var queue = new Queue<(Node node, int index)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (node, index) = queue.Dequeue();
            if (node.Children == null) continue;
            foreach (var c in node.Children)
            {
                if (c.Size < KeepFolderBytes) continue;
                snap.Folders.Add(new FolderEntry { P = index, N = c.Name, S = c.Size, F = c.Files });
                queue.Enqueue((c, snap.Folders.Count - 1));
            }
        }

        snap.TopFiles = _big.OrderByDescending(f => f.S).Take(TopFilesCount).ToList();
        return snap;
    }

    private void ScanDir(string path, Node node, int depth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _current = path;

        long size = 0;
        int files = 0;
        List<Node>? kids = null;
        bool inWindows = (path + "\\").StartsWith(_windowsDir, StringComparison.OrdinalIgnoreCase);

        try
        {
            var en = new FileSystemEnumerable<Item>(path,
                (ref FileSystemEntry e) => new Item(e.FileName.ToString(), e.IsDirectory, e.Length, e.Attributes,
                    e.LastWriteTimeUtc.LocalDateTime),
                Options);

            foreach (var it in en)
            {
                if (it.IsDir)
                {
                    // junction, симлинки и точки монтирования не обходим — иначе двойной счёт
                    if ((it.Attr & FileAttributes.ReparsePoint) != 0) continue;
                    (kids ??= new List<Node>()).Add(new Node { Name = it.Name });
                    continue;
                }

                files++;
                int a = (int)it.Attr;
                if ((a & (RecallOnOpen | RecallOnDataAccess)) != 0 || (it.Attr & FileAttributes.Offline) != 0)
                    continue; // файл только в облаке, место на диске не занимает

                if (inWindows && it.Length > 0)
                    size += it.Length / LinkCount(System.IO.Path.Combine(path, it.Name));
                else
                    size += it.Length;
                if (it.Length >= BigFileBytes)
                    _big.Add(new FileEntry { Path = System.IO.Path.Combine(path, it.Name), S = it.Length, Modified = it.Modified });
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
        catch (System.Security.SecurityException) { }

        Interlocked.Add(ref _files, files);
        Interlocked.Add(ref _bytes, size);
        node.Children = kids;

        if (kids != null)
        {
            if (depth < 3 && kids.Count > 1)
            {
                Parallel.ForEach(kids,
                    new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) },
                    k => ScanDir(System.IO.Path.Combine(path, k.Name), k, depth + 1, ct));
            }
            else
            {
                foreach (var k in kids) ScanDir(System.IO.Path.Combine(path, k.Name), k, depth + 1, ct);
            }

            foreach (var k in kids)
            {
                size += k.Size;
                files += k.Files;
            }
        }

        node.Size = size;
        node.Files = files;
    }
}
