using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public sealed class DriveVm
{
    public string Root { get; init; } = "";
    public string Letter => Root.TrimEnd('\\');
    public string Label { get; init; } = "";
    public long Total { get; init; }
    public long Free { get; init; }
    public double UsedFraction => Total > 0 ? (double)(Total - Free) / Total : 0;
    public string UsedCaption => Loc.T("ov.used");
    public string FreeText => Loc.F("ov.free", Format.Size(Free));
    public string TotalText => Loc.F("ov.of", Format.Size(Total));
    /// <summary>Жёлтый, когда диск почти заполнен; иначе белый.</summary>
    public Brush BarBrush => UsedFraction > 0.85
        ? (Brush)Application.Current.Resources["AccentBrush"]
        : (Brush)Application.Current.Resources["TextBrush"];
    public string LastScanText { get; init; } = "";
    public string WeekText { get; init; } = "";
    public Visibility WeekVisibility => WeekText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public bool CanScan { get; init; }
}

public partial class OverviewPage : UserControl, IPage
{
    private string? _chartDrive;
    private int _version;

    public OverviewPage()
    {
        InitializeComponent();
        Loaded += (_, _) => { Refresh(); _ = CheckAutoAsync(); };
        AppState.DataChanged += () => Dispatcher.Invoke(Refresh);
        AppState.ScanStateChanged += () => Dispatcher.Invoke(Refresh);
    }

    public void OnShown()
    {
        Refresh();
        _ = CheckAutoAsync();
    }

    private static string SystemDrive => System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    public void ScanDefault() => _ = AppState.ScanAsync(SystemDrive);

    private async Task CheckAutoAsync()
    {
        bool on = await Task.Run(Scheduler.IsEnabled);
        AutoTip.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Refresh()
    {
        if (!IsLoaded) return;
        var drives = AppState.FixedDrives();
        var store = AppState.Store;
        var history = store.History;

        HowItWorks.Visibility = history.Count(h => h.Version == Snapshot.CurrentVersion) < 3 ? Visibility.Visible : Visibility.Collapsed;
        MainScanText.Text = Loc.F("common.scanDrive", SystemDrive.TrimEnd('\\'));
        MainScan.IsEnabled = !AppState.IsScanning;

        var last = history.OrderByDescending(h => h.Time).FirstOrDefault();
        Subtitle.Text = last == null ? Loc.T("side.noSnapshots") : Loc.F("ov.last", Format.Ago(last.Time), Format.When(last.Time));

        var vms = new List<DriveVm>();
        foreach (var d in drives)
        {
            var latest = store.Latest(d.Name);
            string week = "";
            if (latest != null)
            {
                var baseline = store.Baseline(d.Name, latest.Time.AddDays(-7), latest);
                if (baseline != null)
                    week = Loc.F("ov.change", Format.Delta(latest.UsedBytes - baseline.UsedBytes), Format.Span(latest.Time - baseline.Time));
            }
            string label;
            try { label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? Loc.T("common.localDisk") : d.VolumeLabel; }
            catch { label = Loc.T("common.drive"); }

            vms.Add(new DriveVm
            {
                Root = d.Name,
                Label = label,
                Total = d.TotalSize,
                Free = d.TotalFreeSpace,
                LastScanText = AppState.ActiveDrive == d.Name ? Loc.T("ov.scanning")
                    : latest == null ? Loc.T("ov.neverScanned") : Loc.F("ov.snapAgo", Format.Ago(latest.Time)),
                WeekText = week,
                CanScan = !AppState.IsScanning,
            });
        }
        Drives.ItemsSource = vms;

        // график
        var scanned = drives.Select(d => d.Name).Where(n => store.HistoryFor(n).Count > 0).ToList();
        if (_chartDrive == null || !scanned.Contains(_chartDrive))
            _chartDrive = scanned.Contains(SystemDrive) ? SystemDrive : scanned.FirstOrDefault();

        ChartCard.Visibility = scanned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TopCard.Visibility = scanned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_chartDrive == null) return;

        ChartDrives.Children.Clear();
        foreach (var n in scanned)
        {
            var rb = new RadioButton
            {
                Content = n.TrimEnd('\\'), Style = (Style)FindResource("Seg"), GroupName = "chartDrive",
                IsChecked = n == _chartDrive, Tag = n,
            };
            rb.Checked += (s, _) => { _chartDrive = (string)((RadioButton)s).Tag; Refresh(); };
            ChartDrives.Children.Add(rb);
        }

        var h = store.HistoryFor(_chartDrive);
        Chart.SetData(h.Select(x => (x.Time, x.UsedBytes)));
        ChartTitle.Text = Loc.F("ov.chartTitle", _chartDrive.TrimEnd('\\'));
        ChartSub.Text = h.Count < 2
            ? Loc.T("ov.chartNeed2")
            : Loc.F("ov.chartSub", Loc.Count(h.Count, "unit.snapshot"), Format.When(h[0].Time));

        _ = LoadTopAsync(_chartDrive);
    }

    private async Task LoadTopAsync(string drive)
    {
        int v = ++_version;
        var store = AppState.Store;
        var latest = store.Latest(drive);
        if (latest == null) return;
        var baseInfo = store.Baseline(drive, latest.Time.AddDays(-7), latest);
        TopTitle.Text = Loc.F("ov.topTitle", drive.TrimEnd('\\'));
        if (baseInfo == null)
        {
            TopList.ItemsSource = null;
            TopSub.Text = "";
            TopEmpty.Text = Loc.T("ov.onlyOne");
            TopEmpty.Visibility = Visibility.Visible;
            return;
        }

        long min = AppState.Settings.MinGrowthMb * 1024L * 1024L;
        var result = await Task.Run(() =>
        {
            var a = store.Load(baseInfo);
            var b = store.Load(latest);
            return a == null || b == null ? null : Growth.Compare(a, b, baseInfo, latest, min);
        });
        if (result == null || v != _version) return;

        TopSub.Text = Loc.F("ov.topSub", Format.When(baseInfo.Time), Format.Delta(result.UsedDelta));
        var top = result.Grew.Take(6).ToList();
        if (top.Count > 0) { double m = top[0].Delta; foreach (var t in top) t.Fraction = t.Delta / m; }
        TopList.ItemsSource = top;
        TopEmpty.Visibility = top.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TopEmpty.Text = Loc.T("ov.nothingGrew");
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string root) _ = AppState.ScanAsync(root);
    }

    private void MainScan_Click(object sender, RoutedEventArgs e) => ScanDefault();

    private void AutoTip_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow w) w.Go("settings");
    }

    private void AllChanges_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow w) w.Go("growth");
    }

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string path) AppState.OpenInFiles(path);
    }
}
