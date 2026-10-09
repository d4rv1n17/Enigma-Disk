using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public sealed class FolderRow
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public string Sub { get; init; } = "";
    public double Fraction { get; init; }
    public string PercentText { get; init; } = "";
    public string DeltaText { get; init; } = "";
    public string SizeText { get; init; } = "";
    public string Icon { get; init; } = "\uE8B7";
    public Brush IconBrush { get; init; } = (Brush)Application.Current.Resources["MutedBrush"];
    public bool IsFolder { get; init; }
    public bool IsFile { get; init; }
}

public sealed record Crumb(string Name, string Path);

public partial class FilesPage : UserControl, IPage
{
    private string? _drive;
    private string? _path;
    private Snapshot? _snap;
    private Snapshot? _base;
    private int _version;

    public FilesPage()
    {
        InitializeComponent();
        AppState.DataChanged += () => Dispatcher.Invoke(() => { _snap = null; if (IsVisible) _ = LoadAsync(); });
    }

    public void OnShown() => _ = LoadAsync();

    /// <summary>Открыть конкретную папку (например, из списка «Что выросло»).</summary>
    public void ShowPath(string path)
    {
        var root = System.IO.Path.GetPathRoot(path);
        if (root == null) return;
        if (!string.Equals(root, _drive, StringComparison.OrdinalIgnoreCase)) { _drive = root; _snap = null; }
        _path = path;
        TabFolders.IsChecked = true;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        int v = ++_version;
        var store = AppState.Store;
        var drives = AppState.FixedDrives().Select(d => d.Name).ToList();
        var sys = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        if (_drive == null || !drives.Contains(_drive, StringComparer.OrdinalIgnoreCase))
        {
            var withSnaps = drives.Where(d => store.HistoryFor(d).Count > 0).ToList();
            _drive = withSnaps.Contains(sys) ? sys : withSnaps.FirstOrDefault() ?? sys;
            _snap = null;
        }
        BuildDriveSeg(drives);

        var latest = store.Latest(_drive);
        if (latest == null)
        {
            ShowEmpty(Loc.F("fl.noScanT", _drive.TrimEnd('\\')), Loc.T("fl.noScanD"), true);
            return;
        }

        if (_snap == null || _snap.Time != latest.Time || !_snap.Drive.Equals(_drive, StringComparison.OrdinalIgnoreCase))
        {
            var baseInfo = store.Baseline(_drive, latest.Time.AddDays(-7), latest);
            var (s, b) = await Task.Run(() => (store.Load(latest), baseInfo == null ? null : store.Load(baseInfo)));
            if (v != _version) return;
            _snap = s;
            _base = b;
        }

        if (_snap == null)
        {
            ShowEmpty(Loc.T("gr.badT"), Loc.T("gr.badD"), true);
            return;
        }

        Empty.Visibility = Visibility.Collapsed;
        Subtitle.Text = Loc.F("fl.info", Format.When(_snap.Time), Loc.Count(_snap.FileCount, "unit.file"),
            Format.Size(_snap.ScannedBytes), Format.Duration(_snap.DurationSec));

        if (_path == null || !_snap.Index.ContainsKey(_path)) _path = _snap.Drive;
        Render();
    }

    private void ShowEmpty(string title, string text, bool scanButton)
    {
        List.ItemsSource = null;
        Crumbs.ItemsSource = null;
        CrumbBar.Visibility = Visibility.Collapsed;
        ColHeader.Visibility = Visibility.Collapsed;
        if (scanButton) Subtitle.Text = "";
        EmptyTitle.Text = title;
        EmptyText.Text = text;
        EmptyScan.Visibility = scanButton ? Visibility.Visible : Visibility.Collapsed;
        EmptyScan.IsEnabled = !AppState.IsScanning;
        Empty.Visibility = Visibility.Visible;
    }

    private void BuildDriveSeg(List<string> drives)
    {
        DriveSeg.Children.Clear();
        foreach (var d in drives)
        {
            var rb = new RadioButton
            {
                Content = d.TrimEnd('\\'), Style = (Style)FindResource("Seg"), GroupName = "fdrive",
                IsChecked = string.Equals(d, _drive, StringComparison.OrdinalIgnoreCase), Tag = d,
            };
            rb.Checked += (s, args) =>
            {
                _drive = (string)((RadioButton)s).Tag;
                _path = null;
                _snap = null;
                _ = LoadAsync();
            };
            DriveSeg.Children.Add(rb);
        }
    }

    private void Render()
    {
        if (_snap == null || _path == null) return;
        bool files = TabFiles.IsChecked == true;
        CrumbBar.Visibility = files ? Visibility.Collapsed : Visibility.Visible;
        ColHeader.Visibility = Visibility.Visible;
        ColShare.Text = files ? "" : Loc.T("fl.colShare");
        ColWeek.Text = files ? Loc.T("fl.colModified") : Loc.T("fl.colWeek");
        if (files) { RenderFiles(); return; }

        int idx = _snap.Index[_path];
        var me = _snap.Folders[idx];
        var kids = _snap.ChildrenOf(idx).Select(i => (i, f: _snap.Folders[i])).OrderByDescending(x => x.f.S).ToList();
        long kidsSum = kids.Sum(k => k.f.S);
        long rest = me.S - kidsSum;
        double parent = Math.Max(1, me.S);
        double maxKid = Math.Max(1, Math.Max(kids.Count > 0 ? kids[0].f.S : 0, rest));
        var accent = (Brush)FindResource("AccentBrush");

        var rows = new List<FolderRow>();
        foreach (var (i, f) in kids)
        {
            var path = _snap.Paths[i];
            string delta = "";
            if (_base != null)
            {
                var d = f.S - _base.SizeOf(path);
                if (Math.Abs(d) >= 10L << 20) delta = Format.Delta(d);
            }
            var hint = Hints.Describe(path);
            rows.Add(new FolderRow
            {
                Path = path,
                Name = f.N,
                Sub = Loc.Count(f.F, "unit.file") + (hint.Length > 0 ? " · " + hint : ""),
                Fraction = f.S / maxKid,
                PercentText = Loc.F("fl.pct", f.S / parent * 100),
                DeltaText = delta,
                SizeText = Format.Size(f.S),
                IsFolder = true,
                IconBrush = Hints.IsCleanable(path) ? accent : (Brush)FindResource("MutedBrush"),
            });
        }
        if (rest >= 1L << 20)
        {
            rows.Add(new FolderRow
            {
                Path = _path,
                Name = Loc.T("fl.rest"),
                Sub = Loc.T("fl.restSub"),
                Fraction = rest / maxKid,
                PercentText = Loc.F("fl.pct", rest / parent * 100),
                SizeText = Format.Size(rest),
                Icon = "\uE8A5",
                IconBrush = (Brush)FindResource("DimBrush"),
            });
        }
        List.ItemsSource = rows;

        // хлебные крошки
        var crumbs = new List<Crumb>();
        int c = idx;
        while (c >= 0)
        {
            var f = _snap.Folders[c];
            crumbs.Insert(0, new Crumb(f.P < 0 ? f.N.TrimEnd('\\') : f.N, _snap.Paths[c]));
            c = f.P;
        }
        Crumbs.ItemsSource = crumbs;
        UpButton.IsEnabled = me.P >= 0;
    }

    private void RenderFiles()
    {
        if (_snap == null) return;
        var top = _snap.TopFiles;
        if (top.Count == 0)
        {
            ShowEmpty(Loc.T("fl.noBigT"), Loc.T("fl.noBigD"), false);
            return;
        }
        double max = Math.Max(1, top[0].S);
        List.ItemsSource = top.Select(f => new FolderRow
        {
            Path = f.Path,
            Name = System.IO.Path.GetFileName(f.Path),
            Sub = System.IO.Path.GetDirectoryName(f.Path) ?? "",
            Fraction = f.S / max,
            DeltaText = f.Modified == default ? "" : Format.Date(f.Modified),
            SizeText = Format.Size(f.S),
            Icon = "\uE8A5",
            IsFile = true,
        }).ToList();
    }

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FolderRow row }) return;
        if (row.IsFolder) { _path = row.Path; Render(); }
        else if (row.IsFile) Native.ShowInExplorer(row.Path);
    }

    private void Crumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path }) { _path = path; Render(); }
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (_snap == null || _path == null) return;
        var f = _snap.Folders[_snap.Index[_path]];
        if (f.P >= 0) { _path = _snap.Paths[f.P]; Render(); }
    }

    private void Explorer_Click(object sender, RoutedEventArgs e)
    {
        if (_path != null) Native.OpenFolder(_path);
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && _snap != null) { Empty.Visibility = Visibility.Collapsed; Render(); }
    }

    private void EmptyScan_Click(object sender, RoutedEventArgs e)
    {
        if (_drive != null) _ = AppState.ScanAsync(_drive);
    }
}
