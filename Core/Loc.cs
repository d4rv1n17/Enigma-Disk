using System.Globalization;
using System.Windows;

namespace EnigmaDisk.Core;

/// <summary>
/// Русский и английский интерфейс. Тексты в XAML берутся через {DynamicResource s.ключ},
/// поэтому язык меняется сразу, без перезапуска. В коде — Loc.T("ключ") и Loc.F("ключ", аргументы).
/// </summary>
public static class Loc
{
    public static string Lang { get; private set; } = "ru";
    public static bool En => Lang == "en";
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(En ? "en-US" : "ru-RU");

    /// <summary>Язык переключили: страницы перерисовывают тексты, собранные в коде.</summary>
    public static event Action? Changed;

    private static ResourceDictionary? _dict;

    public static string T(string key) => S.TryGetValue(key, out var v) ? (En ? v.en : v.ru) : key;

    public static string F(string key, params object?[] args) => string.Format(Culture, T(key), args);

    /// <summary>Слово в нужной форме: ключ хранит «файл|файла|файлов» и «file|files».</summary>
    public static string N(long n, string key)
    {
        var forms = T(key).Split('|');
        if (En || forms.Length < 3) return forms[n == 1 || forms.Length == 1 ? 0 : 1];
        long m = Math.Abs(n) % 100, m1 = m % 10;
        if (m > 10 && m < 20) return forms[2];
        if (m1 == 1) return forms[0];
        if (m1 > 1 && m1 < 5) return forms[1];
        return forms[2];
    }

    /// <summary>Число вместе со словом: «3 снимка», «1 file».</summary>
    public static string Count(long n, string key) => n.ToString("N0", Culture) + " " + N(n, key);

    public static string SystemDefault()
    {
        var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return two is "ru" or "uk" or "be" or "kk" or "ky" or "uz" ? "ru" : "en";
    }

    public static void Apply(string? lang)
    {
        Lang = lang == "en" ? "en" : lang == "ru" ? "ru" : SystemDefault();
        var d = new ResourceDictionary();
        foreach (var (k, v) in S) d["s." + k] = En ? v.en : v.ru;
        var merged = Application.Current.Resources.MergedDictionaries;
        if (_dict != null) merged.Remove(_dict);
        merged.Add(_dict = d);
        Changed?.Invoke();
    }

    private static readonly Dictionary<string, (string ru, string en)> S = new()
    {
        // общее
        ["app.tagline"] = ("что растёт на диске", "what grows on your disk"),
        ["app.version"] = ("версия 1.1", "version 1.1"),
        ["common.scan"] = ("Сканировать", "Scan"),
        ["common.scanDrive"] = ("Сканировать {0}", "Scan {0}"),
        ["common.open"] = ("Открыть", "Open"),
        ["common.openExplorer"] = ("Открыть в Проводнике", "Show in Explorer"),
        ["common.cancel"] = ("Отменить", "Cancel"),
        ["common.makeSnapshot"] = ("Сделать снимок", "Take a snapshot"),
        ["common.drive"] = ("Диск", "Drive"),
        ["common.localDisk"] = ("Локальный диск", "Local disk"),
        ["common.error"] = ("Что-то пошло не так:\n\n{0}", "Something went wrong:\n\n{0}"),
        ["common.waitScan"] = ("Дождитесь окончания сканирования.", "Please wait for the scan to finish."),
        ["unit.snapshot"] = ("снимок|снимка|снимков", "snapshot|snapshots"),
        ["unit.file"] = ("файл|файла|файлов", "file|files"),
        ["unit.day"] = ("день|дня|дней", "day|days"),
        ["unit.min"] = ("мин", "min"),
        ["unit.hour"] = ("ч", "h"),

        // время
        ["time.now"] = ("только что", "just now"),
        ["time.minAgo"] = ("{0} мин назад", "{0} min ago"),
        ["time.hoursAgo"] = ("{0} ч назад", "{0} h ago"),
        ["time.yesterday"] = ("вчера", "yesterday"),
        ["time.daysAgo"] = ("{0} назад", "{0} ago"),

        // меню
        ["nav.overview"] = ("Обзор", "Overview"),
        ["nav.growth"] = ("Что выросло", "What grew"),
        ["nav.files"] = ("Папки и файлы", "Folders & files"),
        ["nav.clean"] = ("Очистка", "Clean up"),
        ["nav.settings"] = ("Настройки", "Settings"),
        ["side.noSnapshots"] = ("Снимков пока нет", "No snapshots yet"),
        ["side.status"] = ("{0}\nпоследний {1}", "{0}\nlast one {1}"),

        // сканирование
        ["scan.title"] = ("Сканирую {0}", "Scanning {0}"),
        ["scan.stats"] = ("{0} · {1}", "{0} · {1}"),
        ["scan.hint"] = ("Только чтение: на диске ничего не меняется", "Read-only: nothing on the disk is changed"),

        // обзор
        ["ov.title"] = ("Обзор", "Overview"),
        ["ov.sub"] = ("Сколько места занято на дисках и что изменилось с прошлых снимков.",
                      "How much space your drives use and what changed since earlier snapshots."),
        ["ov.howTitle"] = ("Как это работает", "How it works"),
        ["ov.step1t"] = ("Снимок", "Snapshot"),
        ["ov.step1d"] = ("Приложение запоминает размер каждой папки на диске. Это занимает около минуты и ничего не меняет.",
                         "The app records the size of every folder on the disk. It takes about a minute and changes nothing."),
        ["ov.step2t"] = ("Сравнение", "Compare"),
        ["ov.step2d"] = ("Через день или неделю снимок повторяется, и видно, какие именно папки выросли.",
                         "A day or a week later another snapshot shows exactly which folders grew."),
        ["ov.step3t"] = ("Очистка", "Clean up"),
        ["ov.step3d"] = ("Если растёт кеш или временные файлы, их можно безопасно удалить в один клик.",
                         "If it's a cache or temporary files, you can safely remove them in one click."),
        ["ov.tipAuto"] = ("Совет: включите ежедневные снимки, и история будет собираться сама.",
                          "Tip: turn on daily snapshots and the history will build up by itself."),
        ["ov.tipAutoBtn"] = ("Включить", "Turn on"),
        ["ov.drives"] = ("Диски", "Drives"),
        ["ov.used"] = ("занято", "used"),
        ["ov.free"] = ("{0} свободно", "{0} free"),
        ["ov.of"] = ("из {0}", "of {0}"),
        ["ov.neverScanned"] = ("Ещё не сканировался", "Not scanned yet"),
        ["ov.scanning"] = ("Сканирование…", "Scanning…"),
        ["ov.snapAgo"] = ("Снимок {0}", "Snapshot {0}"),
        ["ov.change"] = ("{0} за {1}", "{0} in {1}"),
        ["ov.last"] = ("Последний снимок {0} · {1}", "Last snapshot {0} · {1}"),
        ["ov.chartTitle"] = ("Занятое место на {0}", "Used space on {0}"),
        ["ov.chartSub"] = ("{0} с {1}", "{0} since {1}"),
        ["ov.chartNeed2"] = ("График появится после второго снимка", "The chart appears after the second snapshot"),
        ["ov.topTitle"] = ("Что выросло на {0}", "What grew on {0}"),
        ["ov.topSub"] = ("С {0} · всего {1}", "Since {0} · total {1}"),
        ["ov.allChanges"] = ("Все изменения  →", "All changes  →"),
        ["ov.onlyOne"] = ("Пока есть только один снимок для сравнения. Сделайте ещё один позже, например завтра, и здесь появятся папки, которые выросли.",
                          "There is only one snapshot to compare so far. Take another one later, say tomorrow, and the folders that grew will appear here."),
        ["ov.nothingGrew"] = ("Ни одна папка заметно не выросла. Хорошая новость.", "No folder grew noticeably. Good news."),

        // рост
        ["gr.title"] = ("Что выросло", "What grew"),
        ["gr.sub"] = ("Сравнение двух снимков. В списке сами папки, которые выросли, а не вся цепочка их родителей.",
                      "A comparison of two snapshots. The list shows the folders that actually grew, not every parent above them."),
        ["gr.lDrive"] = ("Диск", "Drive"),
        ["gr.lPeriod"] = ("Сравнить с", "Compare with"),
        ["gr.lShow"] = ("Показать", "Show"),
        ["gr.day"] = ("Вчера", "Yesterday"),
        ["gr.week"] = ("Неделей ранее", "A week ago"),
        ["gr.month"] = ("Месяцем ранее", "A month ago"),
        ["gr.all"] = ("Первым снимком", "First snapshot"),
        ["gr.grew"] = ("Что выросло", "Grew"),
        ["gr.shrank"] = ("Что уменьшилось", "Shrank"),
        ["gr.sumUp"] = ("Занятое место на {0} выросло", "Used space on {0} grew"),
        ["gr.sumDown"] = ("Занятое место на {0} уменьшилось", "Used space on {0} shrank"),
        ["gr.sumSub"] = ("С {0} по {1} ({2}) · сейчас свободно {3}", "From {0} to {1} ({2}) · {3} free now"),
        ["gr.loading"] = ("Сравниваю снимки…", "Comparing snapshots…"),
        ["gr.clickHint"] = ("Нажмите на строку, чтобы посмотреть папку подробно.", "Click a row to explore the folder."),
        ["gr.noScanT"] = ("Диск {0} ещё не сканировался", "Drive {0} hasn't been scanned yet"),
        ["gr.noScanD"] = ("Сделайте первый снимок. Сравнение станет доступно после второго.", "Take the first snapshot. Comparison becomes available after the second."),
        ["gr.need2T"] = ("Нужен второй снимок", "A second snapshot is needed"),
        ["gr.need2D"] = ("Первый снимок сделан {0}. Сделайте ещё один позже, например завтра, и здесь появятся папки, которые выросли. Чтобы не вспоминать об этом, включите ежедневные снимки в настройках.",
                         "The first snapshot was taken {0}. Take another one later, for example tomorrow, and the folders that grew will appear here. To do it automatically, turn on daily snapshots in Settings."),
        ["gr.badT"] = ("Не удалось прочитать снимок", "Couldn't read the snapshot"),
        ["gr.badD"] = ("Файл снимка повреждён или удалён. Сделайте новый снимок.", "The snapshot file is damaged or missing. Take a new snapshot."),
        ["gr.noneUpT"] = ("Ничего заметно не выросло", "Nothing grew noticeably"),
        ["gr.noneDownT"] = ("Ничего заметно не уменьшилось", "Nothing shrank noticeably"),
        ["gr.noneD"] = ("Ни одна папка не изменилась больше чем на {0} МБ. Порог можно изменить в настройках.",
                        "No folder changed by more than {0} MB. You can change the threshold in Settings."),
        ["gr.new"] = ("новая · {0}", "new · {0}"),
        ["gr.gone"] = ("удалена · было {0}", "deleted · was {0}"),
        ["gr.cleanable"] = ("можно очистить", "can be cleaned"),
        ["gr.openTip"] = ("Открыть в Проводнике", "Show in Explorer"),

        // файлы
        ["fl.title"] = ("Папки и файлы", "Folders & files"),
        ["fl.sub"] = ("Чем занят диск на момент последнего снимка. Нажмите на папку, чтобы зайти внутрь.",
                      "What takes up the disk as of the last snapshot. Click a folder to open it."),
        ["fl.info"] = ("Снимок {0} · {1} · просканировано {2} за {3}", "Snapshot {0} · {1} · scanned {2} in {3}"),
        ["fl.folders"] = ("Папки", "Folders"),
        ["fl.bigFiles"] = ("Крупные файлы", "Large files"),
        ["fl.up"] = ("Вверх", "Up"),
        ["fl.colName"] = ("Название", "Name"),
        ["fl.colShare"] = ("Доля от папки", "Share of folder"),
        ["fl.colWeek"] = ("За неделю", "This week"),
        ["fl.colSize"] = ("Размер", "Size"),
        ["fl.colModified"] = ("Изменён", "Modified"),
        ["fl.rest"] = ("Файлы и мелкие папки", "Files and small folders"),
        ["fl.restSub"] = ("Файлы прямо в этой папке и вложенные папки меньше 1 МБ", "Files directly in this folder and subfolders under 1 MB"),
        ["fl.pct"] = ("{0:0.#}%", "{0:0.#}%"),
        ["fl.noScanT"] = ("Диск {0} ещё не сканировался", "Drive {0} hasn't been scanned yet"),
        ["fl.noScanD"] = ("Сделайте снимок, чтобы увидеть, чем занято место.", "Take a snapshot to see what takes up the space."),
        ["fl.noBigT"] = ("Крупных файлов нет", "No large files"),
        ["fl.noBigD"] = ("На диске нет файлов больше 50 МБ.", "There are no files over 50 MB on this drive."),
        ["fl.weekTip"] = ("Изменение за неделю", "Change over the week"),

        // очистка
        ["cl.title"] = ("Очистка", "Clean up"),
        ["cl.sub"] = ("Только известные кеши и временные файлы. Документы, игры и настройки не трогаются, а файлы, открытые программами, пропускаются.",
                      "Only well-known caches and temporary files. Documents, games and settings are never touched, and files in use are skipped."),
        ["cl.safe"] = ("Безопасно", "Safe"),
        ["cl.safeSub"] = ("Удаляется только то, что программы создадут заново сами.", "Only things that programs recreate on their own."),
        ["cl.careful"] = ("На ваше усмотрение", "Your call"),
        ["cl.carefulSub"] = ("Тоже безопасно, но есть последствия: прочитайте описание.", "Also safe, but with side effects: read the description."),
        ["cl.adminT"] = ("Часть пунктов требует прав администратора", "Some items need administrator rights"),
        ["cl.adminD"] = ("Без них системные папки Windows будут пропущены.", "Without them, Windows system folders are skipped."),
        ["cl.adminBtn"] = ("Перезапустить от администратора", "Restart as administrator"),
        ["cl.chipAdmin"] = ("нужны права администратора", "needs admin rights"),
        ["cl.counting"] = ("считаю…", "counting…"),
        ["cl.selected"] = ("Выбрано: {0}", "Selected: {0}"),
        ["cl.measuring"] = ("Считаю размер…", "Measuring…"),
        ["cl.pick"] = ("Отметьте, что очистить", "Pick what to clean"),
        ["cl.recalc"] = ("Пересчитать", "Recount"),
        ["cl.clean"] = ("Очистить выбранное", "Clean selected"),
        ["cl.confirmT"] = ("Очистка", "Clean up"),
        ["cl.confirm"] = ("Будет удалено около {0}:\n\n{1}\n\nУдалённые файлы не попадут в корзину. Продолжить?",
                          "About {0} will be removed:\n\n{1}\n\nDeleted files won't go to the Recycle Bin. Continue?"),
        ["cl.cleaning"] = ("Очищаю: {0}…", "Cleaning: {0}…"),
        ["cl.freed"] = ("Освобождено {0}", "Freed {0}"),
        ["cl.freedSkip"] = ("Освобождено {0} · пропущено занятых файлов: {1}", "Freed {0} · files in use skipped: {1}"),
        ["cl.done"] = ("Готово. Освобождено {0}.", "Done. Freed {0}."),
        ["cl.doneSkip"] = ("Готово. Освобождено {0}. Часть файлов занята программами: закройте их и повторите.",
                           "Done. Freed {0}. Some files are in use: close those programs and try again."),
        ["cl.aborted"] = ("Очистка прервана", "Cleanup stopped"),

        ["clean.usertemp.t"] = ("Временные файлы", "Temporary files"),
        ["clean.usertemp.d"] = ("Папка Temp вашего пользователя. Удаляются только файлы старше суток, чтобы не мешать работающим программам.",
                                "Your user Temp folder. Only files older than a day are removed, so running programs aren't affected."),
        ["clean.browsers.t"] = ("Кеш браузеров", "Browser cache"),
        ["clean.browsers.d"] = ("Chrome, Edge, Яндекс, Opera, Brave, Firefox. Пароли, история и вкладки не затрагиваются. Закройте браузер, чтобы очистить всё.",
                                "Chrome, Edge, Yandex, Opera, Brave, Firefox. Passwords, history and tabs stay. Close the browser to clean everything."),
        ["clean.apps.t"] = ("Кеш Discord и Teams", "Discord and Teams cache"),
        ["clean.apps.d"] = ("Картинки и временные данные мессенджеров. Переписка не удаляется. Закройте программы перед очисткой.",
                            "Images and temporary data of these messengers. Chats stay. Close the apps first."),
        ["clean.shaders.t"] = ("Кеш шейдеров видеокарты", "Graphics shader cache"),
        ["clean.shaders.d"] = ("NVIDIA, AMD, Intel и DirectX. Пересоздаётся сам, но первые минуты в играх возможны подлагивания.",
                               "NVIDIA, AMD, Intel and DirectX. It rebuilds itself, but games may stutter for the first few minutes."),
        ["clean.dumps.t"] = ("Дампы и отчёты об ошибках", "Crash dumps and error reports"),
        ["clean.dumps.d"] = ("Файлы, которые Windows и программы записывают при падениях. Нужны только разработчикам.",
                             "Files Windows and apps write when they crash. Only developers need them."),
        ["clean.wintemp.t"] = ("Временные файлы Windows", "Windows temporary files"),
        ["clean.wintemp.d"] = ("Системная папка Windows\\Temp. Только файлы старше суток.", "The system Windows\\Temp folder. Only files older than a day."),
        ["clean.updates.t"] = ("Скачанные обновления Windows", "Downloaded Windows updates"),
        ["clean.updates.d"] = ("Установочные файлы уже установленных обновлений и кеш оптимизации доставки. При необходимости Windows скачает их снова.",
                               "Setup files of already installed updates and the Delivery Optimization cache. Windows re-downloads them if needed."),
        ["clean.dev.t"] = ("Кеши разработчика", "Developer caches"),
        ["clean.dev.d"] = ("Gradle, npm, pip, Yarn и NuGet. Ничего не сломается, но следующая сборка проектов будет дольше: пакеты скачаются заново.",
                           "Gradle, npm, pip, Yarn and NuGet. Nothing breaks, but the next project build is slower: packages are downloaded again."),
        ["clean.recycle.t"] = ("Корзина", "Recycle Bin"),
        ["clean.recycle.d"] = ("Файлы из корзины на всех дисках будут удалены навсегда.", "Files in the Recycle Bin on all drives are deleted for good."),

        // удаление
        ["un.confirm"] = ("Удалить Enigma Disk с этого компьютера?", "Remove Enigma Disk from this computer?"),
        ["un.data"] = ("Удалить также историю снимков? Если оставить, после повторной установки история сохранится.",
                       "Also delete the snapshot history? If you keep it, the history will still be there after reinstalling."),
        ["un.done"] = ("Enigma Disk удалён.", "Enigma Disk has been removed."),

        // настройки
        ["st.title"] = ("Настройки", "Settings"),
        ["st.sub"] = ("Язык, автоматические снимки и хранение истории.", "Language, automatic snapshots and history."),
        ["st.lang"] = ("Язык", "Language"),
        ["st.langSub"] = ("Язык интерфейса меняется сразу.", "The interface switches right away."),
        ["st.auto"] = ("Ежедневные снимки", "Daily snapshots"),
        ["st.autoSub"] = ("Windows сама будет запускать Enigma Disk раз в день в фоне, без окна и с пониженным приоритетом. Если компьютер был выключен, снимок сделается при следующем включении.",
                          "Windows will run Enigma Disk once a day in the background, without a window and at low priority. If the PC was off, the snapshot is taken the next time it starts."),
        ["st.autoCheck"] = ("Делать снимок каждый день", "Take a snapshot every day"),
        ["st.time"] = ("Время запуска", "Start time"),
        ["st.drives"] = ("Диски", "Drives"),
        ["st.autoOn"] = ("Включено: каждый день в {0:00}:00, диски {1}.", "On: every day at {0:00}:00, drives {1}."),
        ["st.autoOff"] = ("Выключено. Снимки делаются только вручную.", "Off. Snapshots are taken manually only."),
        ["st.autoFail"] = ("Не удалось создать задание в Планировщике Windows:\n\n{0}", "Couldn't create the Windows Task Scheduler task:\n\n{0}"),
        ["st.compare"] = ("Сравнение", "Comparison"),
        ["st.compareSub"] = ("Мелкие изменения не показываются, чтобы список был коротким.", "Small changes are hidden to keep the list short."),
        ["st.minLabel"] = ("Показывать изменения от", "Show changes from"),
        ["st.mb"] = ("МБ", "MB"),
        ["st.store"] = ("Хранение снимков", "Snapshot history"),
        ["st.storeSub"] = ("Последние две недели хранятся все снимки, дальше по одному в день, а после двух месяцев по одному в неделю.",
                           "All snapshots from the last two weeks are kept, then one per day, and after two months one per week."),
        ["st.keepLabel"] = ("Хранить не дольше", "Keep for up to"),
        ["st.days"] = ("дней", "days"),
        ["st.storeInfo"] = ("{0} · {1} · {2}", "{0} · {1} · {2}"),
        ["st.openData"] = ("Открыть папку с данными", "Open data folder"),
        ["st.deleteAll"] = ("Удалить все снимки", "Delete all snapshots"),
        ["st.deleteConfirm"] = ("Удалить все снимки? История изменений начнётся заново.", "Delete all snapshots? The change history will start over."),
        ["st.about"] = ("О программе", "About"),
        ["st.aboutText"] = ("Enigma Disk 1.1. Всё хранится только на этом компьютере, в интернет ничего не отправляется. Сканирование только читает размеры файлов и ничего не меняет на диске.",
                            "Enigma Disk 1.1. Everything stays on this computer and nothing is sent online. Scanning only reads file sizes and changes nothing on the disk."),
    };
}
