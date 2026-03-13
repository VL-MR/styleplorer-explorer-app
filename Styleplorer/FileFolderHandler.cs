using Dropbox.Api.Files;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Path = System.IO.Path;
namespace Styleplorer
{
    public class FileFolderHandler
    {
        private readonly CustomListView ListBox;
        private readonly NavigationManager navigationManager = new();

        public IconCache iconCache = new();

        public FileFolderHandler(CustomListView folderView, NavigationManager navigationManager)
        {
            ListBox = folderView;
            this.navigationManager = navigationManager;
            //InitializeIcons();
        }

        public async void LoadLogicalDrives()
        {
            ObservableCollection<FileSystemObject> list = new();

            foreach (string drive in Directory.GetLogicalDrives())
            {
                var driveInfo = new DriveInfo(drive);

                var driveItem = new DriveObject()
                {
                    Name = GetFormattedDriveName(driveInfo),
                    Path = driveInfo.RootDirectory.FullName,
                    Icon = MainWindow.driveIcon,
                    Size = driveInfo.TotalSize,
                    FreeSpace = driveInfo.AvailableFreeSpace,
                    Type = driveInfo.DriveType.ToString(),
                    FileSystem = driveInfo.DriveFormat,
                    VolumeLabel = driveInfo.VolumeLabel,
                    DateChanged = null
                };

                list.Add(driveItem);
            }
            if (MainWindow.Instance._dropboxService != null)
            {
                if (MainWindow.Instance._dropboxService._client != null)
                {
                    try
                    {
                        var spaceUsage = await MainWindow.Instance._dropboxService.GetSpaceUsageAsync();
                        var totalSpace = spaceUsage.Allocation.AsIndividual.Value.Allocated;
                        ulong usedSpace = spaceUsage.Used;
                        ulong freeSpace = totalSpace - usedSpace;

                        var driveItem = new DriveObject()
                        {
                            Name = "DropBox",
                            Path = "dropbox:///",
                            Icon = MainWindow.driveIcon,
                            Size = (long?)totalSpace,
                            FreeSpace = (long)freeSpace,
                            Type = "Cloud",
                            FileSystem = "Dropbox",
                            VolumeLabel = "Dropbox",
                            DateChanged = null
                        };
                        list.Add(driveItem);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                ListBox._items.Clear();
                foreach (var item in list)
                {
                    ListBox._items.Add(item);
                }
            });
            //MainWindow.Instance.Scale();
            MainWindow.UpdateElementCount(ListBox);
        }

        //private void InitializeIcons()
        //{
        //    folderInsideFolderIcon.BeginInit();
        //    folderInsideFolderIcon.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/FolderInsideFolder.jpg", UriKind.RelativeOrAbsolute);
        //    folderInsideFolderIcon.DecodePixelWidth = 100;
        //    folderInsideFolderIcon.CacheOption = BitmapCacheOption.OnDemand;
        //    folderInsideFolderIcon.EndInit();

        //    standardFolderIcon.BeginInit();
        //    standardFolderIcon.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/StandartFolder.jpg", UriKind.RelativeOrAbsolute);
        //    standardFolderIcon.DecodePixelWidth = 100;
        //    standardFolderIcon.CacheOption = BitmapCacheOption.OnDemand;
        //    standardFolderIcon.EndInit();

        //    standardFolderIcon1.BeginInit();
        //    standardFolderIcon1.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/StandartFolder1.jpg", UriKind.RelativeOrAbsolute);
        //    standardFolderIcon1.DecodePixelWidth = 100;
        //    standardFolderIcon1.CacheOption = BitmapCacheOption.OnDemand;
        //    standardFolderIcon1.EndInit();

        //    standardFolderIcon2.BeginInit();
        //    standardFolderIcon2.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/StandartFolder2.jpg", UriKind.RelativeOrAbsolute);
        //    standardFolderIcon2.DecodePixelWidth = 100;
        //    standardFolderIcon2.CacheOption = BitmapCacheOption.OnDemand;
        //    standardFolderIcon2.EndInit();

        //    driveIcon.BeginInit();
        //    driveIcon.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/SSD SATA.png", UriKind.RelativeOrAbsolute);
        //    driveIcon.DecodePixelWidth = 100;
        //    driveIcon.CacheOption = BitmapCacheOption.OnDemand;
        //    driveIcon.EndInit();
        //}

        private static string GetFormattedDriveName(DriveInfo driveInfo)
        {
            string volumeLabel = !string.IsNullOrEmpty(driveInfo.VolumeLabel) ? driveInfo.VolumeLabel : "Локальный диск";
            return $"{volumeLabel} ({driveInfo.Name.TrimEnd('\\')})";
        }

        private static bool ShouldShowItem(FileSystemInfo info)
        {
            bool isHidden = (info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            bool isSystem = (info.Attributes & FileAttributes.System) == FileAttributes.System;

            if (isSystem && !AppSettings.ShowSystemFiles)
                return false;
            if (isHidden && !AppSettings.ShowHiddenFiles && !isSystem)
                return false;

            return true;
        }

        private static async Task<bool> IsFavoriteAsync(string path)
        {
            return await Task.Run(() => MainWindow.Instance.favoritesCollection.Any(item => item.Path == path));
        }

        private static async Task<Dictionary<string, ImageSource>> GetGroupAsync(string path)
        {
            return await Task.Run(() =>
            {
                foreach (var group in MainWindow.Instance.groupsCollection)
                {
                    if (group.Items.Any(item => item.Path == path))
                    {
                        return new Dictionary<string, ImageSource> { { group.Name, group.Image } };
                    }
                }
                return null;
            });
        }

        public async Task HandleFolderSelection(string selectedPath)
        {
            try
            {
                var list = new ObservableCollection<FileSystemObject>();

                if (selectedPath.StartsWith("dropbox://"))
                {
                    string dropboxPath = selectedPath.Substring(10);
                    if (dropboxPath == "/")
                    {
                        await LoadDropboxFolderAsync(null, list);
                    }
                    else
                    {
                        await HandleDropboxFolderSelection(dropboxPath, list);
                    }
                }
                else
                {
                    await HandleLocalFolderSelection(selectedPath, list);
                }

                var sortedList = list.OrderBy(item => item is FolderObject ? 0 : 1)
                                     .ThenBy(item => item.Name, new WindowsExplorerComparer())
                                     .ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ListBox._items.Clear();
                    foreach (var item in sortedList)
                    {
                        ListBox._items.Add(item);
                    }
                });

                //MainWindow.Instance.Scale();
                MainWindow.UpdateElementCount(ListBox);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке содержимого папки: {ex.Message}");
            }
        }

        private static async Task HandleLocalFolderSelection(string selectedPath, ObservableCollection<FileSystemObject> list)
        {
            var directories = await Task.Run(() => Directory.GetDirectories(selectedPath));
            var files = await Task.Run(() => Directory.GetFiles(selectedPath));

            foreach (var directory in directories)
            {
                if (!HasAccessToFolder(directory))
                {
                    continue;
                }

                var directoryInfo = new DirectoryInfo(directory);
                if (!ShouldShowItem(directoryInfo))
                {
                    continue;
                }

                var folderItem = await CreateFolderObjectAsync(directory, directoryInfo);
                if (folderItem != null) list.Add(folderItem);
            }

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (!ShouldShowItem(fileInfo))
                {
                    continue;
                }

                var fileItem = CreateFileObject(file, fileInfo);
                if (fileItem != null) list.Add(fileItem);
            }
        }

        private static async Task HandleDropboxFolderSelection(string folderPath, ObservableCollection<FileSystemObject> list)
        {
            var entries = await MainWindow.Instance._dropboxService.ListFilesAsync(folderPath);

            foreach (var entry in entries)
            {
                if (entry is FolderMetadata folderMetadata)
                {
                    var folderItem = new FolderObject
                    {
                        Name = folderMetadata.Name,
                        Path = $"dropbox://{folderMetadata.PathDisplay}",
                        //Icon = standardFolderIcon,
                    };
                    list.Add(folderItem);
                }
                else if (entry is FileMetadata fileMetadata)
                {
                    var fileItem = new FileObject
                    {
                        Name = fileMetadata.Name,
                        Path = $"dropbox://{fileMetadata.PathDisplay}",
                        //Icon = await iconCache.GetIcon(fileMetadata.Name),
                        DateChanged = fileMetadata.ServerModified,
                        Type = GetFileTypeDescription(fileMetadata.Name)
                    };
                    list.Add(fileItem);
                }
            }
        }

        private static async Task<FolderObject> CreateFolderObjectAsync(string directory, DirectoryInfo directoryInfo)
        {
            var isFavorite = await IsFavoriteAsync(directory);
            var group = await GetGroupAsync(directory);

            return new FolderObject
            {
                Name = Path.GetFileName(directory),
                Path = directory,
                Icon = null,
                Size = null,
                Type = Path.GetExtension(directory),
                DateChanged = directoryInfo.LastWriteTime,
                IsFavorite = isFavorite,
                Group = group?.FirstOrDefault().Key,
                GroupImage = group?.FirstOrDefault().Value
            };
        }

        private static FileObject CreateFileObject(string file, FileInfo fileInfo)
        {
            string fileName = AppSettings.ShowFileExtensions || !IsRegisteredFileType(file)
                ? Path.GetFileName(file)
                : Path.GetFileNameWithoutExtension(file);

            return new FileObject
            {
                Name = fileName,
                Path = file,
                Icon = null,
                Size = fileInfo.Length,
                Type = GetFileTypeDescription(file),
                DateChanged = fileInfo.LastWriteTime
            };
        }

        public async void HandleSelectionChanged(object sender, MouseButtonEventArgs e)
        {
            FileSystemObject? selectedItem = ListBox.SelectedItem as FileSystemObject;
            iconCache = new IconCache();
            if (selectedItem == null)
                return;

            string path = selectedItem.Path;
            if (path.StartsWith("dropbox://"))
            {
                string dropboxPath = path.Substring(10);
                if (dropboxPath == "/")
                {
                    await LoadDropboxFolderAsync();
                    navigationManager.NavigateTo(path, ListBox);
                }
                else
                {
                    await HandleDropboxItemSelection(dropboxPath);
                }
            }
            else if (File.Exists(path))
            {
                await HandleLocalFileSelection(path);
            }
            else if (Directory.Exists(path))
            {
                await HandleFolderSelection(path);
                navigationManager.NavigateTo(path, ListBox);
            }
        }

        private async Task HandleDropboxItemSelection(string dropboxPath)
        {
            var metadata = await MainWindow.Instance._dropboxService._client.Files.GetMetadataAsync(dropboxPath);

            if (metadata.IsFile)
            {
                string tempFilePath = await MainWindow.Instance._dropboxService.DownloadFileAsync(dropboxPath);
                string originalExtension = Path.GetExtension(metadata.Name);
                string newTempFilePath = Path.ChangeExtension(tempFilePath, originalExtension);
                File.Move(tempFilePath, newTempFilePath);

                await HandleLocalFileSelection(newTempFilePath, true, metadata.Name, $"dropbox:/{dropboxPath}");
            }
            else
            {
                await HandleFolderSelection($"dropbox://{dropboxPath}");
                navigationManager.NavigateTo($"dropbox://{dropboxPath}", ListBox);
            }
        }

        private async Task HandleLocalFileSelection(string path, bool isTemp = false, string? originalFileName = null, string? originalPath = null)
        {
            string extension = Path.GetExtension(path).ToLower();
            if (AppSettings.EnablePreview)
            {
                if (IsTextFile(extension))
                {
                    await OpenTextFileInNewTabAsync(GetAppropriateTabControl(), path, originalFileName, originalPath);
                }
                else if (IsImageFile(extension))
                {
                    OpenImageFileInNewTab(GetAppropriateTabControl(), path, originalFileName);
                }
                else
                {
                    OpenFileWithDefaultProgram(path);
                }
            }
            else
            {
                OpenFileWithDefaultProgram(path);
            }

            if (isTemp)
            {
                // Удалить временный файл после открытия
                File.Delete(path);
            }
        }

        private TabControl GetAppropriateTabControl()
        {
            var tabControl = MainWindow.FindParent<TabControl>(ListBox);
            if (tabControl.Name == "TabControl1")
            {
                if (MainWindow.Instance.TabControl2.Visibility != Visibility.Visible)
                {
                    MainWindow.Instance.ShowSecondListBox();
                }
                return MainWindow.Instance.TabControl2;
            }
            else
            {
                return MainWindow.Instance.TabControl1;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern nint ShellExecute(nint hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);

        public void OpenFileWithDefaultProgram(string path)
        {
            _ = ShellExecute(nint.Zero, "open", path, "", "", 1);
        }

        private static bool IsRegisteredFileType(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension)) return false;

                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(extension))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(null); // Получаем значение по умолчанию
                        return val != null;
                    }
                }
            }
            catch
            {
                // Обработка ошибок доступа к реестру
            }
            return false;
        }

        private static bool IsTextFile(string extension)
        {
            string[] imageExtensions = { ".txt", ".cs", ".xaml", ".vb", ".xml",
                ".axaml", ".xhtml", ".rss", ".svg", ".csproj", ".vbproj", ".config",
                ".js", ".json", ".html", ".htm", ".css", ".php", ".py", ".sql", ".java",
                ".cpp", ".h", ".hpp", ".rb", ".fs", ".fsx", ".ps1", ".md", ".markdown", ".tex", ".lua" };
            return imageExtensions.Contains(extension);
        }

        private async Task OpenTextFileInNewTabAsync(TabControl tabControl, string path, string? originalFileName = null, string? originalPath = null)
        {
            string fileName = originalFileName ?? Path.GetFileName(path);
            //    var existingTab = tabControl.Items.OfType<TabItem>()
            //.FirstOrDefault(tab => tab.Tag as string == path);

            var existingTab = tabControl.Items.OfType<TabItem>()
    .FirstOrDefault(tab =>
    {
        if (tab.Tag is TabItemData tabData && tabData.CurrentPath != null)
        {
            return tabData.CurrentPath == path;
        }
        return false;
    });

            if (existingTab != null)
            {
                tabControl.SelectedItem = existingTab;
                return;
            }


            TabItem newTab = new()
            {
                Header = fileName,
                //Tag = originalPath ?? path,
                Background = Brushes.Transparent,
                Foreground = Brushes.White
            };
            TabItemData tabData = new TabItemData();
            tabData.CurrentPath = originalPath ?? path;
            tabData.DesiredWidth = newTab.DesiredSize.Width;
            newTab.Tag = tabData;
            TextEditor textEditor = new()
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                ShowLineNumbers = true,
                FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            string extension = Path.GetExtension(path).ToLower();
            switch (extension)
            {
                case ".cs":
                case ".csx":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
                    break;
                case ".vb":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VB");
                    break;
                case ".xml":
                case ".xaml":
                case ".axaml":
                case ".xhtml":
                case ".rss":
                case ".svg":
                case ".csproj":
                case ".vbproj":
                case ".config":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
                    break;
                case ".js":
                case ".json":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
                    break;
                case ".html":
                case ".htm":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
                    break;
                case ".css":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("CSS");
                    break;
                case ".php":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PHP");
                    break;
                case ".py":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Python");
                    break;
                case ".sql":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("SQL");
                    break;
                case ".java":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Java");
                    break;
                case ".cpp":
                case ".h":
                case ".hpp":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C++");
                    break;
                case ".rb":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Ruby");
                    break;
                case ".fs":
                case ".fsx":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("F#");
                    break;
                case ".ps1":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("PowerShell");
                    break;
                case ".md":
                case ".markdown":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Markdown");
                    break;
                case ".tex":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("TeX");
                    break;
                case ".lua":
                    textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Lua");
                    break;
                default:
                    textEditor.SyntaxHighlighting = null;
                    break;
            }

            try
            {
                await Task.Run(() =>
                {
                    string fileContent = File.ReadAllText(path);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        textEditor.Text = fileContent;
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при чтении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            textEditor.PreviewMouseWheel += TextEditor_PreviewMouseWheel;
            textEditor.TextChanged += (sender, e) =>
            {
                UpdateTabHeader(newTab, true);
            };

            newTab.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    TabItem selectedTab = tabControl.SelectedItem as TabItem;
                    if (selectedTab != null)
                    {
                        _ = SaveFile(selectedTab);
                    }
                    e.Handled = true;
                }
            };
            newTab.Loaded += (s, e) =>
            {
                var closeButton = FindChild<Button>(newTab, "CloseButton");
                if (closeButton != null)
                {
                    closeButton.Click -= MainWindow.Instance.CloseTab_Click;
                    closeButton.Click += (sender, args) => CloseTab(tabControl, newTab);
                }
            };
            Application.Current.Dispatcher.Invoke(() =>
            {
                newTab.Content = textEditor;
                newTab.Style = (Style)MainWindow.Instance.FindResource("ClosableTabItemStyle");
                tabControl.Items.Insert(tabControl.Items.Count - 1, newTab);
                //tabControl.Items.Add(newTab);
                tabControl.SelectedItem = newTab;
            });
        }

        private static void CloseTab(TabControl tabControl, TabItem tabItem)
        {
            if (tabItem.Header.ToString().StartsWith("*"))
            {
                string fileName = Path.GetFileName(tabItem.Tag as string);
                MessageBoxResult result = MessageBox.Show(
                    $"Файл '{fileName}' не был сохранен. Сохранить изменения?",
                    "Несохраненные изменения",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        _ = SaveFile(tabItem);
                        tabControl.Items.Remove(tabItem);
                        break;
                    case MessageBoxResult.No:
                        tabControl.Items.Remove(tabItem);
                        break;
                    case MessageBoxResult.Cancel:
                        return;
                }
            }
            else
            {
                tabControl.Items.Remove(tabItem);
            }
        }

        private static T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T childType = child as T;
                if (childType == null)
                {
                    foundChild = FindChild<T>(child, childName);
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }
            return foundChild;
        }

        private double currentZoom = 1.0;
        private const double zoomFactor = 0.1;
        private const double minZoom = 0.7;
        private const double maxZoom = 2.3;

        private void TextEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                TextEditor textEditor = (TextEditor)sender;
                e.Handled = true;

                // Сохраняем текущую позицию курсора
                TextViewPosition caretPosition = textEditor.TextArea.Caret.Position;

                // Вычисляем новый зум
                double oldZoom = currentZoom;
                if (e.Delta > 0)
                    currentZoom = Math.Min(currentZoom + zoomFactor, maxZoom);
                else
                    currentZoom = Math.Max(currentZoom - zoomFactor, minZoom);

                // Применяем новый размер шрифта
                textEditor.FontSize = 12 * currentZoom;

                // Восстанавливаем позицию курсора
                textEditor.TextArea.Caret.Position = caretPosition;

                // Корректируем прокрутку
                double zoomFactor2 = currentZoom / oldZoom;
                textEditor.ScrollToVerticalOffset(textEditor.VerticalOffset * zoomFactor2);
                textEditor.ScrollToHorizontalOffset(textEditor.HorizontalOffset * zoomFactor2);
            }
        }

        private static void UpdateTabHeader(TabItem tab, bool isModified)
        {
            TabItemData tabItemData = tab.Tag as TabItemData;
            string path = tabItemData != null ? tabItemData.CurrentPath : tab.Tag as string;
            string fileName = Path.GetFileName(path);
            tab.Header = isModified ? $"*{fileName}" : fileName;
        }

        private static async Task SaveFile(TabItem tab)
        {
            TabItemData tabItemData = tab.Tag as TabItemData;
            string path = tabItemData != null ? tabItemData.CurrentPath : tab.Tag as string;
            //string path = (string)tab.Tag;
            TextEditor textEditor = (TextEditor)tab.Content;

            try
            {
                if (path.StartsWith("dropbox://"))
                {
                    string dropboxPath = path.Substring(9);
                    using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textEditor.Text)))
                    {
                        await MainWindow.Instance._dropboxService._client.Files.UploadAsync(
                            dropboxPath,
                            WriteMode.Overwrite.Instance,
                            body: stream
                        );
                    }
                }
                else
                {
                    File.WriteAllText(path, textEditor.Text);
                }
                UpdateTabHeader(tab, false); // Обновляем заголовок, убирая "*"
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenImageFileInNewTab(TabControl tabControl, string path, string? originalFileName = null)
        {
            string fileName = originalFileName ?? Path.GetFileName(path);
            //var existingTab = tabControl.Items.OfType<TabItem>()
            //    .FirstOrDefault(tab => tab.Tag as string == path);

            var existingTab = tabControl.Items.OfType<TabItem>()
    .FirstOrDefault(tab =>
    {
        if (tab.Tag is TabItemData tabData && tabData.CurrentPath != null)
        {
            return tabData.CurrentPath == path;
        }
        return false;
    });

            if (existingTab != null)
            {
                tabControl.SelectedItem = existingTab;
                return;
            }

            TabItem newTab = new()
            {
                Header = fileName,
                //Tag = path,
                Background = Brushes.Transparent,
                Foreground = Brushes.White
            };
            TabItemData tabData = new TabItemData();
            tabData.CurrentPath = path;
            tabData.DesiredWidth = newTab.DesiredSize.Width;
            newTab.Tag = tabData;
            Image image = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform
            };

            try
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(path);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                image.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Создаем контейнер для изображения
            Grid imageContainer = new Grid();
            imageContainer.Children.Add(image);

            // Создаем основной контейнер
            Grid mainContainer = new Grid();
            mainContainer.Children.Add(imageContainer);
            mainContainer.SizeChanged += (sender, e) => AdjustImageContainerSize(mainContainer, imageContainer, image);
            mainContainer.PreviewMouseWheel += (sender, e) => ImageZoom(sender, e, image, imageContainer);
            mainContainer.MouseLeftButtonDown += (sender, e) => StartDrag(sender, e, imageContainer);
            mainContainer.MouseMove += (sender, e) => Drag(sender, e, imageContainer);
            mainContainer.MouseLeftButtonUp += (sender, e) => EndDrag(sender, e, imageContainer);
            mainContainer.ClipToBounds = true;

            newTab.Content = mainContainer;
            newTab.Style = (Style)MainWindow.Instance.FindResource("ClosableTabItemStyle");
            tabControl.Items.Insert(tabControl.Items.Count - 1, newTab);
            //tabControl.Items.Add(newTab);
            tabControl.SelectedItem = newTab;
        }

        private static void AdjustImageContainerSize(Grid mainContainer, Grid imageContainer, Image image)
        {
            if (image.Source is BitmapSource bitmapSource)
            {
                double containerWidth = mainContainer.ActualWidth;
                double containerHeight = mainContainer.ActualHeight;
                double imageWidth = bitmapSource.PixelWidth;
                double imageHeight = bitmapSource.PixelHeight;

                double scale = Math.Min(containerWidth / imageWidth, containerHeight / imageHeight);
                scale = Math.Min(scale, 1.0); // Ограничиваем масштаб до 1.0 (оригинальный размер)

                imageContainer.Width = imageWidth * scale;
                imageContainer.Height = imageHeight * scale;
            }
        }

        private const double MaxZoom = 100;
        private const double MinZoom = 1;

        private static void ImageZoom(object sender, MouseWheelEventArgs e, Image image, Grid imageContainer)
        {
            e.Handled = true;
            var mainContainer = (Grid)sender;
            var transform = imageContainer.RenderTransform as MatrixTransform ?? new MatrixTransform();

            double zoom = e.Delta > 0 ? 1.1 : 0.9;
            Matrix matrix = transform.Matrix;
            double currentScale = matrix.M11;
            double newScale = currentScale * zoom;
            var position = e.GetPosition(imageContainer);

            // Ограничиваем масштаб
            if (newScale < MinZoom) newScale = MinZoom;
            if (newScale > MaxZoom) newScale = MaxZoom;

            if (newScale != currentScale)
            {
                Point absolutePos = image.TranslatePoint(position, mainContainer);
                // Масштабируем относительно позиции курсора
                matrix.ScaleAt(newScale / currentScale, newScale / currentScale, absolutePos.X, absolutePos.Y);

                // Вычисляем новые размеры изображения
                double newWidth = imageContainer.Width * newScale;
                double newHeight = imageContainer.Height * newScale;

                // Центрируем изображение, если оно меньше контейнера
                if (newWidth < mainContainer.ActualWidth)
                {
                    matrix.OffsetX = (mainContainer.ActualWidth - newWidth) / 2;
                }
                if (newHeight < mainContainer.ActualHeight)
                {
                    matrix.OffsetY = (mainContainer.ActualHeight - newHeight) / 2;
                }

                imageContainer.RenderTransform = new MatrixTransform(matrix);

                // Устанавливаем выравнивание по левому верхнему углу
                imageContainer.HorizontalAlignment = HorizontalAlignment.Left;
                imageContainer.VerticalAlignment = VerticalAlignment.Top;

                // Ограничиваем положение изображения
                AdjustImageContainerPosition(mainContainer, imageContainer);
            }
        }

        private static void AdjustImageContainerPosition(Grid mainContainer, Grid imageContainer)
        {
            var transform = imageContainer.RenderTransform as MatrixTransform;
            if (transform == null) return;

            Matrix matrix = transform.Matrix;
            double scale = matrix.M11;

            double containerWidth = imageContainer.Width * scale;
            double containerHeight = imageContainer.Height * scale;

            // Вычисляем смещение для центрирования
            double offsetX = Math.Max((mainContainer.ActualWidth - containerWidth) / 2, 0);
            double offsetY = Math.Max((mainContainer.ActualHeight - containerHeight) / 2, 0);

            // Если контейнер изображения больше основного контейнера, ограничиваем его положение
            if (containerWidth > mainContainer.ActualWidth)
            {
                matrix.OffsetX = Math.Max(mainContainer.ActualWidth - containerWidth, Math.Min(0, matrix.OffsetX));
            }
            else
            {
                matrix.OffsetX = offsetX;
            }

            if (containerHeight > mainContainer.ActualHeight)
            {
                matrix.OffsetY = Math.Max(mainContainer.ActualHeight - containerHeight, Math.Min(0, matrix.OffsetY));
            }
            else
            {
                matrix.OffsetY = offsetY;
            }

            imageContainer.RenderTransform = new MatrixTransform(matrix);
        }

        private Point _lastPosition;
        private bool _isDragging = false;
        private void StartDrag(object sender, MouseButtonEventArgs e, Grid imageContainer)
        {
            var element = (UIElement)sender;
            _lastPosition = e.GetPosition(element);
            element.CaptureMouse();
            _isDragging = true;
        }

        private void Drag(object sender, MouseEventArgs e, Grid imageContainer)
        {
            if (!_isDragging) return;

            var mainContainer = (Grid)sender;
            var position = e.GetPosition(mainContainer);
            var transform = imageContainer.RenderTransform as MatrixTransform ?? new MatrixTransform();

            Matrix matrix = transform.Matrix;

            // Проверяем, увеличен ли контейнер изображения
            if (imageContainer.ActualWidth * matrix.M11 > mainContainer.ActualWidth ||
                imageContainer.ActualHeight * matrix.M11 > mainContainer.ActualHeight)
            {
                double deltaX = position.X - _lastPosition.X;
                double deltaY = position.Y - _lastPosition.Y;

                matrix.OffsetX += deltaX;
                matrix.OffsetY += deltaY;

                imageContainer.RenderTransform = new MatrixTransform(matrix);
            }

            _lastPosition = position;
            AdjustImageContainerPosition(mainContainer, imageContainer);
        }

        private void EndDrag(object sender, MouseButtonEventArgs e, Grid imageContainer)
        {
            var element = (UIElement)sender;
            element.ReleaseMouseCapture();
            _isDragging = false;
            AdjustImageContainerPosition((Grid)sender, imageContainer);
        }

        private static bool IsImageFile(string? extension)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".ico" };
            return imageExtensions.Contains(extension);
        }

        [DllImport("shell32.dll")]
        private static extern nint SHGetFileInfo(string? pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public nint hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private static string? GetFileTypeDescription(string? fileName)
        {
            SHFILEINFO shfi = new();
            uint flags = 0x000000400;

            nint result = SHGetFileInfo(fileName, 0, ref shfi, (uint)Marshal.SizeOf(shfi), flags);

            if (result != nint.Zero)
            {
                return shfi.szTypeName;
            }
            return null;
        }

        public static bool HasAccessToFolder(string folderPath)
        {
            try
            {
                Directory.GetDirectories(folderPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private CancellationTokenSource cts = new CancellationTokenSource();

        public async void HandleScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            var visibleItems = GetVisibleItems(ListBox, scrollViewer);

            cts.Cancel();
            cts = new CancellationTokenSource();


            try
            {
                await Task.Delay(30, cts.Token);

                foreach (FileSystemObject item in visibleItems)
                {
                    if (item.Icon == null && File.Exists(item.Path))
                    {
                        item.Icon = await iconCache.GetIcon(item.Path);
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static IEnumerable<FileSystemObject> GetVisibleItems(ListBox listBox, ScrollViewer scrollViewer)
        {
            var items = new List<FileSystemObject>();

            foreach (FileSystemObject item in listBox.Items)
            {
                var listBoxItem = (ListBoxItem)listBox.ItemContainerGenerator.ContainerFromItem(item);
                if (listBoxItem != null && IsUserVisible(listBoxItem, listBox))
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private static bool IsUserVisible(FrameworkElement element, FrameworkElement container)
        {
            if (!element.IsVisible)
                return false;

            Rect bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
            Rect rect = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);
            return rect.Contains(bounds.TopLeft) || rect.Contains(bounds.BottomRight);
        }

        public async void DropboxButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance._dropboxService == null)
            {
                MainWindow.Instance._dropboxService = new DropboxService();
            }
            if (MainWindow.Instance._dropboxService._client != null)
            {
                await LoadDropboxFolderAsync();
            }
            else
            {
                var a = await MainWindow.Instance._dropboxService.AuthenticateAsync();
                if (a == false)
                {
                    MainWindow.Instance._dropboxService._client = null;
                    return;
                }
                else
                {
                    //await LoadDropboxFolderAsync();
                    if (ListBox.CurrentPath == null)
                    {
                        LoadLogicalDrives();
                    }
                }
            }
        }

        public async Task LoadDropboxFolderAsync(string? folderPath = "", ObservableCollection<FileSystemObject>? list = null)
        {
            //ListBox._items.Clear();
            ObservableCollection<FileSystemObject> list1 = new();
            var entries = await MainWindow.Instance._dropboxService.ListFilesAsync(folderPath);

            foreach (var entry in entries)
            {
                FileSystemObject item;
                if (entry is FolderMetadata folderMetadata)
                {
                    item = new FolderObject
                    {
                        Name = folderMetadata.Name,
                        Path = $"dropbox://{folderMetadata.PathDisplay}",
                        //Icon = driveIcon,
                    };
                }
                else if (entry is FileMetadata fileMetadata)
                {
                    item = new FileObject
                    {
                        Name = fileMetadata.Name,
                        Path = $"dropbox://{fileMetadata.PathDisplay}",
                        //Icon = await iconCache.GetIcon(fileMetadata.Name),
                        DateChanged = fileMetadata.ServerModified.ToLocalTime(),
                        Size = (long?)fileMetadata.Size ?? 0
                    };
                }
                else
                {
                    continue;
                }
                if (list != null)
                {
                    list.Add(item);
                }
                else
                {
                    list1.Add(item);
                }
                //ListBox._items.Add(item);
            }
            if (list == null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ListBox._items.Clear();
                    foreach (var item in list1)
                    {
                        ListBox._items.Add(item);
                    }
                });
            }
            //MainWindow.Instance.Scale();
            MainWindow.UpdateElementCount(ListBox);
        }
    }

    public class WindowsExplorerComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string x, string y);

        public int Compare(string x, string y)
        {
            return StrCmpLogicalW(x, y);
        }
    }
}