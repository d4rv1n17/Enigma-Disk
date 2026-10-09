namespace EnigmaDisk.Core;

public static class Format
{
    private static readonly string[] UnitsRu = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
    private static readonly string[] UnitsEn = { "B", "KB", "MB", "GB", "TB" };

    public static string Size(long bytes)
    {
        var units = Loc.En ? UnitsEn : UnitsRu;
        var c = Loc.Culture;
        double v = Math.Abs((double)bytes);
        int u = 0;
        while (v >= 1000 && u < units.Length - 1) { v /= 1024; u++; }
        string num = u == 0 ? v.ToString("0", c) : v < 9.95 ? v.ToString("0.0", c) : v.ToString("0", c);
        return (bytes < 0 ? "−" : "") + num + " " + units[u];
    }

    /// <summary>Размер в ГБ с нужным числом знаков (для подписей графика).</summary>
    public static string Gb(double bytes, int decimals) =>
        (bytes / (1024.0 * 1024 * 1024)).ToString("F" + decimals, Loc.Culture) + " " + (Loc.En ? "GB" : "ГБ");

    public static string Delta(long bytes) => bytes >= 0 ? "+" + Size(bytes) : "−" + Size(-bytes);

    public static string Count(long n) => n.ToString("N0", Loc.Culture);

    public static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return Loc.T("time.now");
        if (d.TotalMinutes < 60) return Loc.F("time.minAgo", (int)d.TotalMinutes);
        if (d.TotalHours < 24) return Loc.F("time.hoursAgo", (int)d.TotalHours);
        if (d.TotalDays < 2) return Loc.T("time.yesterday");
        return Loc.F("time.daysAgo", Loc.Count((int)d.TotalDays, "unit.day"));
    }

    /// <summary>Длительность промежутка: «12 мин», «5 ч», «3 дня».</summary>
    public static string Span(TimeSpan d)
    {
        if (d.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(d.TotalMinutes))} {Loc.T("unit.min")}";
        if (d.TotalHours < 36) return $"{(int)Math.Round(d.TotalHours)} {Loc.T("unit.hour")}";
        return Loc.Count((int)Math.Round(d.TotalDays), "unit.day");
    }

    public static string When(DateTime t) => t.ToString(Loc.En ? "MMM d, HH:mm" : "d MMMM, HH:mm", Loc.Culture);
    public static string Date(DateTime t) => t.ToString(Loc.En ? "MMM d, yyyy" : "d MMM yyyy", Loc.Culture);

    public static string Duration(double sec) =>
        sec < 60 ? $"{sec:0} {(Loc.En ? "s" : "с")}"
                 : $"{(int)(sec / 60)} {Loc.T("unit.min")} {(int)(sec % 60)} {(Loc.En ? "s" : "с")}";
}
