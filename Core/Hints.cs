namespace EnigmaDisk.Core;

/// <summary>Пояснения к известным папкам на двух языках. Cleanable — папку чистит раздел «Очистка».</summary>
public static class Hints
{
    private sealed record Rule(string Pattern, string Ru, string En, bool Cleanable = false);

    private static readonly Rule[] Rules =
    {
        new(@"\appdata\local\temp", "Временные файлы программ", "Temporary files of programs", true),
        new(@"\windows\temp", "Временные файлы Windows", "Windows temporary files", true),
        new(@"\softwaredistribution\download", "Скачанные обновления Windows", "Downloaded Windows updates", true),
        new(@"\deliveryoptimization", "Кеш доставки обновлений Windows", "Windows update delivery cache", true),
        new(@"\windows\winsxs", "Хранилище компонентов Windows, вручную не удалять", "Windows component store, don't delete by hand"),
        new(@"\windows\installer", "Установщики программ, вручную не удалять", "Program installers, don't delete by hand"),
        new(@"\nvidia\dxcache", "Кеш шейдеров NVIDIA", "NVIDIA shader cache", true),
        new(@"\nvidia\glcache", "Кеш шейдеров NVIDIA (OpenGL)", "NVIDIA shader cache (OpenGL)", true),
        new(@"\amd\dxcache", "Кеш шейдеров AMD", "AMD shader cache", true),
        new(@"\amd\dxccache", "Кеш шейдеров AMD", "AMD shader cache", true),
        new(@"\amd\vkcache", "Кеш шейдеров AMD (Vulkan)", "AMD shader cache (Vulkan)", true),
        new(@"\d3dscache", "Кеш шейдеров DirectX", "DirectX shader cache", true),
        new(@"\steamapps\shadercache", "Кеш шейдеров Steam", "Steam shader cache"),
        new(@"\steamapps\downloading", "Незавершённые загрузки Steam", "Unfinished Steam downloads"),
        new(@"\steamapps\common", "Установленные игры Steam", "Installed Steam games"),
        new(@"\epic games", "Игры Epic Games", "Epic Games titles"),
        new(@"\user data\default\cache", "Кеш браузера", "Browser cache", true),
        new(@"\google\chrome\user data", "Профиль Chrome: кеш, история, расширения", "Chrome profile: cache, history, extensions"),
        new(@"\microsoft\edge\user data", "Профиль Edge: кеш, история, расширения", "Edge profile: cache, history, extensions"),
        new(@"\yandex\yandexbrowser", "Профиль Яндекс Браузера", "Yandex Browser profile"),
        new(@"\mozilla\firefox", "Профиль Firefox", "Firefox profile"),
        new(@"\telegram desktop\tdata", "Кеш и медиа Telegram, чистится в настройках Telegram", "Telegram cache and media, clean it in Telegram settings"),
        new(@"\discord\cache", "Кеш Discord", "Discord cache", true),
        new(@"\crashdumps", "Дампы падений программ", "Program crash dumps", true),
        new(@"\windows\wer", "Отчёты об ошибках Windows", "Windows error reports", true),
        new(@"\.gradle", "Кеш Gradle (Android, Java)", "Gradle cache (Android, Java)", true),
        new(@"\.m2\repository", "Кеш Maven", "Maven cache"),
        new(@"\.nuget\packages", "Кеш пакетов NuGet", "NuGet package cache", true),
        new(@"\nuget\v3-cache", "Кеш NuGet", "NuGet cache", true),
        new(@"\npm-cache", "Кеш npm", "npm cache", true),
        new(@"\node_modules", "Зависимости Node.js проекта", "Node.js project dependencies"),
        new(@"\pip\cache", "Кеш pip", "pip cache", true),
        new(@"\.android\avd", "Эмуляторы Android", "Android emulators"),
        new(@"\android\sdk", "Android SDK", "Android SDK"),
        new(@"\unity\cache", "Кеш Unity", "Unity cache"),
        new(@"\asset store-5.x", "Скачанные ассеты Unity Asset Store", "Downloaded Unity Asset Store assets"),
        new(@"\library\packagecache", "Кеш пакетов Unity-проекта", "Unity project package cache"),
        new(@"\.vs\", "Служебные файлы Visual Studio", "Visual Studio working files"),
        new(@"\bin\release", "Результат сборки проекта", "Project build output"),
        new(@"\bin\debug", "Результат сборки проекта", "Project build output"),
        new(@"\$recycle.bin", "Корзина", "Recycle Bin", true),
        new(@"\downloads", "Загрузки", "Downloads"),
        new(@"\onedrive", "Файлы OneDrive, сохранённые на диске", "OneDrive files kept on disk"),
        new(@"\docker", "Образы и тома Docker", "Docker images and volumes"),
        new(@"\wsl", "Диски WSL", "WSL disks"),
        new(@"\packages\microsoft", "Данные приложений из Microsoft Store", "Microsoft Store app data"),
        new(@"\logs", "Логи", "Logs"),
        new(@"\cache", "Кеш программы", "App cache"),
    };

    private static Rule? Find(string path)
    {
        var p = path.ToLowerInvariant() + "\\";
        foreach (var r in Rules)
            if (p.Contains(r.Pattern.EndsWith('\\') ? r.Pattern : r.Pattern + "\\")) return r;
        return null;
    }

    public static string Describe(string path) => Find(path) is { } r ? (Loc.En ? r.En : r.Ru) : "";

    public static bool IsCleanable(string path) => Find(path)?.Cleanable == true;
}
