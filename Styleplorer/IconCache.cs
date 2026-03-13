using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Styleplorer
{
    public class IconCache
    {
        private const int MAX_CACHE_SIZE = 100;
        private ConcurrentDictionary<string, BitmapSource> cache = new ConcurrentDictionary<string, BitmapSource>();
        private Queue<string> cacheOrder = new Queue<string>();
        public async Task<BitmapSource> GetIcon(string path)
        {
            BitmapSource icon = IconHelper.GetIcon2(path);

            if (icon != null)
            {
                //string hash = await ComputeHash(icon);
                string lightKey = await ComputePartialHash(icon);
                if (cache.TryGetValue(lightKey, out BitmapSource? value))
                {
                    return value;
                }
                else
                {
                    //cache[hash] = icon;
                    AddToCache(lightKey, icon);
                    return icon;
                }
            }
            else
            {
                return null;
            }
        }

        public async Task<BitmapSource> GetIcon2(string path)
        {
            BitmapSource icon = IconHelper.GetIcon3(path);

            if (icon != null)
            {
                //string hash = await ComputeHash(icon);
                string lightKey = await ComputePartialHash(icon);

                if (cache.TryGetValue(lightKey, out BitmapSource? value))
                {
                    return value;
                }
                else
                {
                    //cache[hash] = icon;
                    AddToCache(lightKey, icon);
                    return icon;
                }
            }
            else
            {
                return null;
            }
        }

        private void AddToCache(string key, BitmapSource icon)
        {
            if (!cache.ContainsKey(key))
            {
                if (cache.Count >= MAX_CACHE_SIZE)
                {
                    string oldestKey = cacheOrder.Dequeue();
                    cache.TryRemove(oldestKey, out _);
                }

                cache[key] = icon;
                cacheOrder.Enqueue(key);
            }
        }


        private async Task<string> ComputeHash(BitmapSource icon)
        {
            using (var md5 = MD5.Create())
            {
                byte[] iconBytes = ConvertBitmapSourceToByteArray(icon);
                using (var stream = new MemoryStream(iconBytes))
                {
                    var hash = await md5.ComputeHashAsync(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        private async Task<string> ComputePartialHash(BitmapSource icon)
        {
            using (var md5 = MD5.Create())
            {
                byte[] iconBytes = ConvertBitmapSourceToByteArray(icon);
                using (var stream = new MemoryStream(iconBytes))
                {
                    byte[] buffer = new byte[4096]; // Хешируем только первые 4KB
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    byte[] hash = md5.ComputeHash(buffer, 0, bytesRead);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        private async Task<string> ComputeLightKey(BitmapSource icon)
        {
            // Уменьшаем иконку до размера 16x16
            var smallIcon = new TransformedBitmap(icon, new ScaleTransform(16.0 / icon.PixelWidth, 16.0 / icon.PixelHeight));

            // Вычисляем хеш уменьшенной иконки
            using (var md5 = MD5.Create())
            {
                byte[] iconBytes = ConvertBitmapSourceToByteArray(smallIcon);
                using (var stream = new MemoryStream(iconBytes))
                {
                    var hash = await md5.ComputeHashAsync(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private byte[] ConvertBitmapSourceToByteArray(BitmapSource icon)
        {
            var encoder = new PngBitmapEncoder();
            var frame = BitmapFrame.Create(icon);
            encoder.Frames.Add(frame);
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
    }
    //public class IconCache
    //{
    //    private MemoryCache cache = MemoryCache.Default;
    //    private const int MaxWidth = 64; // Максимальная ширина иконки
    //    private const int MaxHeight = 64; // Максимальная высота иконки

    //    public async Task<BitmapSource> GetIcon(string path)
    //    {
    //        string key = $"{path}_{MainWindow.selectedView}";

    //        if (cache.Contains(key))
    //        {
    //            return (BitmapSource)cache.Get(key);
    //        }

    //        BitmapSource icon = await Task.Run(() =>
    //        {
    //            BitmapSource originalIcon;
    //            if (MainWindow.selectedView == "Big")
    //            {
    //                originalIcon = IconHelper.GetIcon(path);
    //            }
    //            else if (MainWindow.selectedView == "Small")
    //            {
    //                originalIcon = IconHelper.GetSmallIcon(path);
    //            }
    //            else
    //            {
    //                originalIcon = IconHelper.GetIcon(path);
    //            }

    //            return ResizeAndCompressIcon(originalIcon);
    //        });

    //        CacheItemPolicy policy = new CacheItemPolicy
    //        {
    //            SlidingExpiration = TimeSpan.FromMinutes(30),
    //            RemovedCallback = (args) =>
    //            {
    //                // Можно добавить логику для обработки удаления из кэша
    //                Console.WriteLine($"Icon removed from cache: {args.CacheItem.Key}");
    //            }
    //        };

    //        cache.Set(key, icon, policy);

    //        return icon;
    //    }

    //    private BitmapSource ResizeAndCompressIcon(BitmapSource originalIcon)
    //    {
    //        if (originalIcon == null) return null;

    //        // Уменьшаем размер изображения
    //        var resizedIcon = new TransformedBitmap(originalIcon, new ScaleTransform(
    //            Math.Min(MaxWidth / originalIcon.Width, 1),
    //            Math.Min(MaxHeight / originalIcon.Height, 1)
    //        ));

    //        // Сжимаем изображение в формат PNG
    //        PngBitmapEncoder encoder = new PngBitmapEncoder();
    //        encoder.Frames.Add(BitmapFrame.Create(resizedIcon));

    //        using (MemoryStream stream = new MemoryStream())
    //        {
    //            encoder.Save(stream);
    //            stream.Position = 0;

    //            BitmapImage compressedIcon = new BitmapImage();
    //            compressedIcon.BeginInit();
    //            compressedIcon.CacheOption = BitmapCacheOption.OnLoad;
    //            compressedIcon.StreamSource = stream;
    //            compressedIcon.EndInit();
    //            compressedIcon.Freeze(); // Это важно для многопоточности

    //            return compressedIcon;
    //        }
    //    }
    //}
}
