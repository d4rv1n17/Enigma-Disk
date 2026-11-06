namespace EnigmaDisk.Core;

/// <summary>Общее состояние приложения: хранилище снимков, настройки и текущее сканирование.</summary>
public static class AppState
{
    public static SnapshotStore Store { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;

    public static Scanner? ActiveScanner { get; private set; }
    public static string? ActiveDrive { get; private set; }
    private static CancellationTokenSource? _cts;
    private static int _scanGate;

    /// <summary>Появились новые снимки или изменились настройки.</summary>
    public static event Action? DataChanged;
    /// <summary>Сканирование началось или закончилось.</summary>
    public static event Action? ScanStateChanged;
    /// <summary>Запрос на переход к папке на вкладке «Файлы».</summary>
    public static event Action<string>? NavigateToFolder;
    /// <summary>Запрос на переход к папке на карте диска.</summary>
    public static event Action<string>? OpenFolder;

    public static void Init()
    {
        Store = new SnapshotStore();
        Settings = SnapshotStore.LoadSettings();
    }

    public static void SaveSettings()
    {
        SnapshotStore.SaveSettings(Settings);
        DataChanged?.Invoke();
    }

    public static void RaiseDataChanged() => DataChanged?.Invoke();
    public static void OpenInFiles(string path) => NavigateToFolder?.Invoke(path);
    public static void OpenOnMap(string path) => OpenFolder?.Invoke(path);

    public static List<DriveInfo> FixedDrives()
    {
        var list = new List<DriveInfo>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if ((d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable) && d.IsReady)
                    list.Add(d);
            }
            catch { }
        }
        return list;
    }

    public static bool IsScanning => ActiveScanner != null;

    /// <summary>Сканирует один диск в фоне. Возвращает false, если отменено или уже идёт сканирование.</summary>
    public static async Task<bool> ScanAsync(string drive)
    {
        if (Interlocked.CompareExchange(ref _scanGate, 1, 0) != 0) return false;
        var scanner = new Scanner(Settings.Exclusions, Settings.UseDiskSize);
        ActiveScanner = scanner;
        ActiveDrive = drive;
        _cts = new CancellationTokenSource();
        ScanStateChanged?.Invoke();
        try
        {
            var token = _cts.Token;
            var snap = await Task.Run(() => scanner.Scan(drive, token), token);
            await Task.Run(() =>
            {
                Store.Save(snap);
                Store.Prune(Settings.RetentionDays);
            });
            NotifyCheck(snap);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Notifier.Raise(new Notifier.Notification
            {
                Kind = "warn",
                Title = Loc.T("scan.failed"),
                Body = ex.Message,
            });
            return false;
        }
        finally
        {
            ActiveScanner = null;
            ActiveDrive = null;
            _cts.Dispose();
            _cts = null;
            Interlocked.Exchange(ref _scanGate, 0);
            ScanStateChanged?.Invoke();
            DataChanged?.Invoke();
        }
    }

    /// <summary>Последовательно создаёт снимок каждого готового фиксированного и съёмного диска.</summary>
    public static async Task ScanAllAsync()
    {
        foreach (var drive in FixedDrives())
        {
            if (IsScanning) return;
            if (!await ScanAsync(drive.Name)) return;
        }
    }

    public static void CancelScan() => _cts?.Cancel();

    /// <summary>Проверка уведомлений после снимка: диск заполнен, быстрый рост.</summary>
    private static void NotifyCheck(Snapshot snap)
    {
        try
        {
            var s = Settings;
            if (!s.NotifyNearFull && !s.NotifyGrowth) return;
            double pct = snap.TotalBytes > 0 ? 100.0 * snap.UsedBytes / snap.TotalBytes : 0;
            if (s.NotifyNearFull && pct >= s.NearFullPercent)
                Notifier.NearFull(snap.Drive.TrimEnd('\\'), pct, snap.FreeBytes);

            if (s.NotifyGrowth)
            {
                var prev = Store.HistoryFor(snap.Drive).Where(h => h.Time < snap.Time).OrderByDescending(h => h.Time).FirstOrDefault();
                if (prev != null && (snap.Time - prev.Time).TotalHours >= 6)
                {
                    long d = snap.UsedBytes - prev.UsedBytes;
                    if (d >= Math.Max(2L * 1024 * 1024 * 1024, (long)((snap.Time - prev.Time).TotalHours / 24.0 * 3L * 1024 * 1024 * 1024)))
                        Notifier.Grew(snap.Drive.TrimEnd('\\'), Format.Delta(d));
                }
            }
        }
        catch { }
    }

    /// <summary>Режим без окна для Планировщика: снимок выбранных дисков и выход.</summary>
    public static void RunHeadless()
    {
        try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal; }
        catch { }

        var drives = Settings.AutoDrives.Count > 0
            ? Settings.AutoDrives
            : new List<string> { System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\" };

        foreach (var d in drives)
        {
            try
            {
                if (!new DriveInfo(d).IsReady) continue;
                var snap = new Scanner(Settings.Exclusions, Settings.UseDiskSize).Scan(d, CancellationToken.None);
                Store.Save(snap);
            }
            catch { }
        }
        Store.Prune(Settings.RetentionDays);
    }
}
