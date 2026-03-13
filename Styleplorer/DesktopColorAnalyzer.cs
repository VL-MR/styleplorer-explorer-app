using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Styleplorer
{
    public class DesktopColorAnalyzer
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, StringBuilder lpvParam, int fuWinIni);

        private const int SPI_GETDESKWALLPAPER = 0x0073;

        public static void AnalyzeAndApplyColors(Window window)
        {
            string wallpaperPath;
            if (IsWallpaperEngineRunning())
            {
                wallpaperPath = GetWallpaperEngineWallpaperPath();
            }
            else
            {
                wallpaperPath = GetWallpaperPath();
            }

            if (!string.IsNullOrEmpty(wallpaperPath))
            {
                wallpaperPath = GetProcessedWallpaperPath(wallpaperPath);
                var colors = GetColors(wallpaperPath);
                ApplyColorScheme(window, colors.Dominant, colors.Shades, colors.Opposite);
            }
        }

        public static bool IsWallpaperEngineRunning()
        {
            return Process.GetProcesses().Any(p => p.ProcessName.ToLower().Contains("wallpaper64") || p.ProcessName.ToLower().Contains("wallpaper32")); // Это перенести в Мониоринг
        }

        public static string GetWallpaperEngineWallpaperPath()
        {
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wallpaper Engine", "config.json");
            
            if (File.Exists(configPath))
            {
                string jsonContent = File.ReadAllText(configPath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = document.RootElement;

                    var userProperty = root.EnumerateObject().FirstOrDefault(prop => !prop.Name.StartsWith("?"));

                    if (userProperty.Value.ValueKind == JsonValueKind.Object &&
                        userProperty.Value.TryGetProperty("general", out JsonElement general) &&
                        general.TryGetProperty("wallpaperconfig", out JsonElement wallpaperConfig) &&
                        wallpaperConfig.TryGetProperty("selectedwallpapers", out JsonElement selectedWallpapers) &&
                        selectedWallpapers.TryGetProperty(MainWindow.Instance.selectedMonitor, out JsonElement monitor0) &&
                        monitor0.TryGetProperty("file", out JsonElement file))
                    {
                        return file.GetString();
                    }
                }
            }

            return null;
        }

        //public static string GetWallpaperPath()
        //{
        //    string wallpaperPath = new string('\0', 260);
        //    SystemParametersInfo(SPI_GETDESKWALLPAPER, wallpaperPath.Length, wallpaperPath, 0);
        //    return wallpaperPath.Trim('\0');
        //}
        public static string GetWallpaperPath()
        {
            // Пробуем через стандартный API
            var sb = new System.Text.StringBuilder(260);
            SystemParametersInfo(SPI_GETDESKWALLPAPER, sb.Capacity, sb, 0);
            string path = sb.ToString();

            // Если путь существует — отлично
            if (File.Exists(path))
                return path;

            // Иначе берём TranscodedWallpaper (текущие реальные обои)
            string transcoded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper"
            );

            if (File.Exists(transcoded))
                return transcoded;

            // Иногда кэш лежит здесь (особенно в Windows 11)
            string cachedDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\CachedFiles"
            );

            if (Directory.Exists(cachedDir))
            {
                var latest = new DirectoryInfo(cachedDir)
                    .GetFiles()
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();
                if (latest != null)
                    return latest.FullName;
            }

            return null; // ничего не нашли
        }

        private static string GetProcessedWallpaperPath(string wallpaperPath)
        {
            string directory = Path.GetDirectoryName(wallpaperPath);
            string fileName = Path.GetFileNameWithoutExtension(wallpaperPath);

            // Проверяем наличие preview-файла
            string previewPath = Directory.GetFiles(directory, "preview.*")
                                          .FirstOrDefault(f => IsImageFile(f));

            if (previewPath != null)
            {
                return previewPath;
            }

            // Если это видео, извлекаем кадр
            if (Path.GetExtension(wallpaperPath).ToLower() == ".mp4")
            {
                string framePath = ExtractFrameFromVideo(wallpaperPath);
                return framePath;
            }

            // Возвращаем оригинальный путь, если это изображение
            return wallpaperPath;
        }

        private static bool IsImageFile(string path)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            return imageExtensions.Contains(Path.GetExtension(path).ToLower());
        }

        private static string ExtractFrameFromVideo(string videoPath)
        {
            var inputFile = new MediaFile { Filename = videoPath };
            var outputFile = new MediaFile { Filename = Path.Combine(Path.GetDirectoryName(videoPath), Path.GetFileNameWithoutExtension(videoPath) + "_frame.jpg") };

            using (var engine = new Engine())
            {
                engine.GetMetadata(inputFile);

                var options = new ConversionOptions { Seek = TimeSpan.FromSeconds(1) };
                engine.GetThumbnail(inputFile, outputFile, options);
            }

            return outputFile.Filename;
        }

        private static (Color Dominant, Color[] Shades, Color Opposite) GetColors(string imagePath)
        {
            using (var bitmap = SKBitmap.Decode(imagePath))
            {
                var pixels = bitmap.Pixels;
                var colorCounts = new Dictionary<SKColor, int>();

                // Квантизация и подсчет цветов
                foreach (var pixel in pixels)
                {
                    var quantizedColor = QuantizeColor(pixel);
                    if (!colorCounts.ContainsKey(quantizedColor))
                    {
                        colorCounts[quantizedColor] = 0;
                    }
                    colorCounts[quantizedColor]++;
                }

                // Игнорируем очень темные и очень светлые цвета
                var significantColors = colorCounts.Where(kv => !IsExtremeColor(kv.Key)).ToList();

                // Доминирующий цвет (наибольшая площадь)
                var dominantColor = significantColors.OrderByDescending(kv => kv.Value).First().Key;

                // Определяем, светлый ли доминирующий цвет
                bool isLightColor = IsLightColor(dominantColor);

                // Создаем 5 оттенков
                Color[] shades = new Color[5];
                for (int i = 0; i < 5; i++)
                {
                    double factor = (i + 1) * 0.05; // 10%, 20%, 30%, 40%, 50%
                    if (isLightColor)
                    {
                        shades[i] = SKColorToWpfColor(DarkenColor(dominantColor, factor));
                    }
                    else
                    {
                        shades[i] = SKColorToWpfColor(LightenColor(dominantColor, factor));
                    }
                }

                // Создаем противоположный цвет
                Color oppositeColor = SKColorToWpfColor(GetOppositeColor(dominantColor));

                return (SKColorToWpfColor(dominantColor), shades, oppositeColor);
            }
        }

        private static bool IsLightColor(SKColor color)
        {
            return (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue) / 255 > 0.5;
        }

        private static SKColor LightenColor(SKColor color, double factor)
        {
            return new SKColor(
                (byte)Math.Min(255, color.Red + (255 - color.Red) * factor),
                (byte)Math.Min(255, color.Green + (255 - color.Green) * factor),
                (byte)Math.Min(255, color.Blue + (255 - color.Blue) * factor)
            );
        }

        private static SKColor DarkenColor(SKColor color, double factor)
        {
            return new SKColor(
                (byte)(color.Red * (1 - factor)),
                (byte)(color.Green * (1 - factor)),
                (byte)(color.Blue * (1 - factor))
            );
        }

        private static SKColor GetOppositeColor(SKColor color)
        {
            return new SKColor(
                (byte)(255 - color.Red),
                (byte)(255 - color.Green),
                (byte)(255 - color.Blue)
            );
        }

        private static SKColor QuantizeColor(SKColor color)
        {
            int factor = 8;//32
            return new SKColor(
                (byte)((color.Red / factor) * factor),
                (byte)((color.Green / factor) * factor),
                (byte)((color.Blue / factor) * factor)
            );
        }

        private static bool IsExtremeColor(SKColor color)
        {
            int threshold = 30;
            return (color.Red < threshold && color.Green < threshold && color.Blue < threshold) ||
                   (color.Red > 255 - threshold && color.Green > 255 - threshold && color.Blue > 255 - threshold);
        }

        private static Color SKColorToWpfColor(SKColor color)
        {
            return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
        }

        private static void ApplyColorScheme(Window window, Color dominantColor, Color[] shades, Color oppositeColor)
        {
            window.Resources["PrimaryBackgroundColor"] = dominantColor;
            window.Resources["SecondaryBackgroundColor"] = shades[2]; // Средний оттенок
            window.Resources["AccentColor"] = oppositeColor; // Противоположный цвет

            window.Resources["Shade1"] = shades[0];
            window.Resources["Shade2"] = shades[1];
            window.Resources["Shade3"] = shades[2];
            window.Resources["Shade4"] = shades[3];
            window.Resources["Shade5"] = shades[4];

            UpdateStyles(window);
        }

        private static void UpdateStyles(Window window)
        {
            window.Resources["TextColorBrush"] = new SolidColorBrush((Color)window.Resources["AccentColor"]);
            window.Resources["BorderColorBrush"] = new SolidColorBrush((Color)window.Resources["Shade5"]);
            window.Resources["WindowBorderColorBrush"] = new SolidColorBrush((Color)window.Resources["Shade5"]);
            window.Resources["RectangleBrush"] = new SolidColorBrush((Color)window.Resources["Shade1"]);
            window.Resources["SideMenuBrush"] = new SolidColorBrush((Color)window.Resources["Shade2"]);
            window.Resources["MainGridBrush"] = new SolidColorBrush((Color)window.Resources["Shade4"]);
            window.Resources["TopPanelBrush"] = new SolidColorBrush((Color)window.Resources["Shade3"]);
            window.Resources["BottomPanelBrush"] = new SolidColorBrush((Color)window.Resources["Shade3"]);
            window.Resources["RectangleBaseColor"] = (Color)window.Resources["AccentColor"];
            window.Resources["FolderColor"] = (Color)window.Resources["Shade1"];
            MainWindow.SelectedFolderColor = (Color)window.Resources["Shade1"];
            UpdateHighlightColors(window);
            UpdateButtonBackgrounds();
            MainWindow.Instance.SetFoldersColor();
        }

        public static void ResetStyles(Window window)
        {
            try
            {
                string[] keys = { "TextColorBrush", "BorderColorBrush", "WindowBorderColorBrush",
                          "RectangleBrush", "SideMenuBrush", "MainGridBrush", "TopPanelBrush", "BottomPanelBrush", "RectangleBaseColor",
                "FolderColor" };

                foreach (var key in keys)
                {
                    string defaultKey = "Default" + key;
                    //if (window.Resources.Contains(defaultKey) && window.Resources[defaultKey] is Brush defaultBrush)
                    //{
                    //    window.Resources[key] = defaultBrush;
                    //}
                    if (window.Resources.Contains(defaultKey))
                    {
                        if (window.Resources[defaultKey] is Brush defaultBrush)
                        {
                            window.Resources[key] = defaultBrush;
                        }
                        else if (window.Resources[defaultKey] is Color defaultColor)
                        {
                            window.Resources[key] = defaultColor;
                        }
                    }
                }
                MainWindow.SelectedFolderColor = Colors.Transparent;
                MainWindow.Instance.SetFoldersColor();
                UpdateHighlightColors(window);
                UpdateButtonBackgrounds();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сбросе стилей: {ex.Message}");
            }
        }

        public static void UpdateHighlightColors(Window window, double brightnessIncrease = 30)
        {
            var converter = new AdaptiveColorConverter();

            if (window.Resources["RectangleBrush"] is SolidColorBrush rectangleBrush)
            {
                var highlightBrush = (SolidColorBrush)converter.Convert(rectangleBrush, typeof(SolidColorBrush), brightnessIncrease, CultureInfo.CurrentCulture);

                window.Resources["RectangleHighlightBrush"] = highlightBrush;
            }
            if (window.Resources["SideMenuBrush"] is SolidColorBrush sideMenuBrush)
            {
                var highlightBrush = (SolidColorBrush)converter.Convert(sideMenuBrush, typeof(SolidColorBrush), brightnessIncrease, CultureInfo.CurrentCulture);

                window.Resources["SideMenuHighlightBrush"] = highlightBrush;
                UpdateButtonBackgrounds();
            }
        }

        public static void UpdateButtonBackgrounds()
        {
            Button[] buttons = { MainWindow.Instance.MainButton, MainWindow.Instance.ThemesButton, MainWindow.Instance.SettingsButton };

            foreach (var button in buttons)
            {
                if (button.Background != Brushes.Transparent)
                {
                    button.Background = (Brush)MainWindow.Instance.Resources["SideMenuHighlightBrush"];
                    return;
                }
                else
                {
                    button.Background = Brushes.Transparent;
                }
            }
        }

        public static void PopulateMonitorComboBox()
        {
            MainWindow.Instance.MonitorComboBox.Items.Clear();
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wallpaper Engine", "config.json");

            if (File.Exists(configPath))
            {
                string jsonContent = File.ReadAllText(configPath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = document.RootElement;

                    var userProperty = root.EnumerateObject().FirstOrDefault(prop => !prop.Name.StartsWith("?"));

                    if (userProperty.Value.ValueKind == JsonValueKind.Object &&
                        userProperty.Value.TryGetProperty("general", out JsonElement general) &&
                        general.TryGetProperty("wallpaperconfig", out JsonElement wallpaperConfig) &&
                        wallpaperConfig.TryGetProperty("selectedwallpapers", out JsonElement selectedWallpapers))
                    {
                        foreach (var monitor in selectedWallpapers.EnumerateObject())
                        {
                            MainWindow.Instance.MonitorComboBox.Items.Add(monitor.Name);
                        }
                    }
                }
            }
        }

        public static string GetWallpaperPathForMonitor(string monitorName)
        {
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wallpaper Engine", "config.json");

            if (File.Exists(configPath))
            {
                string jsonContent = File.ReadAllText(configPath);
                using (JsonDocument document = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = document.RootElement;

                    var userProperty = root.EnumerateObject().FirstOrDefault(prop => !prop.Name.StartsWith("?"));

                    if (userProperty.Value.ValueKind == JsonValueKind.Object &&
                        userProperty.Value.TryGetProperty("general", out JsonElement general) &&
                        general.TryGetProperty("wallpaperconfig", out JsonElement wallpaperConfig) &&
                        wallpaperConfig.TryGetProperty("selectedwallpapers", out JsonElement selectedWallpapers) &&
                        selectedWallpapers.TryGetProperty(monitorName, out JsonElement monitor) &&
                        monitor.TryGetProperty("file", out JsonElement file))
                    {
                        return file.GetString();
                    }
                }
            }

            return null;
        }

    }
}
