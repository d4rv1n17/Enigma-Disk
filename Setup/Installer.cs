using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace EnigmaDiskSetup
{
    /// <summary>Установка для текущего пользователя: %LOCALAPPDATA%\Programs\Enigma Disk.</summary>
    public static class Installer
    {
        public const string AppName = "Enigma Disk";
        public const string Version = "1.1.0";
        public const string Publisher = "Enigma Studio";
        public const string ExeName = "EnigmaDisk.exe";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EnigmaDisk";

        public static string DefaultDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

        public static string StartMenuShortcut => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

        public static string DesktopShortcut => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnigmaDisk", "settings.json");

        /// <summary>Папка уже установленной версии, если есть.</summary>
        public static string InstalledDir()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                {
                    var dir = k?.GetValue("InstallLocation") as string;
                    return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, ExeName)) ? dir : null;
                }
            }
            catch { return null; }
        }

        public static long PayloadSize()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.exe"))
                return s?.Length ?? 0;
        }

        /// <summary>Устанавливает программу. report(шаг, доля 0..1) вызывается из фонового потока.</summary>
        public static void Install(string dir, bool desktop, Action<string, double> report)
        {
            var exe = Path.Combine(dir, ExeName);

            report("stepClose", 0.02);
            CloseRunning(exe);

            report("stepCopy", 0.05);
            Directory.CreateDirectory(dir);
            var tmp = exe + ".new";
            using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.exe"))
            {
                if (src == null) throw new InvalidOperationException("payload.exe not found");
                using (var dst = File.Create(tmp))
                {
                    var buf = new byte[1 << 20];
                    long total = src.Length, done = 0;
                    int n;
                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        dst.Write(buf, 0, n);
                        done += n;
                        report("stepCopy", 0.05 + 0.80 * done / Math.Max(1, total));
                    }
                }
            }
            ReplaceWithRetry(tmp, exe);

            report("stepShortcuts", 0.88);
            CreateShortcut(StartMenuShortcut, exe, dir);
            if (desktop) CreateShortcut(DesktopShortcut, exe, dir);
            else if (File.Exists(DesktopShortcut)) File.Delete(DesktopShortcut);

            report("stepRegister", 0.95);
            WriteLanguage(Text.Lang);
            Register(dir, exe);
            report("stepRegister", 1.0);
        }

        public static void Launch(string dir)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(dir, ExeName)) { UseShellExecute = true, WorkingDirectory = dir });
            }
            catch { }
        }

        private static void CloseRunning(string exe)
        {
            foreach (var p in Process.GetProcessesByName("EnigmaDisk"))
            {
                try
                {
                    string path = null;
                    try { path = p.MainModule?.FileName; } catch { }
                    if (path != null && !string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) continue;
                    p.CloseMainWindow();
                    if (!p.WaitForExit(4000)) { p.Kill(); p.WaitForExit(3000); }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        private static void ReplaceWithRetry(string tmp, string exe)
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    if (File.Exists(exe)) File.Delete(exe);
                    File.Move(tmp, exe);
                    return;
                }
                catch (IOException) when (i < 10) { Thread.Sleep(500); }
                catch (UnauthorizedAccessException) when (i < 10) { Thread.Sleep(500); }
            }
        }

        private static void CreateShortcut(string lnk, string target, string workDir)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnk));
            var type = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(type);
            try
            {
                dynamic sh = shell;
                dynamic s = sh.CreateShortcut(lnk);
                s.TargetPath = target;
                s.WorkingDirectory = workDir;
                s.IconLocation = target + ",0";
                s.Description = Text.T("shortcutDesc");
                s.Save();
                Marshal.FinalReleaseComObject(s);
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }

        private static void Register(string dir, string exe)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", Publisher);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + exe + "\" --uninstall");
                k.SetValue("QuietUninstallString", "\"" + exe + "\" --uninstall --quiet");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        /// <summary>Язык, выбранный в установщике, становится языком программы.</summary>
        private static void WriteLanguage(string lang)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                string json = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : "{}";
                var re = new Regex("\"Language\"\\s*:\\s*\"[^\"]*\"");
                if (re.IsMatch(json))
                    json = re.Replace(json, "\"Language\": \"" + lang + "\"", 1);
                else
                {
                    int i = json.IndexOf('{');
                    if (i < 0) json = "{}";
                    i = json.IndexOf('{');
                    bool empty = json.Substring(i + 1).Trim().StartsWith("}");
                    json = json.Insert(i + 1, "\n  \"Language\": \"" + lang + "\"" + (empty ? "\n" : ","));
                }
                File.WriteAllText(SettingsPath, json, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
