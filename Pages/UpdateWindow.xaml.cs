using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public partial class UpdateWindow : Window
{
    private readonly Updater.Info _info;
    private bool _busy;

    public UpdateWindow(Updater.Info info)
    {
        InitializeComponent();
        _info = info;
        DataContext = this;
        TitleText.Text = Loc.F("nt.versionTitle", info.Version);
        SubText.Text = Loc.F("upd.now", About.Version);
        NotesText.Text = string.IsNullOrWhiteSpace(info.Notes) ? "" : info.Notes.Trim();
        HasNotes = NotesText.Text.Length > 0;
        SourceInitialized += (_, _) => Native.DarkTitleBar(new WindowInteropHelper(this).Handle, 0x00121212);
    }

    public bool HasNotes { get; }

    private void Page_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = _info.HtmlUrl, UseShellExecute = true }); }
        catch { }
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        LaterBtn.IsEnabled = PageBtn.IsEnabled = UpdateBtn.IsEnabled = false;
        UpdateIcon.Text = "";
        ProgressRow.Visibility = Visibility.Visible;

        try
        {
            UpdateText.Text = Loc.T("upd.download");
            var file = Path.Combine(Path.GetTempPath(), $"EnigmaDisk-{_info.Version}.exe");
            await Updater.Download(_info.DownloadUrl, file, p =>
                Dispatcher.Invoke(() =>
                {
                    ProgBar.Width = Math.Max(0, Math.Min(1, p)) * Math.Max(1, ProgBar.Parent is Grid g ? g.ActualWidth : 1);
                    ProgText.Text = p.ToString("0%");
                }));

            UpdateText.Text = Loc.T("upd.install");
            ProgText.Text = "";
            ProgBar.Width = ProgBar.Parent is Grid g ? g.ActualWidth : 0;

            Updater.Apply(file);
            UpdateText.Text = Loc.T("upd.restart");
            await Task.Delay(800);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _busy = false;
            LaterBtn.IsEnabled = PageBtn.IsEnabled = UpdateBtn.IsEnabled = true;
            UpdateIcon.Text = "";
            UpdateText.Text = Loc.T("upd.update");
            ProgressRow.Visibility = Visibility.Collapsed;
            MessageBox.Show(this, Loc.F("upd.fail", ex.Message), "Enigma Disk",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
