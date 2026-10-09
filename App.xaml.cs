using System.Windows;
using System.Windows.Threading;
using EnigmaDisk.Core;

namespace EnigmaDisk;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppState.Init();

        if (e.Args.Any(a => a.Equals("--snapshot", StringComparison.OrdinalIgnoreCase)))
        {
            AppState.RunHeadless();
            Shutdown(0);
            return;
        }

        Loc.Apply(AppState.Settings.Language);

        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            Uninstaller.Run(quiet: e.Args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase)));
            Shutdown(0);
            return;
        }

        Task.Run(Uninstaller.RefreshScheduledTask);
        DispatcherUnhandledException += OnUnhandled;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var w = new MainWindow();
        MainWindow = w;
        w.Show();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var log = System.IO.Path.Combine(SnapshotStore.DataDir, "error.log");
            File.AppendAllText(log, $"[{DateTime.Now:u}] {e.Exception}\n\n");
        }
        catch { }
        MessageBox.Show(Loc.F("common.error", e.Exception.Message), "Enigma Disk",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
