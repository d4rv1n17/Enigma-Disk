using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public partial class GrowthPage : UserControl, IPage
{
    private string? _drive;
    private int _days = 7;
    private GrowthResult? _result;
    private int _version;

    public Brush BarBrush { get; private set; } = (Brush)Application.Current.Resources["AccentBrush"];
    public Brush DeltaBrush { get; private set; } = (Brush)Application.Current.Resources["AccentBrush"];

    public GrowthPage()
    {
        InitializeComponent();
        DataContext = this;
        AppState.DataChanged += () => Dispatcher.Invoke(() => { if (IsVisible) Refresh(); });
    }

    public void OnShown() => Refresh();

    private void BuildDriveSeg(List<string> drives)
    {
        DriveSeg.Children.Clear();
        foreach (var d in drives)
        {
            var rb = new RadioButton
            {
                Content = d.TrimEnd('\\'), Style = (Style)FindResource("Seg"), GroupName = "gdrive",
                IsChecked = d == _drive, Tag = d,
            };
            rb.Checked += (s, _) => { _drive = (string)((RadioButton)s).Tag; Refresh(); };
            DriveSeg.Children.Add(rb);
        }
    }

    private void ShowEmpty(string title, string text, bool scanButton)
    {
        List.ItemsSource = null;
        ClickHint.Visibility = Visibility.Collapsed;
        Loading.Visibility = Visibility.Collapsed;
        EmptyTitle.Text = title;
        EmptyText.Text = text;
        EmptyScan.Visibility = scanButton ? Visibility.Visible : Visibility.Collapsed;
        EmptyScan.IsEnabled = !AppState.IsScanning;
        Empty.Visibility = Visibility.Visible;
    }

    private void Refresh()
    {
        var store = AppState.Store;
        var drives = AppState.FixedDrives().Select(d => d.Name).ToList();
        var withSnaps = drives.Where(d => store.HistoryFor(d).Count > 0).ToList();
        var sys = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        if (_drive == null || !drives.Contains(_drive))
            _drive = withSnaps.Contains(sys) ? sys : withSnaps.FirstOrDefault() ?? sys;
        BuildDriveSeg(drives);

        var latest = store.Latest(_drive);
        var baseline = latest == null ? null : store.Baseline(_drive, latest.Time.AddDays(-_days), latest);

        if (latest == null || baseline == null)
        {
            _result = null;
            Summary.Visibility = Visibility.Collapsed;
            if (latest == null)
                ShowEmpty(Loc.F("gr.noScanT", _drive.TrimEnd('\\')), Loc.T("gr.noScanD"), true);
            else
                ShowEmpty(Loc.T("gr.need2T"), Loc.F("gr.need2D", Format.Ago(latest.Time)), true);
            return;
        }

        Empty.Visibility = Visibility.Collapsed;
        _ = CompareAsync(_drive, baseline, latest);
    }

    private async Task CompareAsync(string drive, SnapshotInfo from, SnapshotInfo to)
    {
        int v = ++_version;
        Loading.Visibility = Visibility.Visible;
        List.ItemsSource = null;
        long min = AppState.Settings.MinGrowthMb * 1024L * 1024L;
        var store = AppState.Store;

        var result = await Task.Run(() =>
        {
            var a = store.Load(from);
            var b = store.Load(to);
            return a == null || b == null ? null : Growth.Compare(a, b, from, to, min);
        });
        if (v != _version) return;
        Loading.Visibility = Visibility.Collapsed;
        _result = result;
        if (result == null)
        {
            Summary.Visibility = Visibility.Collapsed;
            ShowEmpty(Loc.T("gr.badT"), Loc.T("gr.badD"), true);
            return;
        }

        Summary.Visibility = Visibility.Visible;
        var d = result.UsedDelta;
        SumDelta.Text = Format.Delta(d);
        SumDelta.Foreground = d > 0 ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextBrush");
        SumTitle.Text = Loc.F(d >= 0 ? "gr.sumUp" : "gr.sumDown", drive.TrimEnd('\\'));
        SumSub.Text = Loc.F("gr.sumSub", Format.When(from.Time), Format.When(to.Time), Format.Span(to.Time - from.Time), Format.Size(to.FreeBytes));
        ShowList();
    }

    private void ShowList()
    {
        if (_result == null) return;
        bool grew = ModeGrew.IsChecked == true;
        BarBrush = grew ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("MutedBrush");
        DeltaBrush = grew ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextBrush");
        // переустанавливаем DataContext, чтобы привязки к кистям обновились
        DataContext = null;
        DataContext = this;
        var items = grew ? _result.Grew : _result.Shrank;
        List.ItemsSource = items;
        if (items.Count == 0)
            ShowEmpty(Loc.T(grew ? "gr.noneUpT" : "gr.noneDownT"), Loc.F("gr.noneD", AppState.Settings.MinGrowthMb), false);
        else
        {
            Empty.Visibility = Visibility.Collapsed;
            ClickHint.Visibility = Visibility.Visible;
        }
    }

    private void Period_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && int.TryParse(rb.Tag as string, out var days))
        {
            _days = days;
            if (IsLoaded) Refresh();
        }
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) ShowList();
    }

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string path) AppState.OpenInFiles(path);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button b && b.Tag is string path) Native.OpenFolder(path);
    }

    private void EmptyScan_Click(object sender, RoutedEventArgs e)
    {
        if (_drive != null) _ = AppState.ScanAsync(_drive);
    }
}
