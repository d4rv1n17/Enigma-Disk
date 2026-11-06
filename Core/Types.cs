using System.Text.Json.Serialization;

namespace EnigmaDisk.Core;

/// <summary>Категория файлов (видео, фото, музыка, архивы…). Размеры в снимке.</summary>
public sealed class TypeEntry
{
    [JsonPropertyName("n")] public int N { get; set; }         // индекс категории
    [JsonPropertyName("s")] public long S { get; set; }
    [JsonPropertyName("c")] public int C { get; set; }
}

/// <summary>Итог по расширению внутри категории.</summary>
public sealed class ExtEntry
{
    [JsonPropertyName("e")] public string E { get; set; } = "";
    [JsonPropertyName("n")] public int N { get; set; }
    [JsonPropertyName("s")] public long S { get; set; }
    [JsonPropertyName("c")] public int C { get; set; }
    [JsonIgnore] public string CategoryName => TypeCategories.Get(N)?.Name ?? "";
    [JsonIgnore] public string SizeText => Format.Size(S);
    [JsonIgnore] public string CountText => Format.Count(C);
    [JsonIgnore] public double Fraction { get; set; }
}

/// <summary>Список расширений, который знает приложение.</summary>
public static class TypeCategories
{
    // имя категории — ключ локализации s.cat.*, перевод берётся из Loc по текущему языку
    public static readonly (string Key, string Icon, string Color, string Exts)[] List =
    {
        ("cat.video",   "E714", "#8B5CF6", "mp4|mkv|avi|mov|wmv|flv|webm|m4v|mpg|mpeg|ts|3gp|mts|m2ts|vob|ogv|rm|rmvb|asf"),
        ("cat.photo",   "E722", "#EC4899", "jpg|jpeg|png|gif|bmp|webp|tiff|tif|heic|heif|avif|raw|cr2|cr3|nef|arw|orf|rw2|dng|psd|xcf|ai"),
        ("cat.music",   "E8D6", "#10B981", "mp3|flac|wav|ogg|oga|m4a|aac|wma|opus|aiff|alac|ape|wv|mka|mid|midi"),
        ("cat.archive", "E8B7", "#F59E0B", "zip|rar|7z|tar|gz|bz2|xz|zst|lz4|cab|iso|img|vhd|vhdx|mdf|arj|lzh|tgz"),
        ("cat.docs",    "E8A5", "#3B82F6", "pdf|doc|docx|xls|xlsx|ppt|pptx|odt|ods|odp|rtf|txt|md|csv|epub|djvu|fb2"),
        ("cat.progs",   "E719", "#06B6D4", "exe|msi|dll|sys|bat|cmd|ps1|jar|apk|appx|msix|msxb|deb|rpm|dmg|com"),
        ("cat.games",   "E7FC", "#F97316", "pak|wad|bsa|esm|esp|gpk|utoc|ucas|vpp_pc|arc|gcf|vdf"),
        ("cat.misc",    "E7C3", "#94A3B8", "")
    };

    /// <summary>Сопоставление нижнего расширения с индексом категории.</summary>
    public static Dictionary<string, int>? Map { get; private set; }

    public static void Build()
    {
        if (Map != null) return;
        Map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < List.Length; i++)
            foreach (var e in List[i].Exts.Split('|', StringSplitOptions.RemoveEmptyEntries))
                Map[e] = i;
    }

    public static int Index(string ext)
    {
        Build();
        if (ext.Length == 0) return List.Length - 1;
        return Map!.TryGetValue(ext, out var i) ? i : List.Length - 1;
    }

    public static (string Name, string Icon, string Color)? Get(int i) =>
        i >= 0 && i < List.Length ? (Loc.T(List[i].Key), List[i].Icon, List[i].Color) : null;
}

/// <summary>Категория типа файлов для интерфейса.</summary>
public sealed class CategoryVm
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Color { get; init; } = "#94A3B8";
    public long Size { get; init; }
    public int Count { get; init; }
    public double Fraction { get; set; }
    public string SizeText => Format.Size(Size);
    public string CountText => Format.Count(Count);
    public string PercentText => Total > 0 ? (Size * 100.0 / Total).ToString("0.#") + "%" : "";
    [JsonIgnore] public long Total { get; set; }
}

/// <summary>Снимок одного типа — рост категории.</summary>
public sealed class TypeRow
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Color { get; init; } = "#94A3B8";
    public long Old { get; init; }
    public long New { get; init; }
    public long Delta => New - Old;
    public string DeltaText => Format.Delta(Delta);
    public string FromTo => $"{Format.Size(Old)} → {Format.Size(New)}";
    public string SizeText => Format.Size(New);
    public double Fraction { get; set; }
}

/// <summary>Дубликаты файлов.</summary>
public sealed class DuplicateItem
{
    public string Path { get; init; } = "";
    public string Group { get; init; } = "";
    public long Size { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string SizeText => Format.Size(Size);
    public string Hint => Hints.Describe(Path);
    public string HintDot => Hint.Length > 0 ? Hint + " · " : "";
    public bool IsCleanable => Hints.IsCleanable(Path);
    public bool IsSelected { get; set; }
    public string DuplicateText => Loc.F("du.inGroups", GroupsCount);
    public int GroupsCount { get; set; }
}

public sealed class DuplicateGroup
{
    public string Hash { get; set; } = "";
    public long Size { get; set; }
    public List<string> Paths { get; set; } = new();
    public string Name => System.IO.Path.GetFileName(Paths.FirstOrDefault() ?? "");
    public string SizeText => Format.Size(Size);
    public string WasteText => Format.Size(Size * (Paths.Count - 1));
    public int Copies => Paths.Count;
    public string CopiesText => Loc.F("du.copies", Copies);
    public bool IsSelected { get; set; }
}

public sealed class DuplicateResult
{
    public List<DuplicateGroup> Groups { get; set; } = new();
    public long Wasted => Groups.Sum(g => g.Size * (g.Paths.Count - 1));
    public int Files => Groups.Sum(g => g.Paths.Count);
}

/// <summary>Группа исключений пользователя.</summary>
public sealed class ExclusionGroup
{
    public string Name { get; set; } = "";
    public string Root { get; set; } = "";
    public List<string> Paths { get; set; } = new();
    public int Count => Paths.Count;
}

/// <summary>Снимок исключений — для экспорта.</summary>
public sealed class ExclusionSnapshot
{
    public string Drive { get; set; } = "";
    public List<ExclusionGroup> Groups { get; set; } = new();
}

/// <summary>Прогресс дубликатов: фаза и текущая папка.</summary>
public sealed class DuplicateProgress
{
    public string Phase { get; set; } = "";
    public string Current { get; set; } = "";
    public int Files { get; set; }
    public int Groups { get; set; }
    public int Percent { get; set; }
}

/// <summary>Прогресс экспорта отчёта.</summary>
public sealed class ReportProgress
{
    public string Phase { get; set; } = "";
    public int Percent { get; set; }
    public string File { get; set; } = "";
}

/// <summary>Отчёт для экспорта.</summary>
public sealed class ExportReport
{
    public string Title { get; set; } = "";
    public DateTime Generated { get; set; } = DateTime.Now;
    public List<SnapshotInfo> History { get; set; } = new();
    public GrowthResult? Growth { get; set; }
    public List<TypeRow> Types { get; set; } = new();
    public DuplicateResult? Duplicates { get; set; }
    public SnapshotInfo? Latest { get; set; }
}

/// <summary>Информация о приложении для окна «О программе».</summary>
public static class About
{
    public const string Version = "2.0.0";
    public const string Name = "Enigma Disk";
    public static string Title => $"{Name} {Version}";
    public static string Subtitle => Loc.T("about.subtitle");
    public static string Author => Loc.T("about.author");
    public static string Url => "https://github.com/d4rv1n17/Enigma-Disk";
}
