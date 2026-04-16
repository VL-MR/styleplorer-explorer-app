using System;
using System.Drawing;
using System.Drawing.IconLib;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Styleplorer
{
    public static class IconHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("shell32.dll")]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        public interface IImageList
        {
            int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            int SetOverlayImage(int iImage, int iOverlay);
            int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            int AddMasked(IntPtr hbmImage, uint crMask, ref int pi);
            int Draw(ref IMAGELISTDRAWPARAMS pimldp);
            int Remove(int i);
            int GetIcon(int i, int flags, out IntPtr picon);
            int GetImageInfo(int i, ref IMAGEINFO pImageInfo);
            int Copy(int iDst, IImageList punkSrc, int iSrc, uint uFlags);
            int Merge(int i1, IImageList punk2, int i2, int dx, int dy, ref Guid riid, out IntPtr ppv);
            int Clone(ref Guid riid, out IntPtr ppv);
            int GetImageRect(int i, ref RECT prc);
            int GetIconSize(ref int cx, ref int cy);
            int SetIconSize(int cx, int cy);
            int GetImageCount(ref int pi);
            int SetImageCount(uint uNewCount);
            int SetBkColor(uint clrBk, ref uint pclr);
            int GetBkColor(ref uint pclr);
            int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);
            int EndDrag();
            int DragEnter(IntPtr hwndLock, int x, int y);
            int DragLeave(IntPtr hwndLock);
            int DragMove(int x, int y);
            int SetDragCursorImage(ref IImageList punk, int iDrag, int dxHotspot, int dyHotspot);
            int DragShowNolock(int fShow);
            int GetDragImage(ref POINT ppt, ref POINT pptHotspot, ref Guid riid, out IntPtr ppv);
            int GetItemFlags(int i, ref uint dwFlags);
            int GetOverlayImage(int iOverlay, ref int piIndex);
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            int x;
            int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;        // x offest from the upperleft of bitmap
            public int yBitmap;        // y offset from the upperleft of bitmap
            public uint rgbBk;
            public uint rgbFg;
            public uint fStyle;
            public uint dwRop;
            public uint fState;
            public uint Frame;
            public uint crEffect;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGEINFO
        {
            public IntPtr hbmImage;
            public IntPtr hbmMask;
            public int Unused1;
            public int Unused2;
            public RECT rcImage;
        }

        public static BitmapSource GetIcon(string path)
        {
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };
            if (Array.Exists(imageExtensions, ext => path.ToLower().EndsWith(ext)))
            {
                if (path.ToLower().EndsWith(".ico"))
                {
                    MultiIcon mIcon = new MultiIcon();
                    mIcon.Load(path);
                    Icon biggestSizeIcon = null;
                    for (int i = 0; i < mIcon.Count; i++)
                    {
                        for (int j = 0; j < mIcon[i].Count; j++)
                        {
                            if (biggestSizeIcon == null || (mIcon[i][j].Icon.Size.Width > biggestSizeIcon.Size.Width && mIcon[i][j].Icon.Size.Height > biggestSizeIcon.Size.Height))
                            {
                                biggestSizeIcon?.Dispose();
                                biggestSizeIcon = mIcon[i][j].Icon;
                            }
                            else
                            {
                                mIcon[i][j].Icon.Dispose();
                            }
                        }
                    }
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        biggestSizeIcon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    biggestSizeIcon.Dispose();
                    return bitmapSource;
                }
                else
                {
                    try
                    {
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.UriSource = new Uri(path);
                        //bitmapImage.DecodePixelWidth = 100;
                        //bitmapImage.DecodePixelHeight = 100;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        //RenderOptions.SetBitmapScalingMode(bitmapImage, BitmapScalingMode.HighQuality);
                        // Если картинка маленькая, создаем новый Bitmap и рисуем картинку по центру
                        if (bitmapImage.PixelWidth < 100 || bitmapImage.PixelHeight < 100)
                        {
                            // Создаем новый Bitmap размером 100x100 с прозрачным фоном
                            Bitmap centeredBitmap = new Bitmap(100, 100);
                            Graphics g = Graphics.FromImage(centeredBitmap);
                            g.Clear(System.Drawing.Color.Transparent);

                            // Преобразуем BitmapImage в Bitmap
                            var bitmap = new Bitmap(bitmapImage.PixelWidth, bitmapImage.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                            var bitmapData = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                            bitmapImage.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, bitmapData.Height * bitmapData.Stride, bitmapData.Stride);
                            bitmap.UnlockBits(bitmapData);

                            // Рисуем картинку по центру
                            g.DrawImage(bitmap, (100 - bitmap.Width) / 2, (100 - bitmap.Height) / 2, bitmap.Width, bitmap.Height);

                            // Преобразуем Bitmap в BitmapSource
                            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                centeredBitmap.GetHbitmap(),
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());

                            bitmapSource.Freeze();
                            return bitmapSource;
                        }
                        else
                        {
                            // Если картинка большая, возвращаем её без изменений
                            return bitmapImage;
                        }
                    }
                    catch
                    {
                        return GetFileIcon(path);
                    }
                }
            }
            else
            {
                return GetFileIcon(path);
            }
        }

        public static BitmapSource GetSmallIcon(string path)
        {
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };
            if (Array.Exists(imageExtensions, ext => path.ToLower().EndsWith(ext)))
            {
                if (path.ToLower().EndsWith(".ico"))
                {
                    return GetSmallIcoIcon(path);
                }
                else
                {
                    return GetSmallImageIcon(path);
                }
            }
            else
            {
                return GetSmallestExeIcon(path);
            }
        }

        private static BitmapSource GetSmallIcoIcon(string path)
        {
            MultiIcon mIcon = new MultiIcon();
            mIcon.Load(path);
            Icon smallestIcon = null;
            for (int i = 0; i < mIcon.Count; i++)
            {
                for (int j = 0; j < mIcon[i].Count; j++)
                {
                    if (smallestIcon == null || (mIcon[i][j].Icon.Size.Width < smallestIcon.Size.Width && mIcon[i][j].Icon.Size.Height < smallestIcon.Size.Height))
                    {
                        smallestIcon?.Dispose();
                        smallestIcon = mIcon[i][j].Icon;
                    }
                    else
                    {
                        mIcon[i][j].Icon.Dispose();
                    }
                }
            }
            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                smallestIcon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            smallestIcon.Dispose();
            return bitmapSource;
        }

        private static BitmapSource GetSmallImageIcon(string path)
        {
            try
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(path);
                bitmapImage.DecodePixelWidth = 16; // Устанавливаем маленький размер
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
            catch
            {
                return GetSmallestExeIcon(path);
            }
        }

        private static readonly object _iconLock = new object();

        public static BitmapSource GetFileIcon(string path)
        {
            lock (_iconLock)
            {
                try
                {
                    SHFILEINFO shinfo = new SHFILEINFO();
                    IntPtr hImgSmall = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), 0x100 | 0x0);

                    Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                    int imageIndex = shinfo.iIcon;

                    IImageList iml = null;
                    IntPtr hIcon = IntPtr.Zero;
                    IntPtr hBitmap = IntPtr.Zero;
                    BitmapSource bitmapSource = null;

                    try
                    {
                        // Попытка получить IImageList с несколькими повторами
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            try
                            {
                                SHGetImageList(0x4, ref IID_IImageList, out iml);
                                if (iml != null) break;
                            }
                            catch (COMException)
                            {
                                if (attempt == 2) throw;
                                System.Threading.Thread.Sleep(10); // Небольшая задержка перед повторной попыткой
                            }
                        }

                        if (iml == null) throw new InvalidOperationException("Failed to get IImageList");

                        iml.GetIcon(imageIndex, 0x1, out hIcon);

                        using (Icon icon = Icon.FromHandle(hIcon))
                        using (Bitmap bitmap = icon.ToBitmap())
                        {
                            hBitmap = bitmap.GetHbitmap();

                            bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }

                        // Проверка на маленькую иконку и обработка
                        if (IsSmallIcon(bitmapSource))
                        {
                            Marshal.ReleaseComObject(iml);
                            iml = null;

                            // Повторная попытка получить IImageList для маленькой иконки
                            for (int attempt = 0; attempt < 3; attempt++)
                            {
                                try
                                {
                                    SHGetImageList(0x2, ref IID_IImageList, out iml);
                                    if (iml != null) break;
                                }
                                catch (COMException)
                                {
                                    if (attempt == 2) throw;
                                    System.Threading.Thread.Sleep(10);
                                }
                            }

                            if (iml == null) throw new InvalidOperationException("Failed to get IImageList for small icon");

                            iml.GetIcon(imageIndex, 0x1, out hIcon);

                            using (Icon icon = Icon.FromHandle(hIcon))
                            using (Bitmap bitmap = icon.ToBitmap())
                            {
                                Bitmap centeredBitmap = new Bitmap(100, 100);
                                using (Graphics g = Graphics.FromImage(centeredBitmap))
                                {
                                    g.Clear(System.Drawing.Color.Transparent);
                                    g.DrawImage(bitmap, (100 - 50) / 2, (100 - 50) / 2, 50, 50);
                                }

                                if (hBitmap != IntPtr.Zero)
                                    DeleteObject(hBitmap);
                                hBitmap = centeredBitmap.GetHbitmap();

                                bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                            }
                        }

                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                    finally
                    {
                        if (hIcon != IntPtr.Zero)
                            DestroyIcon(hIcon);
                        if (hImgSmall != IntPtr.Zero)
                            DestroyIcon(hImgSmall);
                        if (hBitmap != IntPtr.Zero)
                            DeleteObject(hBitmap);
                        if (iml != null)
                            Marshal.ReleaseComObject(iml);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (ex is COMException comEx)
                    {
                        Console.WriteLine($"COM Error Code: 0x{comEx.ErrorCode:X}");
                    }
                    return null;
                }
            }
        }

        private static bool IsSmallIcon(BitmapSource bitmapSource)
        {
            for (int i = 103; i < 153; i++)
            {
                for (int j = 103; j < 153; j++)
                {
                    byte[] pixels = new byte[4];
                    bitmapSource.CopyPixels(new Int32Rect(i, j, 1, 1), pixels, 4, 0);
                    if (pixels[3] != 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // Shell.Application Мб.

        //private static BitmapSource GetFileIcon(string path)
        //{
        //    try
        //    {
        //        // Если файл не является изображением, получаем его иконку
        //        SHFILEINFO shinfo = new SHFILEINFO();
        //        IntPtr hImgSmall = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), 0x100 | 0x0);

        //        Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        //        int imageIndex = shinfo.iIcon;

        //        SHGetImageList(0x4, ref IID_IImageList, out IImageList iml);
        //        iml.GetIcon(imageIndex, 0x1, out nint hIcon);

        //        BitmapSource bitmapSource;
        //        IntPtr hBitmap = IntPtr.Zero;
        //        try
        //        {
        //            using (Icon icon = Icon.FromHandle(hIcon))
        //            {
        //                using (Bitmap bitmap = icon.ToBitmap())
        //                {
        //                    hBitmap = bitmap.GetHbitmap();

        //                    bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
        //                        hBitmap,
        //                        IntPtr.Zero,
        //                        Int32Rect.Empty,
        //                        BitmapSizeOptions.FromEmptyOptions());
        //                    bitmap.Dispose();
        //                }
        //                icon.Dispose();
        //            }
        //        }
        //        finally
        //        {
        //            if (hIcon != IntPtr.Zero)
        //                DestroyIcon(hIcon);
        //            if (hImgSmall != IntPtr.Zero)
        //                DestroyIcon(hImgSmall);
        //            if (hBitmap != IntPtr.Zero)
        //                DeleteObject(hBitmap);
        //        }
        //        // Проверяем, является ли иконка маленькой
        //        bool isSmallIcon = true;
        //        for (int i = 103; i < 153; i++)
        //        {
        //            for (int j = 103; j < 153; j++)
        //            {
        //                byte[] pixels = new byte[4];
        //                bitmapSource.CopyPixels(new Int32Rect(i, j, 1, 1), pixels, 4, 0);
        //                if (pixels[3] != 0)
        //                {
        //                    isSmallIcon = false;
        //                    break;
        //                }
        //            }
        //            if (!isSmallIcon) break;
        //        }

        //        // Если иконка маленькая, получаем меньшую иконку
        //        if (isSmallIcon)
        //        {
        //            SHGetImageList(0x2, ref IID_IImageList, out iml);
        //            iml.GetIcon(imageIndex, 0x1, out hIcon);

        //            try
        //            {
        //                using (Icon icon = Icon.FromHandle(hIcon))
        //                {
        //                    using (Bitmap bitmap = icon.ToBitmap())
        //                    {
        //                        // Создаем новый Bitmap размером 100x100 с прозрачным фоном
        //                        Bitmap centeredBitmap = new Bitmap(100, 100);
        //                        Graphics g = Graphics.FromImage(centeredBitmap);
        //                        g.Clear(System.Drawing.Color.Transparent);

        //                        // Рисуем иконку по центру
        //                        g.DrawImage(bitmap, (100 - 50) / 2, (100 - 50) / 2, 50, 50);

        //                        hBitmap = centeredBitmap.GetHbitmap();

        //                        bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
        //                            hBitmap,
        //                            IntPtr.Zero,
        //                            Int32Rect.Empty,
        //                            BitmapSizeOptions.FromEmptyOptions());
        //                        bitmap.Dispose();
        //                    }
        //                    icon.Dispose();
        //                }
        //            }
        //            finally
        //            {
        //                if (hIcon != IntPtr.Zero)
        //                    DestroyIcon(hIcon);
        //                if (hImgSmall != IntPtr.Zero)
        //                    DestroyIcon(hImgSmall);
        //                if (hBitmap != IntPtr.Zero)
        //                    DeleteObject(hBitmap);
        //            }
        //        }

        //        bitmapSource.Freeze();
        //        return bitmapSource;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"COM Error: {ex.Message}");
        //        if (ex is COMException comEx)
        //        {
        //            Console.WriteLine($"COM Error Code: 0x{comEx.ErrorCode:X}");
        //        }
        //        return null;
        //    }
        //} // Ошибка COM. Из-за этого и пропадают иконки файлов.

        //[DllImport("ole32.dll")]
        //public static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        //[DllImport("ole32.dll")]
        //static extern void CoUninitialize();

        //[STAThread]
        //private static BitmapSource GetFileIcon(string path)
        //{
        //    CoInitializeEx(IntPtr.Zero, 0x0); // Initialize COM
        //    try
        //    {
        //        using (Icon icon = Icon.ExtractAssociatedIcon(path))
        //        {
        //            return Imaging.CreateBitmapSourceFromHIcon(
        //                icon.Handle,
        //                Int32Rect.Empty,
        //                BitmapSizeOptions.FromEmptyOptions());
        //        }
        //    }
        //    finally
        //    {
        //        CoUninitialize(); // Uninitialize COM
        //    }
        //}

        public static BitmapSource GetSmallestExeIcon(string path)
        {
            if (path == null)
            {
                return null;
            }
            if (path.ToLower().EndsWith(".exe"))
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hImgSmall = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), 0x100 | 0x0);

                Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
                IImageList iml;
                int imageIndex = shinfo.iIcon;

                // Сначала пробуем получить самую маленькую иконку (16x16)
                SHGetImageList(0x1, ref IID_IImageList, out iml); // 0x1 для иконок 16x16
                IntPtr hIcon;
                iml.GetIcon(imageIndex, 0x1, out hIcon);

                BitmapSource bitmapSource;
                IntPtr hBitmap = IntPtr.Zero;
                try
                {
                    using (Icon icon = Icon.FromHandle(hIcon))
                    {
                        using (Bitmap bitmap = icon.ToBitmap())
                        {
                            hBitmap = bitmap.GetHbitmap();

                            bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            bitmap.Dispose();
                        }
                        icon.Dispose();
                    }
                }
                finally
                {
                    if (hIcon != IntPtr.Zero)
                        DestroyIcon(hIcon);
                    if (hImgSmall != IntPtr.Zero)
                        DestroyIcon(hImgSmall);
                    if (hBitmap != IntPtr.Zero)
                        DeleteObject(hBitmap);
                }

                // Если иконка маленькая, возвращаем её
                if (bitmapSource.PixelWidth <= 16 && bitmapSource.PixelHeight <= 16)
                {
                    bitmapSource.Freeze();
                    return bitmapSource;
                }

                // Иначе пробуем получить следующую по размеру иконку (32x32)
                SHGetImageList(0x0, ref IID_IImageList, out iml); // 0x0 для иконок 32x32
                iml.GetIcon(imageIndex, 0x1, out hIcon);

                try
                {
                    using (Icon icon = Icon.FromHandle(hIcon))
                    {
                        using (Bitmap bitmap = icon.ToBitmap())
                        {
                            hBitmap = bitmap.GetHbitmap();

                            bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            bitmap.Dispose();
                        }
                        icon.Dispose();
                    }
                }
                finally
                {
                    if (hIcon != IntPtr.Zero)
                        DestroyIcon(hIcon);
                    if (hImgSmall != IntPtr.Zero)
                        DestroyIcon(hImgSmall);
                    if (hBitmap != IntPtr.Zero)
                        DeleteObject(hBitmap);
                }

                bitmapSource.Freeze();
                return bitmapSource;
            }

            // Если файл не является exe, возвращаем null или можно выбросить исключение
            return null;
        }

        public static BitmapSource GetIcon2(string path)
        {
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };
            if (Array.Exists(imageExtensions, ext => path.ToLower().EndsWith(ext)))
            {
                if (path.ToLower().EndsWith(".ico"))
                {
                    return GetLargestIcoIcon(path);
                }
                else
                {
                    return GetResizedImageIcon(path);
                }
            }
            else
            {
                return GetFileIcon(path);
            }
        }

        private static BitmapSource GetLargestIcoIcon(string path)
        {
            MultiIcon mIcon = new MultiIcon();
            mIcon.Load(path);
            Icon largestIcon = null;
            for (int i = 0; i < mIcon.Count; i++)
            {
                for (int j = 0; j < mIcon[i].Count; j++)
                {
                    if (largestIcon == null || (mIcon[i][j].Icon.Width > largestIcon.Width && mIcon[i][j].Icon.Height > largestIcon.Height))
                    {
                        largestIcon?.Dispose();
                        largestIcon = mIcon[i][j].Icon;
                    }
                    else
                    {
                        mIcon[i][j].Icon.Dispose();
                    }
                }
            }
            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                largestIcon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            largestIcon.Dispose();
            if (bitmapSource.PixelWidth <= 100 && bitmapSource.PixelHeight <= 100)
            {
                return CenterImage(bitmapSource, 100, 100);
            }
            else
            {
                return ResizeImage(bitmapSource, 100, 100);
            }
        }

        private static BitmapSource GetResizedImageIcon(string path)
        {
            try
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(path);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                if (bitmapImage.PixelWidth <= 100 && bitmapImage.PixelHeight <= 100)
                {
                    return CenterImage(bitmapImage, 100, 100);
                }
                else
                {
                    return ResizeImage(bitmapImage, 100, 100);
                }
            }
            catch
            {
                return GetFileIcon(path);
            }
        }
        private static BitmapSource CenterImage(BitmapSource source, int maxWidth, int maxHeight)
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(source, new Rect((maxWidth - source.PixelWidth) / 2, (maxHeight - source.PixelHeight) / 2, source.PixelWidth, source.PixelHeight));
            }

            var centeredImage = new RenderTargetBitmap(maxWidth, maxHeight, 96, 96, PixelFormats.Pbgra32);
            centeredImage.Render(drawingVisual);

            return centeredImage;
        }
        public static BitmapSource ResizeImage(BitmapSource source, int maxWidth, int maxHeight)
        {
            double scale = Math.Min((double)maxWidth / source.PixelWidth, (double)maxHeight / source.PixelHeight);
            int newWidth = (int)(source.PixelWidth * scale);
            int newHeight = (int)(source.PixelHeight * scale);

            var resizedBitmap = new TransformedBitmap(source, new ScaleTransform(scale, scale));

            // Создаем новый Bitmap размером maxWidth x maxHeight с прозрачным фоном
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(resizedBitmap, new Rect((maxWidth - newWidth) / 2, (maxHeight - newHeight) / 2, newWidth, newHeight));
            }

            var resizedImage = new RenderTargetBitmap(maxWidth, maxHeight, 96, 96, PixelFormats.Pbgra32);
            resizedImage.Render(drawingVisual);

            return resizedImage;
        }

        public static BitmapSource GetIcon3(string path)
        {
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };
            if (Array.Exists(imageExtensions, ext => path.ToLower().EndsWith(ext)))
            {
                if (path.ToLower().EndsWith(".ico"))
                {
                    return GetLargestIcoIcon(path);
                }
                else
                {
                    return GetStretchedImageIcon(path);
                }
            }
            else
            {
                return GetFileIcon(path);
            }
        }

        private static BitmapSource GetStretchedImageIcon(string path)
        {
            try
            {
                BitmapImage originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.UriSource = new Uri(path);
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.EndInit();

                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(originalImage, new Rect(0, 0, 100, 100));
                }

                RenderTargetBitmap stretchedImage = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
                stretchedImage.Render(drawingVisual);

                return stretchedImage;
            }
            catch
            {
                return GetFileIcon(path);
            }
        }

    }
}
