using System.Windows;
using System.Windows.Controls;
using EnigmaDisk.Core;

namespace EnigmaDisk.Pages;

public partial class ExclusionsPage : UserControl, IPage
{
    public ExclusionsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public void OnShown() => Refresh();

    private void Refresh()
    {
        var list = AppState.Settings.Exclusions;
        List.ItemsSource = list;
        Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.T("ex.add"),
            Multiselect = false,
        };
        if (dlg.ShowDialog() != true) return;
        var path = dlg.FolderName.TrimEnd('\\');
        if (path.Length < 3) return;
        if (AppState.Settings.Exclusions.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(Window.GetWindow(this)!, Loc.T("ex.exists"), "Enigma Disk");
            return;
        }
        AppState.Settings.Exclusions.Add(path);
        AppState.SaveSettings();
        Refresh();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string path }) return;
        AppState.Settings.Exclusions.Remove(path);
        AppState.SaveSettings();
        Refresh();
    }
}
