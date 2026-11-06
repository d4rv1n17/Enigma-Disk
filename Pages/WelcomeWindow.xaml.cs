using System.Windows;
using System.Windows.Interop;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.DarkTitleBar(new WindowInteropHelper(this).Handle, 0x00121212);
    }

    /// <summary>Показывает окно при первом запуске. True — пользователь нажал «Сделать первый снимок».</summary>
    public static bool ShowOnFirstRun(Window owner)
    {
        if (AppState.Settings.WelcomeShown) return false;
        var w = new WelcomeWindow { Owner = owner };
        w.ShowDialog();
        return w._scan;
    }

    /// <summary>Повторный показ из настроек — без автоснимка.</summary>
    public static void ShowAgain(Window owner)
    {
        var w = new WelcomeWindow { Owner = owner };
        w.DontShow.Visibility = Visibility.Collapsed;
        w.ShowDialog();
    }

    private bool _scan;

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        Finish();
        Close();
    }

    private void Snapshot_Click(object sender, RoutedEventArgs e)
    {
        _scan = true;
        Finish();
        Close();
    }

    private void Finish()
    {
        if (DontShow.IsChecked == true)
        {
            AppState.Settings.WelcomeShown = true;
            AppState.SaveSettings();
        }
    }
}
