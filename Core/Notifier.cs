namespace EnigmaDisk.Core;

/// <summary>
/// Уведомления внутри приложения: баннер поверх интерфейса после снимка.
/// Системные тосты ненадёжны для неупакованных приложений, поэтому показываем свой баннер.
/// </summary>
public static class Notifier
{
    public sealed class Notification
    {
        public string Title { get; init; } = "";
        public string Body { get; init; } = "";
        /// <summary>Переход на страницу при клике (например, "clean").</summary>
        public string? Navigate { get; init; }
        public string Kind { get; init; } = "info";   // info | warn
    }

    /// <summary>Появилось уведомление (окно показывает баннер).</summary>
    public static event Action<Notification>? Raised;

    /// <summary>Показать баннер (страницы подписываются сами).</summary>
    public static void Raise(Notification n) => Raised?.Invoke(n);

    public static void NearFull(string drive, double pct, long freeBytes)
    {
        Raised?.Invoke(new Notification
        {
            Kind = "warn",
            Title = Loc.F("nt.nearFull", drive, (int)pct),
            Body = Loc.F("nt.nearFullBody", Format.Size(freeBytes)),
            Navigate = "clean",
        });
    }

    public static void Grew(string drive, string delta)
    {
        Raised?.Invoke(new Notification
        {
            Kind = "warn",
            Title = Loc.F("nt.grew", drive, delta),
            Body = Loc.T("nt.grewBody"),
            Navigate = "growth",
        });
    }
}
