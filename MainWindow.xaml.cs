using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using EnigmaDisk.Core;
using EnigmaDisk.Pages;

namespace EnigmaDisk;

public partial class MainWindow : Window
{
    private readonly OverviewPage _overview = new();
    private readonly GrowthPage _growth = new();
    private readonly FilesPage _files = new();
    private readonly SpacePage _space = new();
    private readonly TypesPage _types = new();
    private readonly DuplicatesPage _duplicates = new();
    private readonly CleanupPage _clean = new();
    private readonly ExclusionsPage _exclude = new();
    private readonly SettingsPage _settings = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool _syncLang;
    private Notifier.Notification? _notif;
    private TaskCompletionSource? _introPreviewDismissed;

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
        AppState.OpenFolder += path =>
        {
            _space.ShowPath(path);
            NavSpace.IsChecked = true;
        };
        Notifier.Raised += OnNotification;
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
            if (e.Key == System.Windows.Input.Key.Escape && Host.Content is SpacePage sp) sp.GoUp();
            if (e.Key == Key.K && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                Go("files");
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => _files.FocusSearch()));
                e.Handled = true;
            }
        };

        // Проверка обновлений при запуске: тихо, показываем предложение только когда есть новая версия
        if (AppState.Settings.CheckUpdates)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var found = await Updater.CheckAsync();
                    if (found != null)
                        Dispatcher.Invoke(() => Notifier.Raise(new Notifier.Notification
                        {
                            Kind = "info",
                            Title = Loc.F("nt.versionTitle", found.Version),
                            Body = Loc.F("upd.now", About.Version) + " → " + found.Version,
                            Navigate = "update",
                        }));
                }
                catch { /* офлайн — молча */ }
            });
        }
    }

    public async Task PlayIntroAsync(bool preview = false)
    {
        if (!preview && !AppState.Settings.ShowIntro) return;
        var wasTopmost = Topmost;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        IntroPreviewHint.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
        if (preview)
        {
            _introPreviewDismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            IntroOverlay.MouseLeftButtonDown += IntroPreview_Click;
        }
        IntroOverlay.Visibility = Visibility.Visible;
        var rotate = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.8))
        { RepeatBehavior = RepeatBehavior.Forever };
        IntroRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, rotate);
        IntroContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)));
        IntroProgress.BeginAnimation(WidthProperty, new DoubleAnimation(0, 110, TimeSpan.FromSeconds(2.3))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        if (preview) await _introPreviewDismissed!.Task;
        else await Task.Delay(2600);
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350));
        fade.Completed += (_, _) =>
        {
            IntroOverlay.Visibility = Visibility.Collapsed;
            Topmost = wasTopmost;
        };
        IntroContent.BeginAnimation(OpacityProperty, fade);
        if (preview)
        {
            IntroOverlay.MouseLeftButtonDown -= IntroPreview_Click;
            _introPreviewDismissed = null;
        }
    }

    private void IntroPreview_Click(object sender, MouseButtonEventArgs e) => _introPreviewDismissed?.TrySetResult();

    public void Go(string page)
    {
        var target = page switch
        {
            "growth" => NavGrowth,
            "space" => NavSpace,
            "types" => NavTypes,
            "files" => NavFiles,
            "duplicates" => NavDuplicates,
            "clean" => NavClean,
            "exclude" => NavExclude,
            "settings" => NavSettings,
            _ => NavOverview,
        };
        target.IsChecked = true;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (Host == null) return;
        UserControl page = sender == NavGrowth ? _growth
            : sender == NavSpace ? _space
            : sender == NavTypes ? _types
            : sender == NavFiles ? _files
            : sender == NavDuplicates ? _duplicates
            : sender == NavClean ? _clean
            : sender == NavExclude ? _exclude
            : sender == NavSettings ? _settings
            : _overview;
        Host.Content = page;
        if (page is IPage p) p.OnShown();
    }

    private void OnNotification(Notifier.Notification n)
    {
        Dispatcher.Invoke(() =>
        {
            _notif = n;
            NotifTitle.Text = n.Title;
            NotifBody.Text = n.Body;
            NotifIcon.Text = n.Kind == "warn" ? "" : "";
            NotifOpen.Visibility = n.Navigate != null ? Visibility.Visible : Visibility.Collapsed;
            NotifBar.Visibility = Visibility.Visible;
        });
    }

    private void NotifOpen_Click(object sender, RoutedEventArgs e)
    {
        NotifBar.Visibility = Visibility.Collapsed;
        if (_notif?.Navigate is string p)
        {
            if (p == "update" && Updater.Cached is { IsNewer: true } info)
                new Pages.UpdateWindow(info) { Owner = this }.ShowDialog();
            else Go(p);
        }
    }

    private void NotifClose_Click(object sender, RoutedEventArgs e) => NotifBar.Visibility = Visibility.Collapsed;

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
                NotifBar.Visibility = Visibility.Collapsed;
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
