using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace EnigmaDisk.Core;

/// <summary>Local ecosystem integration; no online account or DRM service.</summary>
public static class HubLink
{
    public static string? Executable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EnigmaHub");
        var folder = key?.GetValue("InstallLocation") as string;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            var registered = Path.Combine(folder, "EnigmaHub.exe");
            if (Supported(registered)) return registered;
        }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Enigma Hub", "EnigmaHub.exe");
        return Supported(fallback) ? fallback : null;
    }

    private static bool Supported(string exe)
    {
        try
        {
            using var marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(exe)!, ".enigma-hub-install.json")));
            return File.Exists(exe) && marker.RootElement.GetProperty("product").GetString() == "Enigma Hub" &&
                Version.TryParse(marker.RootElement.GetProperty("version").GetString(), out var version) && version >= new Version(1, 3, 0);
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException) { return false; }
    }

    public static void Open(string action = "open")
    {
        var exe = Executable() ?? throw new FileNotFoundException(
            "Install or update Enigma Hub (1.3.0 or newer): https://enigmastudioo.com");
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var arg in new[] { "--product", "disk", "--action", action,
                     "--product-exe", Environment.ProcessPath! }) info.ArgumentList.Add(arg);
        Process.Start(info);
    }

    public static bool ClaimLaunch()
    {
        var endpoint = Environment.GetEnvironmentVariable("ENIGMA_HUB_ENDPOINT");
        var ticket = Environment.GetEnvironmentVariable("ENIGMA_HUB_TICKET");
        Environment.SetEnvironmentVariable("ENIGMA_HUB_ENDPOINT", null);
        Environment.SetEnvironmentVariable("ENIGMA_HUB_TICKET", null);
        if (string.IsNullOrEmpty(ticket) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != "http" || uri.Host != "127.0.0.1" || uri.Port <= 0 ||
            uri.AbsolutePath != "/claim" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "") return false;
        try
        {
            using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var body = new StringContent(JsonSerializer.Serialize(new { product = "disk", ticket }), Encoding.UTF8, "application/json");
            using var response = http.PostAsync(uri, body).GetAwaiter().GetResult();
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException) { return false; }
    }

    public static bool EnsureLaunch()
    {
#if DEBUG
        return true; // Source development doesn't require an installed Hub.
#else
        if (ClaimLaunch()) return true;
        try { Open("launch"); }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(
                "Enigma Disk is part of Enigma Hub.\nInstall or restore Enigma Hub, then launch Disk again.\n\nhttps://enigmastudioo.com",
                "Enigma Hub required", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        return false;
#endif
    }
}
