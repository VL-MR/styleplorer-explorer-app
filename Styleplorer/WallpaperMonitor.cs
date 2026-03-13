using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Styleplorer
{
    public class WallpaperMonitor
    {
        private static FileSystemWatcher? _wpeWatcher; // Наблюдатель за конфигурационным файлом Wallpaper Engine
        private static FileSystemWatcher? _windowsWatcher; // Наблюдатель за обоями Windows
        private static string? _lastWpeConfigHash; // Хеш последнего состояния конфигурационного файла Wallpaper Engine
        private static string? _lastWindowsWallpaperHash; // Хеш последнего состояния обоев Windows
        private static Window? _window; // Ссылка на главное окно приложения

        public static void Start(Window window)
        {
            _window = window;

            // Настройка наблюдателя для конфигурационного файла Wallpaper Engine
            string? wpeConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wallpaper Engine", "config.json");
            _wpeWatcher = new FileSystemWatcher(Path.GetDirectoryName(wpeConfigPath));
            _wpeWatcher.Filter = "config.json";
            _wpeWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _wpeWatcher.Changed += OnWpeConfigChanged;

            // Настройка наблюдателя для обоев Windows
            string? windowsWallpaperPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes");
            _windowsWatcher = new FileSystemWatcher(windowsWallpaperPath);
            _windowsWatcher.Filter = "TranscodedWallpaper";
            _windowsWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _windowsWatcher.Changed += OnWindowsWallpaperChanged;

            // Инициализация начальных хешей конфигурационных файлов
            _lastWpeConfigHash = GetFileHash(wpeConfigPath);
            _lastWindowsWallpaperHash = GetFileHash(DesktopColorAnalyzer.GetWallpaperPath());

            // Включение наблюдателей
            UpdateWatchers();
        }

        public static void Stop()
        {
            // Отключение и освобождение ресурсов наблюдателя Wallpaper Engine
            if (_wpeWatcher != null)
            {
                _wpeWatcher.EnableRaisingEvents = false;
                _wpeWatcher.Dispose();
            }

            // Отключение и освобождение ресурсов наблюдателя обоев Windows
            if (_windowsWatcher != null)
            {
                _windowsWatcher.EnableRaisingEvents = false;
                _windowsWatcher.Dispose();
            }

            _window = null;
        }

        private static void UpdateWatchers()
        {
            // Обновление состояния наблюдателей в зависимости от того, запущен ли Wallpaper Engine
            bool isWpeRunning = DesktopColorAnalyzer.IsWallpaperEngineRunning();
            _wpeWatcher.EnableRaisingEvents = isWpeRunning;
            _windowsWatcher.EnableRaisingEvents = !isWpeRunning;
        }

        private static void OnWpeConfigChanged(object sender, FileSystemEventArgs e)
        {
            // Обработка изменений конфигурационного файла Wallpaper Engine
            string? currentHash = GetFileHash(e.FullPath);
            if (currentHash != _lastWpeConfigHash)
            {
                _lastWpeConfigHash = currentHash;
                UpdateColors();
            }
        }

        private static void OnWindowsWallpaperChanged(object sender, FileSystemEventArgs e)
        {
            // Обработка изменений обоев Windows
            string? currentHash = GetFileHash(DesktopColorAnalyzer.GetWallpaperPath());
            if (currentHash != _lastWindowsWallpaperHash)
            {
                _lastWindowsWallpaperHash = currentHash;
                UpdateColors();
            }
        }

        private static void UpdateColors()
        {
            // Обновление цветов интерфейса на основе текущих обоев
            _window.Dispatcher.Invoke(() =>
            {
                DesktopColorAnalyzer.AnalyzeAndApplyColors(_window);
                UpdateWatchers();
            });
        }

        private static string GetFileHash(string path)
        {
            // Получение хеша файла для отслеживания изменений
            if (!File.Exists(path)) return string.Empty;

            using (var md5 = MD5.Create())
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[]? hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}
