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
