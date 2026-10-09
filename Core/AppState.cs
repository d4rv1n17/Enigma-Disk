namespace EnigmaDisk.Core;

/// <summary>Общее состояние приложения: хранилище снимков, настройки и текущее сканирование.</summary>
public static class AppState
{
    public static SnapshotStore Store { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;

    public static Scanner? ActiveScanner { get; private set; }
    public static string? ActiveDrive { get; private set; }
    private static CancellationTokenSource? _cts;

    /// <summary>Появились новые снимки или изменились настройки.</summary>
    public static event Action? DataChanged;
    /// <summary>Сканирование началось или закончилось.</summary>
    public static event Action? ScanStateChanged;
    /// <summary>Запрос на переход к папке на вкладке «Файлы».</summary>
    public static event Action<string>? NavigateToFolder;

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
        if (ActiveScanner != null) return false;
        var scanner = new Scanner();
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
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            ActiveScanner = null;
            ActiveDrive = null;
            _cts.Dispose();
            _cts = null;
            ScanStateChanged?.Invoke();
            DataChanged?.Invoke();
        }
    }

    public static void CancelScan() => _cts?.Cancel();

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
                var snap = new Scanner().Scan(d, CancellationToken.None);
                Store.Save(snap);
            }
            catch { }
        }
        Store.Prune(Settings.RetentionDays);
    }
}
