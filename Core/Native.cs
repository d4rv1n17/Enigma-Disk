using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EnigmaDisk.Core;

public static class Native
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint flags);

    /// <summary>Тёмный заголовок окна (Windows 10 20H1+ и Windows 11) и цвет заголовка на Windows 11.</summary>
    public static void DarkTitleBar(IntPtr hwnd, int captionColorBgr)
    {
        int on = 1;
        if (DwmSetWindowAttribute(hwnd, 20, ref on, 4) != 0)
            DwmSetWindowAttribute(hwnd, 19, ref on, 4);
        int color = captionColorBgr;
        DwmSetWindowAttribute(hwnd, 35, ref color, 4);   // DWMWA_CAPTION_COLOR, только Windows 11
        int text = 0x00F2F2F2;
        DwmSetWindowAttribute(hwnd, 36, ref text, 4);    // DWMWA_TEXT_COLOR
    }

    public static (long bytes, long items) RecycleBinSize()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? (info.i64Size, info.i64NumItems) : (0, 0);
    }

    public static bool EmptyRecycleBin()
    {
        const uint noConfirmation = 0x1, noProgress = 0x2, noSound = 0x4;
        int hr = SHEmptyRecycleBin(IntPtr.Zero, null, noConfirmation | noProgress | noSound);
        return hr == 0 || hr == unchecked((int)0x8000FFFF); // E_UNEXPECTED = корзина уже пуста
    }

    public static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool RestartAsAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch { return false; } // пользователь отказался в окне UAC
    }

    public static void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }

    public static void ShowInExplorer(string file)
    {
        try
        {
            if (File.Exists(file))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
            else
                OpenFolder(System.IO.Path.GetDirectoryName(file) ?? "");
        }
        catch { }
    }
}
