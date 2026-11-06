using System.Windows;
using System.Windows.Controls;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public partial class SettingsPage : UserControl, IPage
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
    }

    public void OnShown()
    {
        _loading = true;
        var s = AppState.Settings;
        HourBox.Text = s.AutoHour.ToString();
        MinBox.Text = s.MinGrowthMb.ToString();
        KeepBox.Text = s.RetentionDays.ToString();
        DiskSizeCheck.IsChecked = s.UseDiskSize;
        IntroCheck.IsChecked = s.ShowIntro;
        NotifNearFullCheck.IsChecked = s.NotifyNearFull;
        NotifGrowthCheck.IsChecked = s.NotifyGrowth;
        NearFullBox.Text = s.NearFullPercent.ToString();
        CheckUpdatesBox.IsChecked = s.CheckUpdates;
        if (Updater.Cached is { IsNewer: true } u) UpdateStatus.Text = Loc.F("upd.title", u.Version);

        var sys = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        if (s.AutoDrives.Count == 0) s.AutoDrives.Add(sys);

        DriveChecks.Children.Clear();
        foreach (var d in AppState.FixedDrives())
        {
            var cb = new CheckBox
            {
                Content = d.Name.TrimEnd('\\'), Tag = d.Name, Margin = new Thickness(0, 0, 20, 6),
                IsChecked = s.AutoDrives.Contains(d.Name, StringComparer.OrdinalIgnoreCase),
            };
            cb.Click += Drive_Click;
            DriveChecks.Children.Add(cb);
        }

        AutoCheck.IsChecked = Scheduler.IsEnabled();
        LangRu.IsChecked = !Loc.En;
        LangEn.IsChecked = Loc.En;
        UpdateAutoStatus();
        UpdateStoreInfo();
        _loading = false;
    }

    private void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var lang = sender == LangEn ? "en" : "ru";
        if (lang == Loc.Lang) return;
        AppState.Settings.Language = lang;
        AppState.SaveSettings();
        Loc.Apply(lang);
    }

    private void UpdateAutoStatus()
    {
        var s = AppState.Settings;
        AutoStatus.Text = AutoCheck.IsChecked == true
            ? Loc.F("st.autoOn", s.AutoHour, string.Join(", ", s.AutoDrives.Select(d => d.TrimEnd('\\'))))
            : Loc.T("st.autoOff");
    }

    private void UpdateStoreInfo()
    {
        var count = AppState.Store.History.Count;
        StoreInfo.Text = Loc.F("st.storeInfo", Loc.Count(count, "unit.snapshot"), Format.Size(AppState.Store.DiskUsage()), SnapshotStore.DataDir);
    }

    private void ApplySchedule()
    {
        if (AutoCheck.IsChecked != true) return;
        if (!Scheduler.Enable(AppState.Settings.AutoHour, out var err))
        {
            MessageBox.Show(Window.GetWindow(this)!, Loc.F("st.autoFail", err), "Enigma Disk",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            AutoCheck.IsChecked = Scheduler.IsEnabled();
        }
    }

    private void Auto_Click(object sender, RoutedEventArgs e)
    {
        if (AutoCheck.IsChecked == true) ApplySchedule();
        else Scheduler.Disable(out _);
        AutoCheck.IsChecked = Scheduler.IsEnabled();
        UpdateAutoStatus();
    }

    private void Drive_Click(object sender, RoutedEventArgs e)
    {
        var s = AppState.Settings;
        s.AutoDrives = DriveChecks.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
        AppState.SaveSettings();
        UpdateAutoStatus();
    }

    private void DiskSize_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppState.Settings.UseDiskSize = DiskSizeCheck.IsChecked == true;
        AppState.SaveSettings();
    }

    private void Intro_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppState.Settings.ShowIntro = IntroCheck.IsChecked == true;
        AppState.SaveSettings();
    }

    private static int Parse(TextBox box, int min, int max, int fallback)
    {
        if (!int.TryParse(box.Text.Trim(), out var v)) v = fallback;
        v = Math.Clamp(v, min, max);
        box.Text = v.ToString();
        return v;
    }

    private void Hour_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var s = AppState.Settings;
        var v = Parse(HourBox, 0, 23, s.AutoHour);
        if (v == s.AutoHour && !_hourDirty) return;
        _hourDirty = false;
        s.AutoHour = v;
        AppState.SaveSettings();
        ApplySchedule();
        UpdateAutoStatus();
    }

    private void Min_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var s = AppState.Settings;
        var v = Parse(MinBox, 1, 100000, s.MinGrowthMb);
        if (v == s.MinGrowthMb) return;
        s.MinGrowthMb = v;
        AppState.SaveSettings();
    }

    private void Keep_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var s = AppState.Settings;
        var v = Parse(KeepBox, 7, 3650, s.RetentionDays);
        if (v == s.RetentionDays) return;
        s.RetentionDays = v;
        AppState.SaveSettings();
    }

    /// <summary>Сохраняем сразу при вводе, чтобы значение не потерялось, если окно закрыть, не уводя фокус.</summary>
    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || sender is not TextBox box || !int.TryParse(box.Text.Trim(), out var v)) return;
        var s = AppState.Settings;
        if (box == MinBox && v >= 1 && v <= 100000 && v != s.MinGrowthMb) { s.MinGrowthMb = v; AppState.SaveSettings(); }
        else if (box == KeepBox && v >= 7 && v <= 3650 && v != s.RetentionDays) { s.RetentionDays = v; AppState.SaveSettings(); }
        else if (box == HourBox && v >= 0 && v <= 23 && v != s.AutoHour) { s.AutoHour = v; AppState.SaveSettings(); UpdateAutoStatus(); _hourDirty = true; }
    }

    private bool _hourDirty;

    private void OpenData_Click(object sender, RoutedEventArgs e) => Native.OpenFolder(SnapshotStore.DataDir);

    // ---- Уведомления ----

    private void Notif_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var s = AppState.Settings;
        s.NotifyNearFull = NotifNearFullCheck.IsChecked == true;
        s.NotifyGrowth = NotifGrowthCheck.IsChecked == true;
        AppState.SaveSettings();
    }

    private void NearFull_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || !int.TryParse(NearFullBox.Text.Trim(), out var v)) return;
        if (v < 10 || v > 99) return;
        if (v == AppState.Settings.NearFullPercent) return;
        AppState.Settings.NearFullPercent = v;
        AppState.SaveSettings();
    }

    private void NotifTest_Click(object sender, RoutedEventArgs e)
    {
        var s = AppState.Settings;
        var drive = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        double pct = 100.0 * s.NearFullPercent;
        long free = Math.Max(1, (long)(100 * 1024L * 1024 * 1024 * (100.0 - s.NearFullPercent) / s.NearFullPercent));
        Notifier.NearFull(drive.TrimEnd('\\'), pct, free);
    }

    // ---- Экспорт отчёта ----

    private string? _reportFile;

    private async void Report_Click(object sender, RoutedEventArgs e)
    {
        var latestAll = AppState.Store.History.OrderByDescending(h => h.Time).FirstOrDefault();
        if (latestAll == null)
        {
            MessageBox.Show(Window.GetWindow(this)!, Loc.T("rp.noData"), "Enigma Disk");
            return;
        }

        ReportBtn.IsEnabled = false;
        ReportOpen.Visibility = Visibility.Collapsed;
        ReportStatus.Text = "…";
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = Loc.T("rp.export"),
                Filter = "HTML|*.html",
                FileName = $"EnigmaDisk-{DateTime.Now:yyyy-MM-dd}.html",
            };
            if (dlg.ShowDialog() != true) { ReportStatus.Text = ""; return; }
            string file = await Task.Run(() => BuildReport(latestAll, dlg.FileName));
            _reportFile = file;
            ReportStatus.Text = Loc.F("rp.saved", file);
            ReportOpen.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this)!, Loc.F("common.error", ex.Message), "Enigma Disk");
        }
        finally
        {
            ReportBtn.IsEnabled = true;
        }
    }

    private static string BuildReport(SnapshotInfo latest, string file)
    {
        var store = AppState.Store;
        var drive = latest.Drive;
        var baseline = store.Baseline(drive, latest.Time.AddDays(-30), latest);
        var from = baseline;

        GrowthResult? growth = null;
        if (from != null)
        {
            var a = store.Load(from);
            var b = store.Load(latest);
            if (a != null && b != null)
                growth = Growth.Compare(a, b, from, latest, AppState.Settings.MinGrowthMb * 1024L * 1024L);
        }

        List<CategoryVm>? types = null;
        var snap = store.Load(latest);
        if (snap != null)
            types = snap.Types.Where(t => t.S > 0)
                .Select(t => new CategoryVm
                {
                    Index = t.N,
                    Name = TypeCategories.Get(t.N)?.Name ?? "",
                    Color = TypeCategories.Get(t.N)?.Color ?? "#94A3B8",
                    Size = t.S,
                    Count = t.C,
                }).OrderByDescending(t => t.Size).ToList();

        Report.Write(file, latest, from, growth, types, null);
        return file;
    }

    private void ReportOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_reportFile != null && File.Exists(_reportFile)) Native.OpenFolder(_reportFile);
    }

    // ---- Обновления и ссылка на проект ----

    private void CheckUpdatesSetting_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppState.Settings.CheckUpdates = CheckUpdatesBox.IsChecked == true;
        AppState.SaveSettings();
    }

    private void HowTo_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow w) WelcomeWindow.ShowAgain(w);
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (!Native.OpenUrl(About.Url))
            MessageBox.Show(Window.GetWindow(this)!, Loc.T("about.openUrlFail"), "Enigma Disk",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesBtn.IsEnabled = false;
        UpdateStatus.Text = Loc.T("upd.checking");
        try
        {
            var found = await Updater.CheckAsync();
            if (found == null)
                UpdateStatus.Text = Loc.F("upd.latest", About.Version);
            else
            {
                UpdateStatus.Text = "";
                new UpdateWindow(found) { Owner = Window.GetWindow(this) }.ShowDialog();
            }
        }
        catch
        {
            UpdateStatus.Text = Loc.T("about.checkFail");
        }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
        }
    }

    private void DeleteAll_Click(object sender, RoutedEventArgs e)
    {
        if (AppState.IsScanning) return;
        var r = MessageBox.Show(Window.GetWindow(this)!, Loc.T("st.deleteConfirm"),
            "Enigma Disk", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (r != MessageBoxResult.Yes) return;
        AppState.Store.DeleteAll();
        AppState.RaiseDataChanged();
        UpdateStoreInfo();
    }
}
