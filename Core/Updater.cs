using System.Diagnostics;
using System.Text.Json;

namespace EnigmaDisk.Core;

/// <summary>
/// Автообновление через GitHub Releases: проверка версии, скачивание и замена exe.
/// Работает и для портативной версии, и для установленной (папка в %LOCALAPPDATA% доступна для записи).
/// </summary>
public static class Updater
{
    private const string Api = "https://api.github.com/repos/d4rv1n17/Enigma-Disk/releases/latest";
    private const string AssetName = "EnigmaDisk-portable.exe";

    /// <summary>Последний проверенный результат (для страницы настроек).</summary>
    public static Info? Cached { get; set; }

    public sealed class Info
    {
        public string Version { get; init; } = "";
        public string Notes { get; init; } = "";
        public string DownloadUrl { get; init; } = "";
        public string HtmlUrl { get; init; } = "";
        public bool IsNewer { get; init; }
    }

    /// <summary>Программа запущена из папки, куда ставил установщик.</summary>
    public static bool IsInstalled
    {
        get
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Uninstaller.UninstallKey);
                var dir = k?.GetValue("InstallLocation") as string;
                var exeDir = System.IO.Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";
                return !string.IsNullOrEmpty(dir) && string.Equals(
                    System.IO.Path.GetFullPath(dir).TrimEnd('\\'),
                    System.IO.Path.GetFullPath(exeDir).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }

    /// <summary>Проверяет последний релиз. null — обновлений нет. Бросает исключение при ошибке сети.</summary>
    public static async Task<Info?> CheckAsync()
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("EnigmaDisk");
        var json = await http.GetStringAsync(Api);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
        var ver = tag.TrimStart('v', 'V');
        bool newer = Version.TryParse(ver, out var latest) && Version.TryParse(About.Version, out var cur)
            && latest > cur;

        string? url = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.Equals(AssetName, StringComparison.OrdinalIgnoreCase))
                {
                    url = a.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }

        var info = new Info
        {
            Version = ver,
            Notes = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "",
            HtmlUrl = root.TryGetProperty("html_url", out var h) ? (h.GetString() ?? "") : "",
            DownloadUrl = url ?? "",
            IsNewer = newer,
        };
        Cached = info;
        return newer && url != null ? info : null;
    }

    /// <summary>Скачивает файл с прогрессом (доля 0..1).</summary>
    public static async Task Download(string url, string file, Action<double>? progress)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("EnigmaDisk");
        using var resp = await http.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;
        var tmp = file + ".part";
        await using (var src = await resp.Content.ReadAsStreamAsync())
        await using (var dst = File.Create(tmp))
        {
            var buf = new byte[1 << 16];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if (total > 0) progress?.Invoke((double)done / total);
            }
        }
        try { if (File.Exists(file)) File.Delete(file); } catch { }
        File.Move(tmp, file);
    }

    /// <summary>Заменяет работающий exe скачанным и запускает новую версию. Вызывающий после этого делает Shutdown.</summary>
    public static void Apply(string newFile)
    {
        var exe = Environment.ProcessPath;
        if (exe == null || !File.Exists(newFile)) throw new FileNotFoundException("Update file was not downloaded.", newFile);
        var old = exe + ".old";
        var pid = Environment.ProcessId;
        var quote = (string value) => "'" + value.Replace("'", "''") + "'";
        var script = $"$exe={quote(exe)}; $old={quote(old)}; $new={quote(Path.GetFullPath(newFile))}; $targetProcessId={pid}; " +
                     "Start-Sleep -Milliseconds 1200; while (Get-Process -Id $targetProcessId -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 200 }; " +
                     "try { if (!(Test-Path -LiteralPath $new)) { throw 'Downloaded update is missing' }; " +
                     "Move-Item -LiteralPath $new -Destination $exe -Force; Start-Process -FilePath $exe; " +
                     "Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue } " +
                     "catch { if ((Test-Path -LiteralPath $old) -and (Test-Path -LiteralPath $exe)) { Move-Item -LiteralPath $exe -Destination ($new + '.failed') -Force }; " +
                     "if ((Test-Path -LiteralPath $old) -and !(Test-Path -LiteralPath $exe)) { Move-Item -LiteralPath $old -Destination $exe -Force }; throw }";

        // Helper ждёт завершения текущего процесса и восстанавливает прежний файл при неудаче.
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var helper = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-EncodedCommand", encoded },
        });
        if (helper == null) throw new InvalidOperationException("Could not start the updater helper.");
        try
        {
            File.Move(exe, old, true);
        }
        catch
        {
            try { helper.Kill(); } catch { }
            helper.Dispose();
            throw;
        }
        helper.Dispose();
    }
}
