using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace EnigmaDisk.Core;

/// <summary>
/// Удаление программы, установленной через EnigmaDiskSetup.exe.
/// Windows вызывает «EnigmaDisk.exe --uninstall» из «Параметры → Приложения».
/// </summary>
public static class Uninstaller
{
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EnigmaDisk";

    public static string StartMenuShortcut => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Enigma Disk.lnk");

    public static string DesktopShortcut => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Enigma Disk.lnk");

    public static void Run(bool quiet)
    {
        if (!quiet)
        {
            var ok = MessageBox.Show(Loc.T("un.confirm"), "Enigma Disk", MessageBoxButton.YesNo,
                MessageBoxImage.Question, MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes) return;
        }

        bool deleteData = !quiet && MessageBox.Show(Loc.T("un.data"), "Enigma Disk", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

        // папку удаляем, только если программа запущена именно из установленной папки
        string? installDir = null;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(UninstallKey);
            installDir = k?.GetValue("InstallLocation") as string;
        }
        catch { }
        var exeDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";
        bool removeFolder = !string.IsNullOrEmpty(installDir) &&
                            string.Equals(System.IO.Path.GetFullPath(installDir).TrimEnd('\\'),
                                          System.IO.Path.GetFullPath(exeDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        try { Scheduler.Disable(out _); } catch { }
        TryDelete(StartMenuShortcut);
        TryDelete(DesktopShortcut);
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
        if (deleteData)
        {
            try { Directory.Delete(SnapshotStore.DataDir, true); } catch { }
        }

        if (!quiet)
            MessageBox.Show(Loc.T("un.done"), "Enigma Disk", MessageBoxButton.OK, MessageBoxImage.Information);

        if (removeFolder)
        {
            // сам себя exe удалить не может: просим cmd сделать это через пару секунд после выхода
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe",
                    $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{exeDir}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            }
            catch { }
        }
    }

    /// <summary>Если ежедневные снимки включены, а программу переустановили в другую папку, задание обновляется.</summary>
    public static void RefreshScheduledTask()
    {
        try
        {
            if (Scheduler.IsEnabled()) Scheduler.Enable(AppState.Settings.AutoHour, out _);
        }
        catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
