using System.Windows;
using System.Windows.Controls;
using EnigmaDisk.Core;
using EnigmaDisk.Controls;

namespace EnigmaDisk.Pages;

public sealed record SpaceCrumb(string Name, string Path);

public partial class SpacePage : UserControl, IPage
{
    private string? _drive;
    private string? _path;
    private Snapshot? _snap;
    private int _version;
    private List<(string path, string name, long size, bool isFile)>? _items;

    public SpacePage()
    {
        InitializeComponent();
        Map.TileClick += OnTileClick;
        Map.TileOpen += OnTileOpen;
        // при изменении размера карты пересобираем раскладку заново под новые пропорции
        Map.SizeChanged += (_, _) => LayoutMap();
        AppState.DataChanged += () => Dispatcher.Invoke(() => { _snap = null; if (IsVisible) _ = LoadAsync(); });
        Loc.Changed += () => Dispatcher.Invoke(() => { if (_snap != null && _path != null) Render(); });
    }

    public void OnShown() => _ = LoadAsync();

    public void ShowPath(string path)
    {
        var root = System.IO.Path.GetPathRoot(path);
        if (root == null) return;
        if (!string.Equals(root, _drive, StringComparison.OrdinalIgnoreCase)) { _drive = root; _snap = null; }
        _path = path;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        int v = ++_version;
        var store = AppState.Store;
        var drives = AppState.FixedDrives().Select(d => d.Name).ToList();
        if (_drive == null || !drives.Contains(_drive, StringComparer.OrdinalIgnoreCase))
            _drive = drives.FirstOrDefault(d => store.HistoryFor(d).Count > 0) ?? drives.FirstOrDefault() ?? @"C:\";
        BuildDriveSeg(drives);

        var latest = store.Latest(_drive);
        if (latest == null)
        {
            Map.SetTiles(new List<Treemap.Tile>());
            TotalEmptyText.Text = Loc.T("tm.empty");
            MapEmpty.Visibility = Visibility.Visible;
            return;
        }
        MapEmpty.Visibility = Visibility.Collapsed;
        if (_snap == null || !_snap.Drive.Equals(_drive, StringComparison.OrdinalIgnoreCase))
            _snap = await Task.Run(() => store.Load(latest));
        if (_snap == null || v != _version) return;

        if (_path == null || !_snap.Index.ContainsKey(_path)) _path = _snap.Drive;
        Render();
    }

    private void BuildDriveSeg(List<string> drives)
    {
        // пересобираем только если состав дисков поменялся, иначе Checked триггерит LoadAsync по кругу
        if (DriveSeg.Children.Count == drives.Count &&
            DriveSeg.Children.OfType<RadioButton>().Select(b => (string)b.Tag).SequenceEqual(drives))
            return;
        DriveSeg.Children.Clear();
        foreach (var d in drives)
        {
            var rb = new RadioButton
            {
                Content = d.TrimEnd('\\'), Style = (Style)FindResource("Seg"), GroupName = "sdrive",
                IsChecked = d == _drive, Tag = d,
            };
            rb.Checked += (s, __) => { _drive = (string)((RadioButton)s).Tag!; _snap = null; _path = null; _ = LoadAsync(); };
            DriveSeg.Children.Add(rb);
        }
    }

    private void Render()
    {
        if (_snap == null || _path == null) return;
        int idx = _snap.Index[_path];
        var me = _snap.Folders[idx];

        // дети папки + файлы (показываем файлы больше 8 МБ прямо в этой папке)
        var items = new List<(string path, string name, long size, bool isFile)>();
        foreach (var c in _snap.ChildrenOf(idx))
            items.Add((_snap.Paths[c], _snap.Folders[c].N, _snap.Folders[c].S, false));
        foreach (var f in _snap.TopFiles)
        {
            if (f.S < 8L << 20) break;
            var dir = System.IO.Path.GetDirectoryName(f.Path);
            if (dir != null && dir.Equals(_path, StringComparison.OrdinalIgnoreCase))
                items.Add((f.Path, System.IO.Path.GetFileName(f.Path), f.S, true));
        }
        _items = items;
        LayoutMap();

        UpButton.IsEnabled = me.P >= 0;
        TotalText.Text = $"{Loc.T("tm.title")}: {Format.Size(me.S)}";

        // хлебные крошки
        var crumbs = new List<SpaceCrumb>();
        int cr = idx;
        while (cr >= 0)
        {
            var f = _snap.Folders[cr];
            crumbs.Insert(0, new SpaceCrumb(f.P < 0 ? f.N.TrimEnd('\\') : f.N, _snap.Paths[cr]));
            cr = f.P;
        }
        Crumbs.ItemsSource = crumbs;
    }

    /// <summary>Раскладка плиток под фактический размер панели; вызывается и при изменении размера.
    /// У крупных папок прямо на карте показываем их содержимое (один уровень вглубь).</summary>
    private void LayoutMap()
    {
        if (_items == null || _snap == null) return;
        double w = Map.ActualWidth, h = Map.ActualHeight;
        if (w < 50 || h < 50) return;   // панель ещё не измерена — придём снова по SizeChanged
        Map.SetTiles(Treemap.LayoutNested(_items, w, h, ChildrenOfSnapshot));
    }

    /// <summary>Дети папки из снапшота: подпапки + крупные файлы (≥8 МБ) этой папки.</summary>
    private IReadOnlyList<(string path, string name, long size, bool isFile)> ChildrenOfSnapshot(string path)
    {
        var snap = _snap!;
        if (!snap.Index.TryGetValue(path, out int idx)) return Array.Empty<(string, string, long, bool)>();
        var list = new List<(string path, string name, long size, bool isFile)>();
        foreach (var c in snap.ChildrenOf(idx))
            list.Add((snap.Paths[c], snap.Folders[c].N, snap.Folders[c].S, false));
        foreach (var f in snap.TopFiles)
        {
            if (f.S < 8L << 20) break;
            var dir = System.IO.Path.GetDirectoryName(f.Path);
            if (dir != null && dir.Equals(path, StringComparison.OrdinalIgnoreCase))
                list.Add((f.Path, System.IO.Path.GetFileName(f.Path), f.S, true));
        }
        return list;
    }

    private void OnTileClick(string path, bool isFile)
    {
        // клик по папке открывает её прямо на карте, по файлу — Проводник
        if (isFile) Native.ShowInExplorer(path);
        else if (_snap != null && _snap.Index.ContainsKey(path)) { _path = path; Render(); }
    }

    private void OnTileOpen(string path) { _path = path; Render(); }

    private void Hint_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(Window.GetWindow(this)!, Loc.T("tm.hintText"), Loc.T("tm.hint"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (_snap == null || _path == null) return;
        if (_snap.Index.TryGetValue(_path, out var i) && _snap.Folders[i].P >= 0)
            _path = _snap.Paths[_snap.Folders[i].P];
        Render();
    }

    public void GoUp() => Up_Click(this, new RoutedEventArgs());

    private void Crumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string p) { _path = p; Render(); }
    }

    private void Explorer_Click(object sender, RoutedEventArgs e)
    {
        if (_path != null) Native.OpenFolder(_path);
    }
}
