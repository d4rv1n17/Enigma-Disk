using System.Security.Cryptography;

namespace EnigmaDisk.Core;

/// <summary>
/// Поиск дубликатов по последнему снимку: группа по размеру, затем частичный и полный хеш.
/// </summary>
public static class Duplicates
{
    public sealed class Progress
    {
        public string Phase { get; set; } = "";
        public string Current { get; set; } = "";
        public int Percent { get; set; }
    }

    /// <summary>Минимальный размер файла для поиска дубликатов.</summary>
    public const long MinBytes = 1L << 20;   // от 1 МБ — мелкие файлы не проверяем

    private sealed class Candidate
    {
        public string Path;
        public long Size;
        public byte[] Partial;
        public string Full = "";
        public Candidate(string p, long s, byte[] partial) { Path = p; Size = s; Partial = partial; }
    }

    public static DuplicateResult Find(Snapshot snap, Action<Progress>? report, CancellationToken ct)
    {
        var bySize = new Dictionary<long, List<string>>();
        var candidates = snap.DuplicateCandidates.Count > 0 ? snap.DuplicateCandidates : snap.TopFiles;
        foreach (var f in candidates)
        {
            if (f.S < MinBytes) continue;
            if (!File.Exists(f.Path)) continue;
            if (!bySize.TryGetValue(f.S, out var l)) bySize[f.S] = l = new();
            l.Add(f.Path);
        }

        var groups = new List<DuplicateGroup>();
        int total = bySize.Sum(kv => kv.Value.Count);
        int done = 0;

        var cands = new List<Candidate>();
        foreach (var kv in bySize)
        {
            if (kv.Value.Count < 2) { done += kv.Value.Count; continue; }
            foreach (var p in kv.Value)
            {
                ct.ThrowIfCancellationRequested();
                report?.Invoke(new Progress { Phase = "size", Current = p, Percent = total == 0 ? 0 : done * 100 / total });
                var partial = ReadPartial(p, 64 * 1024);
                if (partial == null) { done++; continue; }
                cands.Add(new Candidate(p, kv.Key, partial));
                done++;
            }
        }

        // группируем по (размер, частичный хеш)
        var sizePartial = new Dictionary<(long, string), List<Candidate>>();
        foreach (var c in cands)
        {
            var key = (c.Size, Convert.ToHexString(c.Partial));
            if (!sizePartial.TryGetValue(key, out var l)) sizePartial[key] = l = new();
            l.Add(c);
        }

        var fullHashed = new List<Candidate>();
        int hashTotal = sizePartial.Values.Where(l => l.Count > 1).Sum(l => l.Count);
        int hashDone = 0;
        foreach (var l in sizePartial.Values)
        {
            if (l.Count < 2) continue;
            foreach (var c in l)
            {
                ct.ThrowIfCancellationRequested();
                report?.Invoke(new Progress { Phase = "hash", Current = c.Path, Percent = hashTotal == 0 ? 0 : hashDone * 100 / hashTotal });
                c.Full = FullHash(c.Path);
                if (c.Full.Length > 0) fullHashed.Add(c);
                hashDone++;
            }
        }

        foreach (var g in fullHashed.GroupBy(c => (c.Size, c.Full)))
        {
            var list = g.Select(c => c.Path).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count < 2) continue;
            groups.Add(new DuplicateGroup
            {
                Hash = g.Key.Item2,
                Size = g.Key.Item1,
                Paths = list,
            });
        }

        groups = groups.OrderByDescending(g => g.Size * (g.Paths.Count - 1)).ToList();
        report?.Invoke(new Progress { Phase = "done", Current = "", Percent = 100 });
        return new DuplicateResult { Groups = groups };
    }

    private static byte[]? ReadPartial(string path, int bytes)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[bytes];
            int read = fs.Read(buf, 0, bytes);
            if (read < bytes)
            {
                var b2 = new byte[read];
                Array.Copy(buf, b2, read);
                return SHA256.HashData(b2);
            }
            return SHA256.HashData(buf);
        }
        catch { return null; }
    }

    private static string FullHash(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(fs));
        }
        catch { return ""; }
    }
}
