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
        ["app.version"] = ("версия 2.0.0", "version 2.0.0"),
        ["common.scan"] = ("Сканировать", "Scan"),
        ["common.scanDrive"] = ("Сканировать {0}", "Scan {0}"),
        ["common.open"] = ("Открыть", "Open"),
        ["common.openExplorer"] = ("Открыть в Проводнике", "Show in Explorer"),
        ["common.cancel"] = ("Отменить", "Cancel"),
        ["common.makeSnapshot"] = ("Сделать снимок", "Take a snapshot"),
        ["common.drive"] = ("Диск", "Drive"),
        ["common.localDisk"] = ("Локальный диск", "Local disk"),
        ["common.error"] = ("Что-то пошло не так:\n\n{0}", "Something went wrong:\n\n{0}"),
        ["scan.failed"] = ("Не удалось создать снимок", "Could not create snapshot"),
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
        ["ov.scanAll"] = ("Все диски", "All drives"),
        ["ov.scanAllTip"] = ("Последовательно создать снимки всех готовых дисков", "Create snapshots of all ready drives, one at a time"),
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
        ["fl.diskSize"] = ("на диске {0}", "allocated {0}"),
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
        ["st.startup"] = ("Запуск приложения", "App startup"),
        ["st.introSub"] = ("Короткая анимация при полном запуске приложения.", "A short animation when the app fully starts."),
        ["st.introCheck"] = ("Показывать анимированное интро", "Show animated intro"),
        ["intro.previewHint"] = ("Нажми, чтобы продолжить", "Click to continue"),
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
        ["st.diskSize"] = ("Измерять фактически занятое место", "Measure allocated disk space"),
        ["st.diskSizeSub"] = ("Включает оценку сжатия и разрежённых файлов. Сканирование станет медленнее; настройка применяется к новым снимкам.",
                              "Includes NTFS compression and sparse file allocation. Scans take longer; applies to new snapshots."),
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
        ["st.aboutText"] = ("Enigma Disk 2.0. Всё хранится только на этом компьютере, в интернет ничего не отправляется. Сканирование только читает размеры файлов и ничего не меняет на диске.",
                            "Enigma Disk 2.0. Everything stays on this computer and nothing is sent online. Scanning only reads file sizes and changes nothing on the disk."),

        // ---- 2.0: новые страницы и функции ----
        ["nav.space"] = ("Карта диска", "Disk map"),
        ["nav.types"] = ("Типы файлов", "File types"),
        ["nav.duplicates"] = ("Дубликаты", "Duplicates"),
        ["nav.exclude"] = ("Исключения", "Exclusions"),

        // Карта диска (treemap)
        ["tm.title"] = ("Карта диска", "Disk map"),
        ["tm.sub"] = ("Прямоугольники — папки и файлы. Чем больше площадь, тем больше места занимает. Клик по папке — внутрь, Esc — назад.",
                      "Each rectangle is a folder or file. Bigger area means more space. Click a folder to go inside, Esc to go back."),
        ["tm.up"] = ("Наверх", "Up one level"),
        ["tm.showFiles"] = ("Показывать файлы", "Show files"),
        ["tm.hideFiles"] = ("Скрыть файлы", "Hide files"),
        ["tm.empty"] = ("Снимка нет — сделайте его на вкладке «Обзор».", "No snapshot yet — take one on the Overview page."),
        ["tm.open"] = ("Открыть в Проводнике", "Show in Explorer"),
        ["tm.openHint"] = ("клик — открыть", "click to open"),
        ["tm.badge"] = ("Открыть →", "Open →"),
        ["tm.hint"] = ("Подсказка", "Hint"),
        ["tm.hintText"] = ("Размер плитки ≈ размер папки. Клик по папке — зайти внутрь, кнопка «Наверх» или Esc — вернуться. Клик по файлу — показать в Проводнике.",
                           "Tile size ≈ folder size. Click a folder to go inside, «Up» or Esc to go back. Click a file to show it in Explorer."),

        // Типы файлов
        ["ty.title"] = ("Типы файлов", "File types"),
        ["ty.sub"] = ("На что уходит место: видео, фото, музыка, архивы и остальные категории.",
                      "Where the space goes: video, photos, music, archives and other categories."),
        ["ty.total"] = ("Всего просканировано", "Total scanned"),
        ["ty.showExt"] = ("Расширения {0}", "{0} extensions"),
        ["ty.catFiles"] = ("{0} · {1}", "{0} · {1}"),
        ["ty.filesIn"] = ("Файлов: {0}", "{0} files"),
        ["ty.all"] = ("всем категориям", "all categories"),
        ["ty.showN"] = ("{0} расширений · {1}", "{0} extensions · {1}"),
        ["ty.noExt"] = ("Нет данных по расширениям в этом снимке.", "No extension data in this snapshot."),
        ["ty.allCats"] = ("все категории", "all categories"),
        ["cat.video"] = ("Видео", "Video"),
        ["cat.photo"] = ("Фото", "Photos"),
        ["cat.music"] = ("Музыка", "Music"),
        ["cat.archive"] = ("Архивы", "Archives"),
        ["cat.docs"] = ("Документы", "Documents"),
        ["cat.progs"] = ("Программы", "Programs"),
        ["cat.games"] = ("Игры", "Games"),
        ["cat.misc"] = ("Разное", "Miscellaneous"),

        // Дубликаты
        ["du.title"] = ("Дубликаты", "Duplicates"),
        ["du.sub"] = ("Файлы с одинаковым содержимым: фото, скачанные дважды, копии в загрузках и кешах. Проверяются файлы от 1 МБ, до 50 000 файлов на снимок. Оригинал остаётся нетронутым.",
                      "Files with identical content: photos saved twice, copies in Downloads and caches. Checks files from 1 MB, up to 50,000 files per snapshot. Originals are never touched."),
        ["du.find"] = ("Найти дубликаты", "Find duplicates"),
        ["du.stop"] = ("Остановить", "Stop"),
        ["du.scanning"] = ("Ищу дубликаты на {0}…", "Looking for duplicates on {0}…"),
        ["du.hashing"] = ("Сверяю содержимое…", "Comparing contents…"),
        ["du.found"] = ("Найдено {0} · лишних копий на {1}", "Found {0} · wasted copies {1}"),
        ["du.groups"] = ("Группы с копиями", "Groups with copies"),
        ["du.waste"] = ("лишних копий", "wasted copies"),
        ["du.copies"] = ("копий: {0}", "{0} copies"),
        ["du.noneT"] = ("Дубликатов не найдено", "No duplicates found"),
        ["du.noneD"] = ("На этом диске каждый файл уникален. Отличная новость.", "Every file on this drive is unique. Great news."),
        ["du.noSnap"] = ("Сначала сделайте снимок диска — поиск работает по последнему снимку.", "Take a snapshot first — the search runs on the latest snapshot."),
        ["du.selectFirst"] = ("Отметьте копии, которые нужно удалить.", "Select the copies you want to delete."),
        ["du.delete"] = ("Удалить выбранные", "Delete selected"),
        ["du.deleteN"] = ("Удалить ({0})", "Delete ({0})"),
        ["du.confirm"] = ("Удалить {0} файлов ({1})? Удаляется по одной копии в каждой группе, оригинал остаётся. Файлы не попадут в корзину.",
                          "Delete {0} files ({1})? One copy per group is removed, originals stay. Files won't go to the Recycle Bin."),
        ["du.done"] = ("Удалено {0} · освобождено {1}", "Deleted {0} · freed {1}"),
        ["du.keepOriginal"] = ("Оригинал", "Original"),
        ["du.cancelled"] = ("Поиск остановлен", "Search stopped"),

        // Исключения
        ["ex.title"] = ("Исключения", "Exclusions"),
        ["ex.sub"] = ("Папки, которые не сканируются и не попадают в снимки: сетевые бэкапы, диски виртуальных машин, рабочие архивы.",
                      "Folders that are never scanned and never appear in snapshots: network backups, VM disks, working archives."),
        ["ex.add"] = ("Добавить папку", "Add folder"),
        ["ex.remove"] = ("Удалить", "Remove"),
        ["ex.empty"] = ("Пока ничего не исключено. Все папки участвуют в снимках.", "Nothing is excluded yet. All folders are included in snapshots."),
        ["ex.exists"] = ("Эта папка уже в списке.", "This folder is already in the list."),
        ["ex.count"] = ("{0} путей", "{0} paths"),
        ["ex.hint"] = ("Исключение ускоряет сканирование и убирает лишний шум из сравнений.", "An exclusion speeds up scans and keeps comparisons noise-free."),

        // История папки
        ["fh.title"] = ("История «{0}»", "History of «{0}»"),
        ["fh.sub"] = ("Как менялся размер папки по всем снимкам.", "How this folder's size changed across all snapshots."),
        ["fh.noData"] = ("Папка появилась не во всех снимках — отсутствующие считаются нулём.", "The folder is missing from some snapshots — missing entries count as zero."),
        ["fh.back"] = ("Назад", "Back"),
        ["fh.current"] = ("Сейчас: {0}", "Now: {0}"),

        // Экспорт
        ["rp.title"] = ("Экспорт отчёта", "Export report"),
        ["rp.sub"] = ("HTML-отчёт: занятое место, что выросло, типы файлов и дубликаты. Открывается в браузере.",
                      "An HTML report: used space, growth, file types and duplicates. Opens in a browser."),
        ["rp.export"] = ("Создать отчёт", "Create report"),
        ["rp.done"] = ("Отчёт готов", "Report ready"),
        ["rp.open"] = ("Открыть отчёт", "Open report"),
        ["rp.saved"] = ("Сохранён в {0}", "Saved to {0}"),
        ["rp.noData"] = ("Нужен хотя бы один снимок.", "At least one snapshot is required."),
        ["rp.grew"] = ("Что выросло", "What grew"),
        ["rp.shrank"] = ("Что уменьшилось", "What shrank"),
        ["rp.duplicates"] = ("Дубликаты", "Duplicates"),
        ["rp.types"] = ("Типы файлов", "File types"),
        ["rp.period"] = ("с {0}", "since {0}"),
        ["rp.to"] = ("по", "to"),
        ["rp.usedSpace"] = ("Занятое место", "Used space"),
        ["rp.generated"] = ("Сгенерировано {0}", "Generated {0}"),
        ["rp.totalUsed"] = ("Занято", "Used"),
        ["rp.free"] = ("Свободно", "Free"),

        // Уведомления
        ["nt.nearFull"] = ("Диск {0} заполнен на {1}%", "Drive {0} is {1}% full"),
        ["nt.nearFullBody"] = ("Свободно всего {0}. Пора освободить место.", "Only {0} free. Time to free up some space."),
        ["nt.grew"] = ("{0} выросла на {1}", "{0} grew by {1}"),
        ["nt.grewBody"] = ("Быстрый рост папки. Нажмите, чтобы посмотреть, что произошло.", "Fast growth detected. Click to see what happened."),
        ["nt.settings"] = ("Уведомления", "Notifications"),
        ["nt.settingsSub"] = ("Предупреждать, когда диск заполнен и когда папка растёт слишком быстро.", "Warn when a drive is nearly full and when a folder grows too fast."),
        ["nt.nearFullCheck"] = ("Диск заполнен", "Drive nearly full"),
        ["nt.nearFullAt"] = ("Предупреждать от", "Warn from"),
        ["nt.growthCheck"] = ("Быстрый рост папок", "Fast folder growth"),
        ["nt.percent"] = ("%", "%"),
        ["nt.test"] = ("Проверить", "Test"),

        // О программе
        ["about.subtitle"] = ("Смотрите, что растёт на диске, и освобождайте место в один клик.", "See what grows on your disk and free up space in one click."),
        ["about.author"] = ("Автор: d4rv1n", "Author: d4rv1n"),
        ["about.openUrl"] = ("Открыть страницу проекта", "Open project page"),
        ["about.openUrlFail"] = ("Не удалось открыть ссылку. Проверьте, что в Windows настроен браузер для веб-ссылок.", "Couldn't open the link. Check that Windows has a default browser for web links."),
        ["about.checkUpdates"] = ("Проверить обновления", "Check for updates"),
        ["about.upToDate"] = ("У вас последняя версия {0}.", "You have the latest version {0}."),
        ["about.newVersion"] = ("Доступна версия {0}!", "Version {0} is available!"),
        ["about.download"] = ("Скачать", "Download"),
        ["about.checkFail"] = ("Не удалось проверить обновления.", "Couldn't check for updates."),
        ["about.releases"] = ("https://github.com/d4rv1n17/Enigma-Disk/releases/latest", "https://github.com/d4rv1n17/Enigma-Disk/releases/latest"),

        // Прочее 2.0
        ["common.today"] = ("сегодня", "today"),
        ["common.week"] = ("неделя", "week"),
        ["common.month"] = ("месяц", "month"),
        ["common.quarter"] = ("3 месяца", "3 months"),
        ["common.year"] = ("год", "year"),
        ["common.all"] = ("всё время", "all time"),
        ["gr.folderHistory"] = ("История папки", "Folder history"),
        ["fl.search"] = ("Поиск папки или файла…", "Search folders or files…"),
        ["fl.searchShortcut"] = ("Быстрый поиск: Ctrl+K", "Quick search: Ctrl+K"),
        ["sr.title"] = ("Результаты поиска", "Search results"),
        ["sr.folders"] = ("Папки", "Folders"),
        ["sr.files"] = ("Крупные файлы", "Large files"),
        ["sr.none"] = ("Ничего не найдено", "Nothing found"),
        ["sr.needScan"] = ("Сначала сделайте снимок.", "Take a snapshot first."),
        ["tm.legend"] = ("Условные обозначения", "Legend"),
        ["du.inGroups"] = ("в группах", "in groups"),

        // Приветствие и короткое обучение
        ["wc.hi"] = ("Привет! Это Enigma Disk", "Hi! This is Enigma Disk"),
        ["wc.intro"] = ("Приложение следит за дисками: делает снимки занятого места, показывает, что выросло, и помогает освободить место. Всё хранится только у вас на компьютере.",
                        "The app watches your drives: it takes snapshots of used space, shows what grew, and helps you free it up. Everything stays on your computer."),
        ["wc.q1t"] = ("Делайте снимки", "Take snapshots"),
        ["wc.q1d"] = ("Кнопка «Сделать снимок» на вкладке «Обзор» — или разрешите ежедневные автоматические в настройках. Снимок нужен, чтобы было с чем сравнивать.",
                      "Hit «Take snapshot» on the Overview tab — or enable daily automatic ones in Settings. Snapshots are what everything is compared against."),
        ["wc.q2t"] = ("Смотрите, что выросло", "See what grew"),
        ["wc.q2d"] = ("Вкладка «Что выросло» показывает папки, которые прибавили в размере. Карта диска и «Типы файлов» показывают, на что вообще уходит место.",
                      "The Growth tab shows folders that got bigger. The Disk map and File types tabs show what the space is spent on."),
        ["wc.q3t"] = ("Освобождайте место", "Free up space"),
        ["wc.q3d"] = ("Вкладка «Очистка» удаляет кеши и временные файлы в один клик, «Дубликаты» находит одинаковые копии, а отчёт можно выгрузить в HTML.",
                      "The Cleanup tab removes caches and temp files in one click, Duplicates finds identical copies, and a report can be exported to HTML."),
        ["wc.start"] = ("Начать", "Get started"),
        ["wc.snapshot"] = ("Сделать первый снимок", "Take first snapshot"),
        ["wc.dontShow"] = ("Больше не показывать", "Don't show again"),
        ["wc.openMap"] = ("Открыть карту диска", "Open disk map"),
        ["st.howTo"] = ("Как пользоваться", "How to use"),

        // Автообновление
        ["upd.title"] = ("Доступна версия {0}", "Version {0} is available"),
        ["upd.body"] = ("У вас {0}. Обновление ставится в один клик, настройки и история сохранятся.",
                        "You have {0}. The update is one click away — settings and history are kept."),
        ["upd.update"] = ("Обновить", "Update"),
        ["upd.openPage"] = ("Страница релиза", "Release page"),
        ["upd.later"] = ("Позже", "Later"),
        ["upd.download"] = ("Скачиваю…", "Downloading…"),
        ["upd.install"] = ("Устанавливаю…", "Installing…"),
        ["upd.restart"] = ("Сейчас перезапущусь с новой версией…", "Restarting with the new version…"),
        ["upd.fail"] = ("Не получилось: {0}", "Failed: {0}"),
        ["upd.done"] = ("Готово", "Done"),
        ["upd.latest"] = ("У вас последняя версия {0}.", "You have the latest version {0}."),
        ["upd.checking"] = ("Проверяю…", "Checking…"),
        ["upd.atStartup"] = ("Проверять обновления при запуске", "Check for updates at startup"),
        ["upd.now"] = ("Сейчас установлена версия {0}", "Currently installed: {0}"),
        ["nt.versionTitle"] = ("Обновление {0}", "Update {0}"),
        ["upd.title0"] = ("Обновления", "Updates"),
        ["upd.sub"] = ("Приложение само проверяет GitHub Releases и предлагает установку в один клик. Обновляется прямо из окна, без установщика.",
                       "The app checks GitHub Releases by itself and offers a one-click install. It updates right in the window, no installer needed."),
    };
}
