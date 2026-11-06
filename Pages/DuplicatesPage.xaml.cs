using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

/// <summary>Один файл внутри группы дубликатов.</summary>
public sealed class DupFileVm : INotifyPropertyChanged
{
    public string Path { get; init; } = "";
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public long Size { get; init; }
    public bool IsOriginal { get; init; }
    public bool CanSelect => !IsOriginal;
    public bool Wasted { get; set; }

    private bool _selected;
    public bool IsSelected
    {
        get => _selected;
        set { _selected = value; On(); }
    }

    public string SizeText => Format.Size(Size);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void On([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Группа дубликатов для отображения.</summary>
public sealed class DupGroupVm
{
    public string Name { get; init; } = "";
    public long Size { get; init; }
    public List<DupFileVm> Items { get; init; } = new();
    public string SizeText => Format.Size(Size);
    public string CopiesText => Loc.F("du.copies", Items.Count);
    public string WasteText => Loc.F("du.waste", Format.Size(Size * Math.Max(0, Items.Count - 1)));
}

public partial class DuplicatesPage : UserControl, IPage
{
    private string? _drive;
    private List<DupGroupVm> _groups = new();
    private CancellationTokenSource? _cts;
    private bool _busy;
    private int _version;

    public DuplicatesPage()
    {
        InitializeComponent();
        AppState.DataChanged += () => Dispatcher.Invoke(() => { if (IsVisible && !_busy) { _groups.Clear(); Summary.Visibility = Visibility.Collapsed; List.ItemsSource = null; BuildDriveSeg(); } });
        Loaded += (_, _) => BuildDriveSeg();
    }

    public void OnShown() => BuildDriveSeg();

    private void BuildDriveSeg()
    {
        if (_busy) return;
        var store = AppState.Store;
        var drives = AppState.FixedDrives().Select(d => d.Name).ToList();
        if (_drive == null || !drives.Contains(_drive, StringComparer.OrdinalIgnoreCase))
            _drive = drives.FirstOrDefault(d => store.HistoryFor(d).Count > 0) ?? drives.FirstOrDefault();
        DriveSeg.Children.Clear();
        foreach (var d in drives)
        {
            var rb = new RadioButton
            {
                Content = d.TrimEnd('\\'), Style = (Style)FindResource("Seg"), GroupName = "ddrive",
                IsChecked = d == _drive, Tag = d,
            };
            rb.Checked += (s, _) => { _drive = (string)((RadioButton)s).Tag!; };
            DriveSeg.Children.Add(rb);
        }
    }

    private async void Find_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_drive == null) return;
        var latest = AppState.Store.Latest(_drive);
        if (latest == null)
        {
            MessageBox.Show(Window.GetWindow(this)!, Loc.T("du.noSnap"), "Enigma Disk");
            return;
        }

        _busy = true;
        _cts = new CancellationTokenSource();
        FindBtn.IsEnabled = false;
        Summary.Visibility = Visibility.Collapsed;
        Progress.Visibility = Visibility.Visible;
        List.ItemsSource = null;
        int v = ++_version;
        var ct = _cts.Token;

        try
        {
            var snap = await Task.Run(() => AppState.Store.Load(latest), ct);
            if (snap == null) return;
            var report = new Progress<Duplicates.Progress>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgTitle.Text = p.Phase == "hash" ? Loc.T("du.hashing") : Loc.F("du.scanning", _drive!.TrimEnd('\\'));
                    ProgPath.Text = p.Current;
                    ProgBar.Width = Math.Max(0, Math.Min(1, p.Percent / 100.0)) * Math.Max(1, ProgBar.Parent is Grid g ? g.ActualWidth : 1);
                });
            });
            IProgress<Duplicates.Progress> rep = report;
            var result = await Task.Run(() => Duplicates.Find(snap, p => rep.Report(p), ct), ct);
            if (v != _version) return;

            _groups = result.Groups.Select(g => new DupGroupVm
            {
                Name = g.Name,
                Size = g.Size,
                Items = g.Paths.Select((p, i) => new DupFileVm { Path = p, Size = g.Size, IsOriginal = i == 0, IsSelected = false }).ToList(),
            }).ToList();
            foreach (var g in _groups)
                foreach (var f in g.Items)
                    f.PropertyChanged += (_, _) => UpdateDeleteBtn();
            long wasted = _groups.Sum(g => g.Size * Math.Max(0, g.Items.Count - 1));
            SumText.Text = _groups.Count == 0 ? Loc.T("du.noneT") : Loc.F("du.found", Loc.Count(_groups.Count, "unit.snapshot"), Format.Size(wasted));
            SumSub.Text = _groups.Count == 0 ? Loc.T("du.noneD") : Loc.T("du.selectFirst");
            SumText.Foreground = _groups.Count == 0 ? (Brush)FindResource("MutedBrush") : (Brush)FindResource("AccentBrush");
            Summary.Visibility = Visibility.Visible;
            DeleteBtn.Visibility = _groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            List.ItemsSource = _groups;
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Invoke(() => { ProgTitle.Text = Loc.T("du.cancelled"); ProgPath.Text = ""; });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this)!, Loc.F("common.error", ex.Message), "Enigma Disk");
        }
        finally
        {
            _busy = false;
            if (v == _version)
            {
                FindBtn.IsEnabled = true;
                Progress.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void Copy_Check(object sender, RoutedEventArgs e) { }

    private void UpdateDeleteBtn()
    {
        int n = _groups.Sum(g => g.Items.Count(i => i.IsSelected));
        DeleteBtn.Content = Loc.F("du.deleteN", n);
        DeleteBtn.IsEnabled = n > 0;
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var toDelete = _groups.SelectMany(g => g.Items).Where(i => i.IsSelected && !i.IsOriginal).ToList();
        if (toDelete.Count == 0) return;
        long total = toDelete.Sum(i => i.Size);
        var r = MessageBox.Show(Window.GetWindow(this)!, Loc.F("du.confirm", toDelete.Count, Format.Size(total)),
            "Enigma Disk", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (r != MessageBoxResult.Yes) return;

        long freed = 0;
        int deleted = 0;
        foreach (var i in toDelete)
        {
            try
            {
                File.Delete(i.Path);
                freed += i.Size;
                deleted++;
                i.Wasted = true;
            }
            catch { }
        }
        // убираем удалённые из групп; группы с одной копией исчезают
        foreach (var g in _groups.ToList())
        {
            g.Items.RemoveAll(f => f.Wasted);
            if (g.Items.Count < 2) _groups.Remove(g);
        }
        long wasted = _groups.Sum(g => g.Size * Math.Max(0, g.Items.Count - 1));
        SumText.Text = Loc.F("du.done", deleted, Format.Size(freed));
        SumSub.Text = _groups.Count == 0 ? Loc.T("du.noneD") : Loc.F("du.found", Loc.Count(_groups.Count, "unit.snapshot"), Format.Size(wasted));
        SumText.Foreground = (Brush)FindResource("TextBrush");
        DeleteBtn.Content = Loc.T("du.delete");
        DeleteBtn.IsEnabled = false;
        List.ItemsSource = null;
        List.ItemsSource = _groups;
    }

    private void Show_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path }) Native.ShowInExplorer(path);
    }
}
