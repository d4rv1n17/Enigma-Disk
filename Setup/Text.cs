using System.Collections.Generic;
using System.Globalization;

namespace EnigmaDiskSetup
{
    /// <summary>Тексты установщика на русском и английском.</summary>
    public static class Text
    {
        public static bool En { get; set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "ru"
                                               && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "uk"
                                               && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "be";

        public static string Lang => En ? "en" : "ru";

        public static string T(string key) =>
            S.TryGetValue(key, out var v) ? (En ? v[1] : v[0]) : key;

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        private static readonly Dictionary<string, string[]> S = new Dictionary<string, string[]>
        {
            ["title"] = new[] { "Установка Enigma Disk", "Enigma Disk Setup" },
            ["header"] = new[] { "Установка", "Setup" },
            ["welcome"] = new[] { "Установить Enigma Disk {0}", "Install Enigma Disk {0}" },
            ["welcomeUpdate"] = new[] { "Обновить Enigma Disk до {0}", "Update Enigma Disk to {0}" },
            ["pitch"] = new[] { "Покажет, какие папки растут на диске, и поможет безопасно освободить место.",
                                "Shows which folders grow on your disk and helps free up space safely." },
            ["folder"] = new[] { "Папка установки", "Install location" },
            ["change"] = new[] { "Изменить…", "Change…" },
            ["pick"] = new[] { "Выберите папку для Enigma Disk", "Choose a folder for Enigma Disk" },
            ["desktop"] = new[] { "Ярлык на рабочем столе", "Desktop shortcut" },
            ["launch"] = new[] { "Запустить после установки", "Launch after setup" },
            ["note"] = new[] { "Нужно около {0}. Права администратора не нужны: программа ставится только для вас.",
                               "Needs about {0}. No administrator rights required: it installs just for you." },
            ["install"] = new[] { "Установить", "Install" },
            ["update"] = new[] { "Обновить", "Update" },
            ["cancel"] = new[] { "Отмена", "Cancel" },
            ["working"] = new[] { "Устанавливаю…", "Installing…" },
            ["stepClose"] = new[] { "Закрываю запущенную программу", "Closing the running app" },
            ["stepCopy"] = new[] { "Копирую файлы", "Copying files" },
            ["stepShortcuts"] = new[] { "Создаю ярлыки", "Creating shortcuts" },
            ["stepRegister"] = new[] { "Добавляю в список приложений Windows", "Adding to Windows apps list" },
            ["doneTitle"] = new[] { "Готово", "All set" },
            ["doneText"] = new[] { "Enigma Disk установлен. Он есть в меню «Пуск», а удалить его можно в «Параметры → Приложения».",
                                   "Enigma Disk is installed. Find it in the Start menu; remove it any time in Settings → Apps." },
            ["finish"] = new[] { "Готово", "Finish" },
            ["error"] = new[] { "Не удалось установить:\n\n{0}", "Setup failed:\n\n{0}" },
            ["busy"] = new[] { "Закройте Enigma Disk и попробуйте ещё раз.", "Please close Enigma Disk and try again." },
            ["shortcutDesc"] = new[] { "Что растёт на диске", "What grows on your disk" },
            ["mb"] = new[] { "{0} МБ", "{0} MB" },
        };
    }
}
