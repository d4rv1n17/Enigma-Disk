using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace EnigmaDiskSetup
{
    public partial class SetupWindow : Window
    {
        private enum Stage { Options, Working, Done }

        private Stage _stage = Stage.Options;
        private string _dir;
        private readonly bool _update;
        private string _stepKey = "stepClose";
        private double _progress;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public SetupWindow()
        {
            InitializeComponent();
            var installed = Installer.InstalledDir();
            _update = installed != null;
            _dir = installed ?? Installer.DefaultDir;

            LangRu.IsChecked = !Text.En;
            LangEn.IsChecked = Text.En;

            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                int on = 1, caption = 0x000F0F0F;
                if (DwmSetWindowAttribute(h, 20, ref on, 4) != 0) DwmSetWindowAttribute(h, 19, ref on, 4);
                DwmSetWindowAttribute(h, 35, ref caption, 4);
            };
            ApplyTexts();
        }

        private void ApplyTexts()
        {
            Title = Text.T("title");
            Header.Text = Text.T("header");
            TitleText.Text = Text.F(_update ? "welcomeUpdate" : "welcome", Installer.Version.Substring(0, 3));
            Pitch.Text = Text.T("pitch");
            FolderLabel.Text = Text.T("folder");
            FolderText.Text = _dir;
            ChangeButton.Content = Text.T("change");
            DesktopCheck.Content = Text.T("desktop");
            LaunchCheck.Content = Text.T("launch");
            long mb = Math.Max(1, Installer.PayloadSize() / (1024 * 1024));
            Note.Text = Text.F("note", Text.F("mb", mb));
            WorkingTitle.Text = Text.T("working");
            StepText.Text = Text.T(_stepKey);
            DoneTitle.Text = Text.T("doneTitle");
            DoneText.Text = Text.T("doneText");
            CancelButton.Content = Text.T("cancel");
            MainButton.Content = _stage == Stage.Done ? Text.T("finish") : Text.T(_update ? "update" : "install");
        }

        private void Lang_Checked(object sender, RoutedEventArgs e)
        {
            if (Header == null) return;
            Text.En = sender == LangEn;
            ApplyTexts();
        }

        private void Change_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = Text.T("pick");
                dlg.SelectedPath = Directory.Exists(_dir) ? _dir : Path.GetDirectoryName(_dir);
                dlg.ShowNewFolderButton = true;
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var picked = dlg.SelectedPath;
                // ставим в подпапку «Enigma Disk», если выбрали общую папку
                if (!Path.GetFileName(picked.TrimEnd('\\')).Equals(Installer.AppName, StringComparison.OrdinalIgnoreCase))
                    picked = Path.Combine(picked, Installer.AppName);
                _dir = picked;
                FolderText.Text = _dir;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private async void Main_Click(object sender, RoutedEventArgs e)
        {
            if (_stage == Stage.Done)
            {
                if (LaunchCheck.IsChecked == true) Installer.Launch(_dir);
                Close();
                return;
            }

            _stage = Stage.Working;
            PageOptions.Visibility = Visibility.Collapsed;
            PageProgress.Visibility = Visibility.Visible;
            MainButton.IsEnabled = false;
            CancelButton.Visibility = Visibility.Collapsed;
            LangRu.IsEnabled = LangEn.IsEnabled = false;

            bool desktop = DesktopCheck.IsChecked == true;
            string dir = _dir;
            try
            {
                await Task.Run(() => Installer.Install(dir, desktop, (key, p) =>
                    Dispatcher.BeginInvoke(new Action(() => ShowProgress(key, p)))));
            }
            catch (Exception ex)
            {
                var msg = ex is IOException || ex is UnauthorizedAccessException ? Text.T("busy") + "\n\n" + ex.Message : ex.Message;
                MessageBox.Show(this, Text.F("error", msg), Text.T("title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                _stage = Stage.Options;
                PageProgress.Visibility = Visibility.Collapsed;
                PageOptions.Visibility = Visibility.Visible;
                MainButton.IsEnabled = true;
                CancelButton.Visibility = Visibility.Visible;
                LangRu.IsEnabled = LangEn.IsEnabled = true;
                return;
            }

            _stage = Stage.Done;
            PageProgress.Visibility = Visibility.Collapsed;
            PageDone.Visibility = Visibility.Visible;
            MainButton.IsEnabled = true;
            ApplyTexts();
        }

        private void ShowProgress(string key, double p)
        {
            _stepKey = key;
            _progress = Math.Max(_progress, p);
            StepText.Text = Text.T(key);
            var parent = (FrameworkElement)ProgressFill.Parent;
            ProgressFill.Width = parent.ActualWidth * _progress;
            PercentText.Text = $"{_progress * 100:0}%";
        }
    }
}
