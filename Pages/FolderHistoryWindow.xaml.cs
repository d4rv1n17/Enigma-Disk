using System.Windows;
using System.Windows.Interop;
using EnigmaDisk.Core;
using EnigmaDisk.Controls;

namespace EnigmaDisk.Pages;

public partial class FolderHistoryWindow : Window
{
    public FolderHistoryWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.DarkTitleBar(new WindowInteropHelper(this).Handle, 0x00121212);
    }

    public static void ShowDialog(Window owner, string path, string drive)
    {
        var w = new FolderHistoryWindow { Owner = owner };
        w.TitleText.Text = Loc.F("fh.title", System.IO.Path.GetFileName(path.TrimEnd('\\')));
        w.SubText.Text = Loc.F("fh.sub", drive.TrimEnd('\\'));
        var pts = FolderHistory.Build(drive, path);
        var last = pts.LastOrDefault();
        if (last != null) w.NowText.Text = Loc.F("fh.current", Format.Size(last.Size));
        w.Hint.Visibility = pts.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        w.Hint.Text = pts.Count == 0 ? Loc.T("tm.empty") : "";
        w.Chart.SetData(pts.Select(p => (p.Time, p.Size)));
        w.ShowDialog();
    }
}
