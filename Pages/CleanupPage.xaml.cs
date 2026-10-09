using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public sealed class CleanVm : INotifyPropertyChanged
{
    public CleanTarget Target { get; }
    public CleanVm(CleanTarget t, bool isAdmin)
    {
        Target = t;
        IsAvailable = !t.NeedsAdmin || isAdmin;
        _selected = t.DefaultOn && IsAvailable;
    }

    public string Title => Target.Title;
    public string Description => Target.Description;
    public string Icon => Target.Id switch
    {
        "browsers" => "",
        "apps" => "",
        "shaders" => "",
        "dumps" => "",
        "updates" => "",
        "dev" => "",
        "recycle" => "",
        _ => "",
    };
    public bool IsAvailable { get; }
    public Visibility AdminVisibility => Target.NeedsAdmin && !IsAvailable ? Visibility.Visible : Visibility.Collapsed;

    private bool _selected;
    public bool IsSelected { get => _selected; set { _selected = value; On(); } }

    private long? _size;
    public long? Size { get => _size; set { _size = value; On(); On(nameof(SizeText)); } }
    public string SizeText => !IsAvailable ? "—" : _size == null ? Loc.T("cl.counting") : Format.Size(_size.Value);

    private string _result = "";
    public string ResultText { get => _result; set { _result = value; On(); On(nameof(ResultVisibility)); } }
    public Visibility ResultVisibility => _result.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>После смены языка заново читаем все тексты.</summary>
    public void Relocalize() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void On([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public partial class CleanupPage : UserControl, IPage
{
    private readonly List<CleanVm> _items;
    private readonly bool _isAdmin = Native.IsAdmin();
    private CancellationTokenSource? _cts;
    private bool _busy;
    private DateTime _measuredAt = DateTime.MinValue;
    private string _status = "";
    private object[] _statusArgs = Array.Empty<object>();

    public CleanupPage()
    {
        InitializeComponent();
        _items = Cleaner.Targets().Select(t => new CleanVm(t, _isAdmin)).ToList();
        SafeItems.ItemsSource = _items.Where(i => i.Target.Safety == Safety.Safe).ToList();
        CarefulItems.ItemsSource = _items.Where(i => i.Target.Safety == Safety.Careful).ToList();
        AdminBanner.Visibility = !_isAdmin && _items.Any(i => i.Target.NeedsAdmin) ? Visibility.Visible : Visibility.Collapsed;
        Loc.Changed += () =>
        {
            foreach (var i in _items) i.Relocalize();
            UpdateTotal();
            ShowStatus(_status, _statusArgs);
        };
        UpdateTotal();
    }

    public void OnShown()
    {
        if (!_busy && (DateTime.Now - _measuredAt).TotalMinutes > 2) _ = MeasureAsync();
    }

    private void ShowStatus(string key, params object[] args)
    {
        _status = key;
        _statusArgs = args;
        StatusText.Text = key.Length == 0 ? "" : Loc.F(key, args.Select(a => a is long l ? Format.Size(l) : a).ToArray());
    }

    private async Task MeasureAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        _busy = true;
        SetButtons();
        ShowStatus("cl.measuring");
        foreach (var i in _items) { i.Size = null; i.ResultText = ""; }
        UpdateTotal();

        try
        {
            await Parallel.ForEachAsync(_items.Where(i => i.IsAvailable), new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cts.Token },
                async (vm, ct) =>
                {
                    var size = await Task.Run(() => Cleaner.Measure(vm.Target, ct), ct);
                    await Dispatcher.InvokeAsync(() => { vm.Size = size; UpdateTotal(); });
                });
            _measuredAt = DateTime.Now;
            ShowStatus("cl.pick");
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_cts == cts) { _busy = false; SetButtons(); }
        }
    }

    private void UpdateTotal()
    {
        long total = _items.Where(i => i.IsSelected && i.Size != null).Sum(i => i.Size!.Value);
        TotalText.Text = Loc.F("cl.selected", Format.Size(total));
    }

    private void SetButtons()
    {
        CleanButton.IsEnabled = !_busy && _items.Any(i => i.IsSelected);
        RecalcButton.IsEnabled = !_busy;
    }

    private void Check_Click(object sender, RoutedEventArgs e)
    {
        UpdateTotal();
        SetButtons();
    }

    private void Recalc_Click(object sender, RoutedEventArgs e) => _ = MeasureAsync();

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.IsSelected && i.IsAvailable).ToList();
        if (selected.Count == 0) return;

        long total = selected.Where(i => i.Size != null).Sum(i => i.Size!.Value);
        var list = string.Join("\n", selected.Select(i => "• " + i.Title));
        var answer = MessageBox.Show(Window.GetWindow(this)!, Loc.F("cl.confirm", Format.Size(total), list),
            Loc.T("cl.confirmT"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        _busy = true;
        SetButtons();
        long freed = 0;
        int skipped = 0;
        try
        {
            foreach (var vm in selected)
            {
                StatusText.Text = Loc.F("cl.cleaning", vm.Title);
                var r = await Task.Run(() => Cleaner.Clean(vm.Target, cts.Token));
                freed += r.Freed;
                skipped += r.Skipped;
                vm.ResultText = r.Skipped > 0
                    ? Loc.F("cl.freedSkip", Format.Size(r.Freed), Format.Count(r.Skipped))
                    : Loc.F("cl.freed", Format.Size(r.Freed));
                vm.Size = Math.Max(0, (vm.Size ?? 0) - r.Freed);
                UpdateTotal();
            }
            ShowStatus(skipped > 0 ? "cl.doneSkip" : "cl.done", freed);
        }
        catch (OperationCanceledException)
        {
            ShowStatus("cl.aborted");
        }
        finally
        {
            _busy = false;
            SetButtons();
        }
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        if (AppState.IsScanning)
        {
            MessageBox.Show(Window.GetWindow(this)!, Loc.T("common.waitScan"), "Enigma Disk");
            return;
        }
        if (Native.RestartAsAdmin()) Application.Current.Shutdown();
    }
}
