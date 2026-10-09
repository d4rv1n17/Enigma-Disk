using System.Diagnostics;
using System.Security;

namespace EnigmaDisk.Core;

/// <summary>Ежедневные снимки через Планировщик заданий Windows (без фоновой службы).</summary>
public static class Scheduler
{
    public const string TaskName = "EnigmaDisk Daily Snapshot";

    public static bool IsEnabled() => Run(new[] { "/query", "/tn", TaskName }, out _) == 0;

    public static bool Enable(int hour, out string error)
    {
        var exe = Environment.ProcessPath ?? "";
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Ежедневный снимок занятого места на дисках (Enigma Disk)</Description>
              </RegistrationInfo>
              <Triggers>
                <CalendarTrigger>
                  <StartBoundary>2026-01-01T{hour:00}:00:00</StartBoundary>
                  <Enabled>true</Enabled>
                  <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>
                </CalendarTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>LeastPrivilege</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>true</StartWhenAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <ExecutionTimeLimit>PT2H</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(exe)}</Command>
                  <Arguments>--snapshot</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "enigmadisk_task.xml");
        File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
        try
        {
            int code = Run(new[] { "/create", "/tn", TaskName, "/xml", tmp, "/f" }, out error);
            return code == 0;
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    public static bool Disable(out string error) => Run(new[] { "/delete", "/tn", TaskName, "/f" }, out error) == 0;

    private static int Run(string[] args, out string output)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var err = p.StandardError.ReadToEnd();
            var outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            output = (err + outp).Trim();
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            output = ex.Message;
            return -1;
        }
    }
}
