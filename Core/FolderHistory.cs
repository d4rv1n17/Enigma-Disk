namespace EnigmaDisk.Core;

/// <summary>Размер конкретной папки по всем снимкам диска — для графика истории.</summary>
public static class FolderHistory
{
    public sealed class Point
    {
        public DateTime Time { get; init; }
        public long Size { get; init; }
    }

    /// <summary>
    /// Проходит по всем снимкам диска и возвращает размер path в каждом.
    /// Отсутствующая в снимке папка считается нулём.
    /// </summary>
    public static List<Point> Build(string drive, string path)
    {
        var store = AppState.Store;
        var points = new List<Point>();
        foreach (var info in store.HistoryFor(drive))
        {
            var snap = store.Load(info);
            if (snap == null) continue;
            points.Add(new Point { Time = info.Time, Size = snap.SizeOf(path) });
        }
        return points;
    }
}
