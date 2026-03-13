using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Styleplorer
{
    public class CustomVirtualizingStackPanel : VirtualizingStackPanel
    {
        private CancellationTokenSource cts = new CancellationTokenSource();
        private IconCache iconCache;

        public CustomVirtualizingStackPanel()
        {
            iconCache = new IconCache();
            cts = new CancellationTokenSource();
            //this.Loaded += CustomVirtualizingStackPanel_Loaded;
        }
        
        protected override Size ArrangeOverride(Size finalSize)
        {
            base.ArrangeOverride(finalSize);
            _ = LoadIconsForVisibleItems();
            return finalSize;
        }

        private void CustomVirtualizingStackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = FindVisualParent<ScrollViewer>(this);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _ = LoadIconsForVisibleItems();
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null) return parent;
            return FindVisualParent<T>(parentObject);
        }

        private IEnumerable<int> GetVisibleItemIndexes()
        {
            var itemsControl = ItemsControl.GetItemsOwner(this);
            int firstVisibleItemIndex = (int)this.VerticalOffset;
            int visibleItemsCount = (int)this.ViewportHeight + 1;
            return Enumerable.Range(firstVisibleItemIndex, visibleItemsCount)
                .Where(i => i < itemsControl.Items.Count);
        }


        #region Загрузка иконок

        private async Task LoadIconsForVisibleItems()
        {
            cts.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;
            try
            {
                var tasks = new ConcurrentBag<Task<(int Index, object Item)>>();
                var itemsControl = ItemsControl.GetItemsOwner(this);
                var itemsSource = itemsControl.ItemsSource as IEnumerable;
                var itemsSnapshot = new List<object>((IEnumerable<object>)itemsSource);
                var visibleIndexes = GetVisibleItemIndexes();
                await Task.Run(() =>
                {
                    try
                    {
                        Parallel.ForEach(visibleIndexes,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cts.Token },
                    (i) =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }
                        var item = itemsSnapshot[i] as FileSystemObject;
                        if (item != null && item.Icon == null)
                        {
                            if (item is FileObject)
                            {
                                tasks.Add(LoadIconForItem(item, i));
                            }
                            else if (item is FolderObject folderItem)
                            {
                                tasks.Add(LoadIconForFolder(folderItem, i));
                            }
                        }
                    });
                    }
                    catch (Exception)
                    {
                        // Обработка или логирование исключения
                    }
                }, token);

                var results = await Task.WhenAll(tasks);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var (index, item) in results)
                    {
                        if (index >= 0 && index < InternalChildren.Count && InternalChildren[index] == item)
                        {
                            UpdateUIForItem(index, item as FileSystemObject);
                        }
                    }
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }

        private async Task<(int Index, object Item)> LoadIconForItem(FileSystemObject item, int index)
        {
            try
            {
                if (item.Icon == null)
                {
                    BitmapSource icon;
                    if (item.Path.StartsWith("dropbox://") && !File.Exists(item.Path))
                    {
                        string dropboxPath = item.Path.Substring("dropbox://".Length);
                        icon = await GetDropboxFileIcon(dropboxPath);
                    }
                    else
                    {
                        icon = await iconCache.GetIcon(item.Path);
                    }

                    if (!cts.Token.IsCancellationRequested && icon != null)
                    {
                        if (icon.CanFreeze)
                        {
                            icon.Freeze();
                        }
                        item.Icon = icon;
                    }
                }
            }
            catch { }
            return (index, item);
        }

        private async Task<(int Index, object Item)> LoadIconForFolder(FolderObject folderItem, int index)
        {
            try
            {
                if (folderItem.Icon == null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        folderItem.Icon = MainWindow.ChangeImageColor(MainWindow.folderInsideFolderIcon);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в LoadIconForFolder: {ex.Message}");
            }
            return (index, folderItem);
        }

        [StructLayout(LayoutKind.Sequential)]
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

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private BitmapSource GetSystemIcon(string path, bool smallIcon = false)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | (smallIcon ? SHGFI_SMALLICON : SHGFI_LARGEICON);

            IntPtr result = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (result == IntPtr.Zero)
                return null;

            try
            {
                using (System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(shfi.hIcon))
                {
                    return Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            finally
            {
                DestroyIcon(shfi.hIcon);
            }
        }
        private async Task<BitmapSource> GetDropboxFileIcon(string dropboxPath)
        {
            try
            {
                var metadata = await MainWindow.Instance._dropboxService._client.Files.GetMetadataAsync(dropboxPath);

                if (metadata.IsFile)
                {
                    var fileMetadata = metadata.AsFile;
                    string extension = Path.GetExtension(fileMetadata.Name).ToLower();

                    string tempFilePath = Path.Combine(Path.GetTempPath(), $"temp{extension}");
                    File.Create(tempFilePath).Close();

                    try
                    {
                        return GetSystemIcon(tempFilePath);
                    }
                    finally
                    {
                        File.Delete(tempFilePath);
                    }
                }

                return GetSystemIcon(Environment.GetFolderPath(Environment.SpecialFolder.System));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении иконки для {dropboxPath}: {ex.Message}");
                return GetSystemIcon(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"));
            }
        }

        private void UpdateUIForItem(int index, FileSystemObject updatedItem)
        {
            var itemsControl = ItemsControl.GetItemsOwner(this);
            if (itemsControl != null)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                if (container != null)
                {
                    container.DataContext = updatedItem;
                }
            }
        }

        #endregion
    }

}
