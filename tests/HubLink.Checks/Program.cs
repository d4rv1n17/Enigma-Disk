using System.IO;
using System.Reflection;
using System.Text.Json;
using EnigmaDisk.Core;

if (args.Contains("--claim"))
{
    bool claimed = HubLink.ClaimLaunch();
    if (Environment.GetEnvironmentVariable("ENIGMA_HUB_TICKET") != null ||
        Environment.GetEnvironmentVariable("ENIGMA_HUB_ENDPOINT") != null) return 2;
    Console.WriteLine(claimed ? "accepted" : "rejected");
    return 0;
}

var folder = Path.Combine(Path.GetTempPath(), "EnigmaDisk-HubLink-check-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    var exe = Path.Combine(folder, "EnigmaHub.exe");
    File.WriteAllText(exe, "test");
    var supported = typeof(HubLink).GetMethod("Supported", BindingFlags.Static | BindingFlags.NonPublic)!;
    foreach (var (version, expected) in new[] { ("1.2.3", false), ("1.3.0", true), ("1.4.0", true), ("bad", false) })
    {
        File.WriteAllText(Path.Combine(folder, ".enigma-hub-install.json"), JsonSerializer.Serialize(new { product = "Enigma Hub", version }));
        if ((bool)supported.Invoke(null, new object[] { exe })! != expected) throw new Exception("Version gate failed: " + version);
    }
    foreach (var endpoint in new[] { "https://example.com/claim", "http://127.0.0.1/claim?x=1", "http://user@127.0.0.1/claim" })
    {
        Environment.SetEnvironmentVariable("ENIGMA_HUB_ENDPOINT", endpoint);
        Environment.SetEnvironmentVariable("ENIGMA_HUB_TICKET", "forged");
        if (HubLink.ClaimLaunch()) throw new Exception("Remote or invalid endpoint accepted");
    }
    Console.WriteLine("HubLink checks passed");
    return 0;
}
finally
{
    foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
    Directory.Delete(folder);
}
