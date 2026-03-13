using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Styleplorer
{
    public static class AppSettings
    {
        private static string settingsFilePath;
        public static bool ShowFileExtensions { get; set; } // Показать/скрыть расширения файлов
        public static bool ShowHiddenFiles { get; set; } // Показать/скрыть скрытые файлы
        public static bool ShowSystemFiles { get; set; } // Показать/скрыть системные файлы
        public static bool EnablePreview { get; set; } // Включить/выключить предпросмотр
        public static bool ConfirmDeletion { get; set; } // Подтверждение удаления
        public static bool OpenFilesWithSingleClick { get; set; } // Открывать файлы одним кликом (иначе двойным)
        public static bool RememberWindowSize { get; set; } // Запоминать размеры и позицию окна
        public static bool OpenLastActiveFolder { get; set; } // Открыть последнюю активную вкладку (не реализованно)
        public static double WindowWidth { get; set; } // Длинна окна
        public static double WindowHeight { get; set; } // Высота окна
        public static double WindowLeft { get; set; } // X позиция окна
        public static double WindowTop { get; set; } // Y позиция окна

        private static void SetSettingsFilePath()
        {
            // Устанавливаем путь к файлу настроек
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            settingsFilePath = Path.Combine(projectPath, "Settings.json");
        }

        public static void LoadSettings()
        {
            // Загружаем настройки из файла
            SetSettingsFilePath();

            if (File.Exists(settingsFilePath))
            {
                string jsonString = File.ReadAllText(settingsFilePath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);

                // Устанавливаем значения настроек из файла
                ShowFileExtensions = GetBoolValue(settings, "ShowFileExtensions", false);
                ShowHiddenFiles = GetBoolValue(settings, "ShowHiddenFiles", false);
                ShowSystemFiles = GetBoolValue(settings, "ShowSystemFiles", false);
                EnablePreview = GetBoolValue(settings, "EnablePreview", true);
                ConfirmDeletion = GetBoolValue(settings, "ConfirmDeletion", true);
                OpenFilesWithSingleClick = GetBoolValue(settings, "OpenFilesWithSingleClick", false);
                RememberWindowSize = GetBoolValue(settings, "RememberWindowSize", true);
                OpenLastActiveFolder = GetBoolValue(settings, "OpenLastActiveFolder", true);
                WindowWidth = GetDoubleValue(settings, "WindowWidth", 860);
                WindowHeight = GetDoubleValue(settings, "WindowHeight", 810);
                WindowLeft = GetDoubleValue(settings, "WindowLeft", 0);
                WindowTop = GetDoubleValue(settings, "WindowTop", 0);
            }
            else
            {
                // Устанавливаем значения настроек по умолчанию
                SetDefaultValues();
            }

            // Применяем настройки к UI
            ApplySettingsToUI();
        }

        private static bool GetBoolValue(Dictionary<string, JsonElement> settings, string key, bool defaultValue)
        {
            // Получаем булевое значение из настроек
            if (settings.TryGetValue(key, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.True)
                    return true;
                if (value.ValueKind == JsonValueKind.False)
                    return false;
            }
            return defaultValue;
        }

        private static double GetDoubleValue(Dictionary<string, JsonElement> settings, string key, double defaultValue)
        {
            // Получаем числовое значение из настроек
            if (settings.TryGetValue(key, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
            {
                return value.GetDouble();
            }
            return defaultValue;
        }

        private static void SetDefaultValues()
        {
            // Устанавливаем значения настроек по умолчанию
            ShowFileExtensions = false;
            ShowHiddenFiles = false;
            ShowSystemFiles = false;
            EnablePreview = true;
            ConfirmDeletion = true;
            OpenFilesWithSingleClick = false;
            RememberWindowSize = true;
            OpenLastActiveFolder = true;
            WindowWidth = 860;
            WindowHeight = 810;
            WindowLeft = 0;
            WindowTop = 0;
        }

        private static void ApplySettingsToUI()
        {
            // Применяем настройки к UI элементам
            MainWindow.Instance.ShowFileExtensionsCheckBox.IsChecked = ShowFileExtensions;
            MainWindow.Instance.ShowHiddenFilesCheckBox.IsChecked = ShowHiddenFiles;
            MainWindow.Instance.ShowSystemFilesCheckBox.IsChecked = ShowSystemFiles;
            MainWindow.Instance.EnablePreviewCheckBox.IsChecked = EnablePreview;
            MainWindow.Instance.ConfirmDeletionCheckBox.IsChecked = ConfirmDeletion;
            MainWindow.Instance.OpenFilesWithSingleClickCheckBox.IsChecked = OpenFilesWithSingleClick;
            MainWindow.Instance.RememberWindowSizeCheckBox.IsChecked = RememberWindowSize;
            //MainWindow.Instance.OpenLastActiveFolderCheckBox.IsChecked = OpenLastActiveFolder;
        }

        public static void SaveSettings()
        {
            // Сохраняем настройки в файл
            var settings = new Dictionary<string, object>
        {
            { "ShowFileExtensions", ShowFileExtensions },
            { "ShowHiddenFiles", ShowHiddenFiles },
            { "ShowSystemFiles", ShowSystemFiles },
            { "EnablePreview", EnablePreview },
            { "ConfirmDeletion", ConfirmDeletion },
            { "OpenFilesWithSingleClick", OpenFilesWithSingleClick },
            { "RememberWindowSize", RememberWindowSize },
            { "OpenLastActiveFolder", OpenLastActiveFolder },
            { "WindowWidth", WindowWidth },
            { "WindowHeight", WindowHeight },
            { "WindowLeft", WindowLeft },
            { "WindowTop", WindowTop }
        };

            string jsonString = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsFilePath, jsonString);
        }

        public static void ApplyWindowPosition(Window window)
        {
            // Применяем позицию и размер окна
            if (RememberWindowSize)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Width = WindowWidth;
                window.Height = WindowHeight;
                window.Left = WindowLeft;
                window.Top = WindowTop;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
    }
}
