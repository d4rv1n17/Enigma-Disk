using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnigmaDisk.Core;
using EnigmaDisk.Controls;

namespace EnigmaDisk.Pages;

public partial class TypesPage : UserControl, IPage
{
    private string? _drive;
    private Snapshot? _snap;
    private int _version;
    private int _selected = -1;   // категория, -1 — все

    public Brush BarBrush { get; } = (Brush)Application.Current.Resources["TextBrush"];

    public TypesPage()
    {
        InitializeComponent();
        Donut.SliceClick += i =>
        {
            var cats = CatList();
            if (i >= 0 && i < cats.Count) { _selected = cats[i].Index; RenderExt(); }
        };
        AppState.DataChanged += () => Dispatcher.Invoke(() => { _snap = null; if (IsVisible) _ = LoadAsync(); });
        Loc.Changed += () => Dispatcher.Invoke(() => { if (_snap != null) { RenderDonut(); RenderExt(); } });
    }

    public void OnShown() => _ = LoadAsync();

    private List<CategoryVm> CatList()
    {
        if (_snap == null) return new();
        var list = new List<CategoryVm>();
        foreach (var t in _snap.Types)
        {
            var def = TypeCategories.Get(t.N);
            if (def == null) continue;
            list.Add(new CategoryVm { Index = t.N, Name = def.Value.Name, Icon = def.Value.Icon, Color = def.Value.Color, Size = t.S, Count = t.C });
        }
        return list.Where(c => c.Size > 0).OrderByDescending(c => c.Size).ToList();
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
            Donut.SetData(new List<TypeDonut.Slice>());
            Legend.Children.Clear();
            List.ItemsSource = null;
            return;
        }
        if (_snap == null || !_snap.Drive.Equals(_drive, StringComparison.OrdinalIgnoreCase))
            _snap = await Task.Run(() => store.Load(latest));
        if (_snap == null || v != _version) return;

        RenderDonut();
        RenderExt();
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
                Content = d.TrimEnd('\\'), Style = (Style)FindResource("Seg"), GroupName = "tdrive",
                IsChecked = d == _drive, Tag = d,
            };
            rb.Checked += (s, __) => { _drive = (string)((RadioButton)s).Tag!; _snap = null; _selected = -1; _ = LoadAsync(); };
            DriveSeg.Children.Add(rb);
        }
    }

    private void RenderDonut()
    {
        if (_snap == null) return;
        var cats = CatList();
        var slices = cats.Select(c => new TypeDonut.Slice { Name = c.Name, Size = c.Size, Count = c.Count, Color = c.Color }).ToList();
        Donut.SetData(slices);

        Legend.Children.Clear();
        long total = Math.Max(1, cats.Sum(c => c.Size));
        foreach (var c in cats)
        {
            // строка-таблица: точка / имя / процент (80px) / размер (96px) / файлы — колонки ровные
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4), VerticalAlignment = VerticalAlignment.Center };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var dot = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c.Color)), VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = c.Name, Margin = new Thickness(10, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") };
            var pct = new TextBlock { Text = (c.Size * 100.0 / total).ToString("0.#", Loc.Culture) + "%", Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center };
            var size = new TextBlock { Text = c.SizeText, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("TextBrush"), HorizontalAlignment = HorizontalAlignment.Right };
            var count = new TextBlock { Text = Loc.Count(c.Count, "unit.file"), Foreground = (Brush)FindResource("DimBrush"), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(dot, 0); Grid.SetColumn(name, 1); Grid.SetColumn(pct, 2);
            Grid.SetColumn(size, 3); Grid.SetColumn(count, 4);
            row.Children.Add(dot); row.Children.Add(name); row.Children.Add(pct);
            row.Children.Add(size); row.Children.Add(count);
            var btn = new Button { Style = (Style)FindResource("Link"), Content = row, Tag = c.Index, HorizontalAlignment = HorizontalAlignment.Stretch };
            btn.Click += (s, _) => { _selected = (int)((Button)s).Tag!; RenderExt(); };
            Legend.Children.Add(btn);
        }

        // кнопка «все категории», когда выбрана конкретная
        if (_selected >= 0)
        {
            var all = new Button
            {
                Style = (Style)FindResource("Link"),
                Content = new TextBlock { Text = "◂ " + Loc.T("ty.allCats"), Foreground = (Brush)FindResource("AccentBrush"), FontWeight = FontWeights.SemiBold },
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            all.Click += (s, _) => { _selected = -1; RenderExt(); };
            Legend.Children.Add(all);
        }
        RenderExt();
    }

    private void RenderExt()
    {
        if (_snap == null) return;
        IEnumerable<ExtEntry> src = _snap.Exts;
        var sel = TypeCategories.Get(_selected);
        ExtTitle.Text = _selected >= 0 && sel != null
            ? Loc.F("ty.showExt", sel.Value.Name)
            : Loc.F("ty.showExt", Loc.T("ty.all"));
        var list = src.Where(e => _selected < 0 || e.N == _selected).OrderByDescending(e => e.S).Take(60).ToList();
        long total = Math.Max(1, list.Sum(e => e.S));
        foreach (var e in list) e.Fraction = e.S / (double)total;
        ExtSub.Text = Loc.F("ty.showN", list.Count, Format.Size(list.Sum(e => e.S)));
        List.ItemsSource = list;
        if (list.Count == 0) ExtSub.Text = Loc.T("ty.noExt");
    }
}
