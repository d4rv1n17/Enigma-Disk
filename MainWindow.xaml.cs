using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EnigmaDisk.Core;
using EnigmaDisk.Pages;

namespace EnigmaDisk;

public partial class MainWindow : Window
{
    private readonly OverviewPage _overview = new();
    private readonly GrowthPage _growth = new();
    private readonly FilesPage _files = new();
    private readonly CleanupPage _clean = new();
    private readonly SettingsPage _settings = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool _syncLang;

    public MainWindow()
    {
        InitializeComponent();
        Host.Content = _overview;

        SourceInitialized += (_, _) =>
            Native.DarkTitleBar(new WindowInteropHelper(this).Handle, 0x00121212);

        AppState.ScanStateChanged += OnScanState;
        AppState.DataChanged += UpdateSideStatus;
        AppState.NavigateToFolder += path =>
        {
            _files.ShowPath(path);
            NavFiles.IsChecked = true;
        };
        Loc.Changed += () =>
        {
            SyncLangButtons();
            UpdateSideStatus();
            UpdateScanBar();
            if (Host.Content is IPage p) p.OnShown();
        };
        _timer.Tick += (_, _) => UpdateScanBar();
        SyncLangButtons();
        UpdateSideStatus();

        var pulse = new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(700))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        Pulse.BeginAnimation(OpacityProperty, pulse);

        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F5 && !AppState.IsScanning) _overview.ScanDefault();
        };
    }

    public void Go(string page)
    {
        var target = page switch
        {
            "growth" => NavGrowth,
            "files" => NavFiles,
            "clean" => NavClean,
            "settings" => NavSettings,
            _ => NavOverview,
        };
        target.IsChecked = true;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (Host == null) return;
        UserControl page = sender == NavGrowth ? _growth
            : sender == NavFiles ? _files
            : sender == NavClean ? _clean
            : sender == NavSettings ? _settings
            : _overview;
        Host.Content = page;
        if (page is IPage p) p.OnShown();
    }

    private void SyncLangButtons()
    {
        _syncLang = true;
        LangRu.IsChecked = !Loc.En;
        LangEn.IsChecked = Loc.En;
        _syncLang = false;
    }

    private void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncLang || !IsLoaded) return;
        var lang = sender == LangEn ? "en" : "ru";
        if (lang == Loc.Lang) return;
        AppState.Settings.Language = lang;
        AppState.SaveSettings();
        Loc.Apply(lang);
    }

    private void OnScanState()
    {
        Dispatcher.Invoke(() =>
        {
            if (AppState.IsScanning)
            {
                ScanBar.Visibility = Visibility.Visible;
                _timer.Start();
                UpdateScanBar();
            }
            else
            {
                _timer.Stop();
                ScanBar.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void UpdateScanBar()
    {
        var s = AppState.ActiveScanner;
        var drive = AppState.ActiveDrive;
        if (s == null || drive == null) return;

        ScanTitle.Text = Loc.F("scan.title", drive.TrimEnd('\\'));
        ScanStats.Text = Loc.F("scan.stats", Loc.Count(s.Files, "unit.file"), Format.Size(s.Bytes));
        ScanPath.Text = s.Current;

        // прогресс по отношению к занятому месту на диске
        try
        {
            var di = new DriveInfo(drive);
            double used = di.TotalSize - di.TotalFreeSpace;
            double f = used > 0 ? Math.Clamp(s.Bytes / used, 0, 1) : 0;
            var parent = (FrameworkElement)ScanProgress.Parent;
            ScanProgress.Width = parent.ActualWidth * f;
        }
        catch { }
    }

    private void UpdateSideStatus()
    {
        Dispatcher.Invoke(() =>
        {
            var last = AppState.Store.History.OrderByDescending(h => h.Time).FirstOrDefault();
            var count = AppState.Store.History.Count;
            SideStatus.Text = last == null
                ? Loc.T("side.noSnapshots")
                : Loc.F("side.status", Loc.Count(count, "unit.snapshot"), Format.Ago(last.Time));
        });
    }

    private void CancelScan_Click(object sender, RoutedEventArgs e) => AppState.CancelScan();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (AppState.IsScanning) AppState.CancelScan();
        base.OnClosing(e);
    }
}

/// <summary>Страница, которая обновляется при показе.</summary>
public interface IPage
{
    void OnShown();
}
