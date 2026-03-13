using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace Styleplorer
{
    public class CustomListView : ListView
    {
        public string? CurrentPath { get; set; } // Текущий путь
        public ObservableCollection<FileSystemObject> _items { get; set; } // Элементы листа
        public NavigationManager NavManager { get; set; } // Навигационный менеджер
        public FileFolderHandler Handler { get; set; } // Handler для обработки событий при нажатии на элементе
        public DataTemplateSelector FileFolderTemplateSelector { get; set; } // Выбор шаблона для элемента
        private List<double> _columnWidths = new();
        private List<dynamic> selectedItems = new(); // Выбранные элементы
        // Стандартный конструктор для листа
        public CustomListView()
        {
            CurrentPath = null;
            _items = new ObservableCollection<FileSystemObject>();
            ItemsSource = _items;
            NavManager = new NavigationManager();
            Handler = new FileFolderHandler(this, NavManager);
            Handler.LoadLogicalDrives();
            SetChanges();
            FileFolderTemplateSelector = MainWindow.dataTemplateSelector;
            InitializeAsXaml();
        }

        // Конструктор для поиска (isSearch не нужен ни для чего, кроме того чтобы просто вызвать этот конструктор)
        public CustomListView(bool isSearch)
        {
            CurrentPath = null;
            _items = new ObservableCollection<FileSystemObject>();
            ItemsSource = _items;
            NavManager = new NavigationManager();
            Handler = new FileFolderHandler(this, NavManager);
            SetChanges();
            FileFolderTemplateSelector = MainWindow.dataTemplateSelector;
            InitializeAsXaml();
        }

        // Конструктор для создания листа с установленным путём
        public CustomListView(string path)
        {
            CurrentPath = path;
            _items = new ObservableCollection<FileSystemObject>();
            ItemsSource = _items;
            NavManager = new NavigationManager();
            Handler = new FileFolderHandler(this, NavManager);
            Handler.HandleFolderSelection(path);
            SetChanges();
            FileFolderTemplateSelector = MainWindow.dataTemplateSelector;
            InitializeAsXaml();
        }

        // Установка событий
        public void SetChanges()
        {
            //InitializeFolderView();

            SelectionChanged += FolderView_SelectionChanged;
            PreviewMouseLeftButtonDown += FolderView_PreviewMouseLeftButtonDown;
            PreviewMouseLeftButtonUp += FolderView_PreviewMouseLeftButtonUp;
            PreviewMouseMove += FolderView_PreviewMouseMove;
            MouseDoubleClick += FolderView_MouseDoubleClick;
            MouseRightButtonUp += FolderView_MouseRightButtonUp;
            Drop += FolderView_Drop;
            DragEnter += FolderView_DragEnter;
            DragOver += FolderView_DragOver;
            MouseEnter += (sender, e) =>
            {
                if (!IsFocused && !MainWindow.IsDragging && !MainWindow.isRenaming)
                {
                    Focus();
                    MainWindow.ActiveListBox = this;
                }
            };
            SizeChanged += (s, e) => AdjustGridViewColumnWidths();
            PreviewMouseWheel += CustomListBox_PreviewMouseWheel;
        }
        
        // Инициализация листа (установка событий для элементов)
        private void InitializeFolderView()
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                AttachEventsToItems();
            }
            else
            {
                ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
            }
        }

        // Инициализация листа на разметке XAML
        public void InitializeAsXaml()
        {
            Style style = (Style)Application.Current.FindResource("CustomListBoxStyle");
            if (style != null)
            {
                Style = style;
            }

            ItemTemplateSelector = FileFolderTemplateSelector;
        }

        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
                AttachEventsToItems();
            }
        }

        // Установка событий для элементов
        public void AttachEventsToItems()
        {
            foreach (var item in Items)
            {
                var listBoxItem = (ListBoxItem)ItemContainerGenerator.ContainerFromItem(item);
                if (listBoxItem != null)
                {
                    listBoxItem.PreviewMouseLeftButtonUp += (sender, e) =>
                    {
                        if (MainWindow.IsDragging)
                        {
                            e.Handled = true;
                        }
                        else
                        {
                            Handler.HandleSelectionChanged(sender, e);
                        }
                    };
                }
            }
        }

        private void FolderView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MainWindow.UpdateSelectionCount(this);
        }

        #region Drag and Drop
        private Point? startPoint;
        private void FolderView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!MainWindow.IsDragging)
            {
                if (CurrentPath == null) return;
                if (ContextMenu == null || !ContextMenu.IsVisible)
                {
                    startPoint = e.GetPosition(null);
                    selectedItems = SelectedItems.Cast<dynamic>().ToList();
                }
            }
        }

        private void FolderView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && startPoint.HasValue && !MainWindow.IsDragging && !MainWindow.IsResize)
            {
                foreach (var item in Items)
                {
                    var listBoxItem = (ListBoxItem)ItemContainerGenerator.ContainerFromItem(item);
                    if (listBoxItem != null)
                    {
                        if (selectedItems.Contains(item) && !listBoxItem.IsSelected)
                        {
                            listBoxItem.IsSelected = true;
                        }
                    }
                }

                Point mousePos = e.GetPosition(null);
                Vector diff = startPoint.Value - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (listBoxItem == null)
                    {
                        return;
                    }
                    MainWindow.IsDragging = true;

                    object clickedItem = SelectedItem;

                    if (clickedItem != null && !selectedItems.Contains(clickedItem))
                    {
                        selectedItems.Add(clickedItem);
                    }
                    List<string> paths = selectedItems.Cast<object>()
    .Where(item => item.GetType().GetProperty("Path")?.GetValue(item) is string)
    .Select(item => (string)item.GetType().GetProperty("Path").GetValue(item))
    .ToList();

                    if (selectedItems.Any(item => item.Path.StartsWith("dropbox://")))
                    {
                        var dropboxFiles = selectedItems.Where(item => item.Path.StartsWith("dropbox://")).ToList();
                        var dataObject = new DataObject();
                        dataObject.SetData("DropboxFiles", dropboxFiles);
                        DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                    }
                    else
                    {
                        DataObject dragData = new DataObject();
                        dragData.SetData("CustomListView", sender);
                        dragData.SetData(DataFormats.FileDrop, paths.ToArray());

                        DragDropEffects allowedEffects = DragDropEffects.Copy | DragDropEffects.Move;

                        DragDropEffects result = DragDrop.DoDragDrop(this, dragData, allowedEffects);

                        if (result == DragDropEffects.Move)
                        {
                            foreach (string path in paths)
                            {
                                var itemToRemove = Items.Cast<object>().FirstOrDefault(item =>
                                    (string)item.GetType().GetProperty("Path").GetValue(item) == path);
                                if (itemToRemove != null)
                                {
                                    RemoveItemFromSourceListBox(path);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void FolderView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.IsResize = false;
            if (MainWindow.IsDragging)
            {
                MainWindow.IsDragging = false;
                startPoint = null;
                SelectedItems.Clear();
                e.Handled = true;
            }
            else if (AppSettings.OpenFilesWithSingleClick)
            {
                var item = ItemContainerGenerator.ContainerFromItem((e.OriginalSource as FrameworkElement)?.DataContext) as ListBoxItem;
                if (item != null)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (SelectedItems.Contains(item))
                            SelectedItems.Remove(item);
                        else
                            SelectedItems.Add(item);
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                    }
                    else
                    {
                        Handler.HandleSelectionChanged(item, e);
                    }
                }
            }
        }

        private void FolderView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!AppSettings.OpenFilesWithSingleClick)
            {
                var item = ItemContainerGenerator.ContainerFromItem((e.OriginalSource as FrameworkElement)?.DataContext) as ListBoxItem;
                if (item != null)
                {
                    Handler.HandleSelectionChanged(item, e);
                    e.Handled = true;
                }
            }
        }

        // Функция для поиска родителя определенного типа для элемента
        private static T FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        //private async void FolderView_Drop(object sender, DragEventArgs e)
        //{
        //    CustomListView sourceListBox = new CustomListView();
        //    if (e.Data != null && e.Data.GetDataPresent("CustomListView"))
        //    {
        //        sourceListBox = e.Data.GetData("CustomListView") as CustomListView;
        //    }
        //    if (CurrentPath == null) return;
        //    if (CurrentPath.StartsWith("dropbox://"))
        //    {
        //        string dropboxPath = CurrentPath.Substring(10);
        //        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        //        {
        //            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
        //            foreach (string file in files)
        //            {
        //                await UploadToDropbox(file, dropboxPath);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        if (Name != sourceListBox.Name)
        //        {
        //            if (ContextMenu == null || !ContextMenu.IsVisible)
        //            {
        //                if (e.Data.GetDataPresent("DropboxFiles"))
        //                {
        //                    var dropboxFiles = (dynamic)e.Data.GetData("DropboxFiles");
        //                    foreach (var file in dropboxFiles)
        //                    {
        //                        string dropboxPath = file.Path.Substring(10);
        //                        string localPath = Path.Combine(CurrentPath, file.Name);
        //                        await DownloadFromDropbox(dropboxPath, localPath);
        //                    }
        //                }
        //                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        //                {
        //                    var targetItem = GetItemAtPoint(e.GetPosition(this));
        //                    string targetPath = (targetItem is FolderObject folder) ? folder.Path : CurrentPath;

        //                    if (targetPath != CurrentPath)
        //                    {
        //                        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        //                        {
        //                            string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
        //                            items = items.Distinct().ToArray();

        //                            foreach (string item in items)
        //                            {
        //                                await MoveOrCopyItem(item, targetPath);
        //                                RemoveItemFromOtherListBox(sourceListBox, item);
        //                            }
        //                        }
        //                    }
        //                    else if (CurrentPath != sourceListBox.CurrentPath)
        //                    {

        //                        string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
        //                        items = items.Distinct().ToArray();
        //                        if (items.Any(item => CurrentPath != null && item == CurrentPath))
        //                        {
        //                            return;
        //                        }

        //                        foreach (string item in items)
        //                        {
        //                            if (Directory.Exists(item))
        //                            {
        //                                string dirName = Path.GetFileName(item);
        //                                string destDir = Path.Combine(CurrentPath, dirName);
        //                                Directory.CreateDirectory(destDir);
        //                                // Проверка, находятся ли файлы на одном диске
        //                                if (Path.GetPathRoot(item) == Path.GetPathRoot(destDir))
        //                                {
        //                                    CopyFilesRecursively(new DirectoryInfo(item), new DirectoryInfo(destDir));
        //                                    Directory.Delete(item, true);
        //                                    RemoveItemFromOtherListBox(sourceListBox, item);
        //                                }
        //                                else
        //                                {
        //                                    CopyFilesRecursively(new DirectoryInfo(item), new DirectoryInfo(destDir));
        //                                }

        //                                //CopyFilesRecursively(new DirectoryInfo(item), new DirectoryInfo(destDir));

        //                                var subdirectories = Directory.GetDirectories(destDir);
        //                                var filesInDirectory = Directory.GetFiles(destDir);

        //                                var folderItem = new FolderObject
        //                                {
        //                                    Name = dirName,
        //                                    Path = destDir,
        //                                    Icon = filesInDirectory.Length == 0 && subdirectories.Length > 0
        //    ? Handler.folderInsideFolderIcon
        //    : null,
        //                                    StandartFolderIcon1 = filesInDirectory.Length == 0 && subdirectories.Length > 0
        //    ? null
        //    : Handler.standardFolderIcon1,
        //                                    StandartFolderIcon2 = filesInDirectory.Length == 0 && subdirectories.Length > 0
        //    ? null
        //    : Handler.standardFolderIcon2
        //                                };

        //                                if (filesInDirectory.Length > 0)
        //                                {
        //                                    folderItem.FileIcon1 = await Handler.iconCache.GetIcon(filesInDirectory[0]);

        //                                    if (filesInDirectory.Length > 1)
        //                                    {
        //                                        folderItem.FileIcon2 = await Handler.iconCache.GetIcon(filesInDirectory[1]);
        //                                    }
        //                                }

        //                                if (!Items.Contains(folderItem))
        //                                {
        //                                    _items.Add(folderItem);
        //                                }
        //                            }
        //                            else
        //                            {
        //                                if (File.Exists(item))
        //                                {
        //                                    string fileName = Path.GetFileName(item);
        //                                    string destFile = Path.Combine(CurrentPath, fileName);
        //                                    Console.WriteLine("fileName: " + fileName);

        //                                    // Проверка, находятся ли файлы на одном диске
        //                                    if (Path.GetPathRoot(item) == Path.GetPathRoot(destFile))
        //                                    {
        //                                        // Перемещение файла
        //                                        Console.WriteLine("destFile: " + destFile);
        //                                        Console.WriteLine("item: " + item);
        //                                        File.Move(item, destFile);
        //                                        // Удаление элемента из ListBox
        //                                        RemoveItemFromOtherListBox(sourceListBox, item);
        //                                    }
        //                                    else
        //                                    {
        //                                        // Копирование файла
        //                                        File.Copy(item, destFile, true);
        //                                    }


        //                                    var fileItem = new FileObject
        //                                    {
        //                                        Name = fileName,
        //                                        Path = destFile,
        //                                        Icon = await Handler.iconCache.GetIcon(destFile)
        //                                    };

        //                                    if (!Items.Contains(fileItem))
        //                                    {
        //                                        _items.Add(fileItem);
        //                                    }
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            MainWindow.ActiveListBox = this;
        //            Focus();
        //        }
        //        else
        //        {
        //            if (ContextMenu == null || !ContextMenu.IsVisible)
        //            {
        //                var targetItem = GetItemAtPoint(e.GetPosition(this));
        //                string targetPath = (targetItem is FolderObject folder) ? folder.Path : CurrentPath;

        //                if (targetPath != CurrentPath)
        //                {
        //                    if (e.Data.GetDataPresent("DropboxFiles"))
        //                    {
        //                        var dropboxFiles = (dynamic)e.Data.GetData("DropboxFiles");
        //                        foreach (var file in dropboxFiles)
        //                        {
        //                            string dropboxPath = file.Path.Substring(10);
        //                            string localPath = Path.Combine(CurrentPath, file.Name);
        //                            await DownloadFromDropbox(dropboxPath, localPath);
        //                        }
        //                    }
        //                    else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        //                    {
        //                        string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
        //                        items = items.Distinct().ToArray();

        //                        foreach (string item in items)
        //                        {
        //                            await MoveOrCopyItem(item, targetPath);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    MainWindow.IsDragging = false;
        //    //if (_dragAdorner != null)
        //    //{
        //    //    AdornerLayer.GetAdornerLayer(this).Remove(_dragAdorner);
        //    //    _dragAdorner = null;
        //    //}
        //}

        private async void FolderView_Drop(object sender, DragEventArgs e)
        {
            CustomListView sourceListBox = new CustomListView();
            if (e.Data != null && e.Data.GetDataPresent("CustomListView"))
            {
                sourceListBox = e.Data.GetData("CustomListView") as CustomListView;
            }
            if (CurrentPath == null) return;

            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            try
            {
                if (CurrentPath.StartsWith("dropbox://"))
                {
                    string dropboxPath = CurrentPath.Substring(10);
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        for (int i = 0; i < files.Length; i++)
                        {
                            await UploadToDropbox(files[i], dropboxPath);
                            progressWindow.UpdateProgress($"Загрузка в Dropbox: {Path.GetFileName(files[i])}", (i + 1) * 100 / files.Length);
                        }
                    }
                }
                else
                {
                    if (Name != sourceListBox.Name)
                    {
                        if (ContextMenu == null || !ContextMenu.IsVisible)
                        {
                            if (e.Data.GetDataPresent("DropboxFiles"))
                            {
                                var dropboxFiles = (dynamic)e.Data.GetData("DropboxFiles");
                                for (int i = 0; i < dropboxFiles.Count; i++)
                                {
                                    var file = dropboxFiles[i];
                                    string dropboxPath = file.Path.Substring(10);
                                    string localPath = Path.Combine(CurrentPath, file.Name);
                                    await DownloadFromDropbox(dropboxPath, localPath);
                                    progressWindow.UpdateProgress($"Загрузка из Dropbox: {file.Name}", (i + 1) * 100 / dropboxFiles.Count);
                                }
                            }
                            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                            {
                                var targetItem = GetItemAtPoint(e.GetPosition(this));
                                string targetPath = (targetItem is FolderObject folder) ? folder.Path : CurrentPath;

                                if (targetPath != CurrentPath)
                                {
                                    string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                                    items = items.Distinct().ToArray();

                                    for (int i = 0; i < items.Length; i++)
                                    {
                                        string item = items[i];
                                        await MoveOrCopyItem(item, targetPath, progressWindow, i, items.Length);
                                        RemoveItemFromOtherListBox(sourceListBox, item);
                                    }
                                }
                                else if (CurrentPath != sourceListBox.CurrentPath)
                                {
                                    string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                                    items = items.Distinct().ToArray();
                                    if (items.Any(item => CurrentPath != null && item == CurrentPath))
                                    {
                                        return;
                                    }

                                    for (int i = 0; i < items.Length; i++)
                                    {
                                        string item = items[i];
                                        if (Directory.Exists(item))
                                        {
                                            string dirName = Path.GetFileName(item);
                                            string destDir = Path.Combine(CurrentPath, dirName);
                                            Directory.CreateDirectory(destDir);
                                            if (Path.GetPathRoot(item) == Path.GetPathRoot(destDir))
                                            {
                                                await CopyDirectoryWithProgress(item, destDir, progressWindow);
                                                Directory.Delete(item, true);
                                                RemoveItemFromOtherListBox(sourceListBox, item);
                                            }
                                            else
                                            {
                                                await CopyDirectoryWithProgress(item, destDir, progressWindow);
                                            }

                                            var folderItem = new FolderObject
                                            {
                                                Name = dirName,
                                                Path = destDir,
                                                Icon = Directory.GetFiles(destDir).Length == 0 && Directory.GetDirectories(destDir).Length > 0
                                                    ? MainWindow.folderInsideFolderIcon
                                                    : null,
                                                StandartFolderIcon1 = Directory.GetFiles(destDir).Length == 0 && Directory.GetDirectories(destDir).Length > 0
                                                    ? null
                                                    : MainWindow.standardFolderIcon1,
                                                StandartFolderIcon2 = Directory.GetFiles(destDir).Length == 0 && Directory.GetDirectories(destDir).Length > 0
                                                    ? null
                                                    : MainWindow.standardFolderIcon2
                                            };

                                            var files = Directory.GetFiles(destDir);
                                            if (files.Length > 0)
                                            {
                                                folderItem.FileIcon1 = await Handler.iconCache.GetIcon(files[0]);
                                                if (files.Length > 1)
                                                {
                                                    folderItem.FileIcon2 = await Handler.iconCache.GetIcon(files[1]);
                                                }
                                            }

                                            if (!Items.Contains(folderItem))
                                            {
                                                _items.Add(folderItem);
                                            }
                                        }
                                        else if (File.Exists(item))
                                        {
                                            string fileName = Path.GetFileName(item);
                                            string destFile = Path.Combine(CurrentPath, fileName);

                                            if (Path.GetPathRoot(item) == Path.GetPathRoot(destFile))
                                            {
                                                File.Move(item, destFile);
                                                RemoveItemFromOtherListBox(sourceListBox, item);
                                            }
                                            else
                                            {
                                                await CopyFileWithProgress(item, destFile, progressWindow);
                                            }

                                            var fileItem = new FileObject
                                            {
                                                Name = fileName,
                                                Path = destFile,
                                                Icon = await Handler.iconCache.GetIcon(destFile)
                                            };

                                            if (!Items.Contains(fileItem))
                                            {
                                                _items.Add(fileItem);
                                            }
                                        }

                                        progressWindow.UpdateProgress($"Обработка: {Path.GetFileName(item)}", (i + 1) * 100 / items.Length);
                                    }
                                }
                            }
                        }
                        MainWindow.ActiveListBox = this;
                        Focus();
                    }
                    else
                    {
                        if (ContextMenu == null || !ContextMenu.IsVisible)
                        {
                            var targetItem = GetItemAtPoint(e.GetPosition(this));
                            string targetPath = (targetItem is FolderObject folder) ? folder.Path : CurrentPath;

                            if (targetPath != CurrentPath)
                            {
                                if (e.Data.GetDataPresent("DropboxFiles"))
                                {
                                    var dropboxFiles = (dynamic)e.Data.GetData("DropboxFiles");
                                    foreach (var file in dropboxFiles)
                                    {
                                        string dropboxPath = file.Path.Substring(10);
                                        string localPath = Path.Combine(CurrentPath, file.Name);
                                        await DownloadFromDropbox(dropboxPath, localPath);
                                    }
                                }
                                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                                {
                                    string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                                    items = items.Distinct().ToArray();

                                    for (int i = 0; i < items.Length; i++)
                                    {
                                        string item = items[i];
                                        await MoveOrCopyItem(item, targetPath, progressWindow, i, items.Length);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                progressWindow.Close();
            }

            MainWindow.IsDragging = false;
        }
        

        private async Task MoveOrCopyItem(string sourcePath, string targetPath)
        {

            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(targetPath, fileName);
            string sourceDirectory = Path.GetDirectoryName(sourcePath);
            if (sourcePath.StartsWith("dropbox://") && targetPath.StartsWith("dropbox://"))
            {
                string sourceDropboxPath = sourcePath.Substring(10);
                string targetDropboxPath = Path.Combine(targetPath.Substring(10), Path.GetFileName(sourcePath)).Replace("\\", "/");
                await MainWindow.Instance._dropboxService._client.Files.MoveV2Async(sourceDropboxPath, targetDropboxPath);
            }
            else if (sourcePath.StartsWith("dropbox://"))
            {
                // Загрузка из Dropbox и сохранение локально
                await DownloadFromDropbox(sourcePath.Substring(10), Path.Combine(targetPath, Path.GetFileName(sourcePath)));
            }
            else if (targetPath.StartsWith("dropbox://"))
            {
                // Загрузка локального файла в Dropbox
                await UploadToDropbox(sourcePath, targetPath.Substring(10));
            }
            else
            {
                if (Directory.Exists(sourcePath))
                {
                    if (Path.GetPathRoot(sourcePath) == Path.GetPathRoot(targetPath))
                    {
                        // Если папки на одном диске, перемещаем
                        if (Directory.Exists(destPath))
                        {
                            // Если целевая папка уже существует, создаем новую с уникальным именем
                            int counter = 1;
                            string newDestPath = destPath;
                            while (Directory.Exists(newDestPath))
                            {
                                newDestPath = Path.Combine(targetPath, $"{fileName} ({counter})");
                                counter++;
                            }
                            destPath = newDestPath;
                        }
                        Directory.Move(sourcePath, destPath);
                    }
                    else
                    {
                        // Если на разных дисках, копируем
                        CopyFilesRecursively(new DirectoryInfo(sourcePath), new DirectoryInfo(destPath));
                    }
                }
                else if (File.Exists(sourcePath))
                {
                    if (Path.GetPathRoot(sourcePath) == Path.GetPathRoot(destPath))
                    {
                        File.Move(sourcePath, destPath);
                    }
                    else
                    {
                        File.Copy(sourcePath, destPath, true);
                    }
                }
            }

            RemoveItemFromSourceListBox(sourcePath);
            MainWindow.Instance.UpdateListBoxesWithPath(sourceDirectory);
            if (targetPath != sourceDirectory)
            {
                MainWindow.Instance.UpdateListBoxesWithPath(targetPath);
            }
        }
        private async Task CopyDirectoryWithProgress(string sourceDir, string targetDir, ProgressWindow progressWindow)
        {
            Directory.CreateDirectory(targetDir);
            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            long totalSize = files.Sum(f => new FileInfo(f).Length);
            long copiedSize = 0;

            foreach (string file in files)
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string targetFile = Path.Combine(targetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

                await CopyFileWithProgress(file, targetFile, progressWindow, copiedSize, totalSize);
                copiedSize += new FileInfo(file).Length;
            }
        }
        private async Task CopyFileWithProgress(string sourceFile, string targetFile, ProgressWindow progressWindow, long copiedSize = 0, long totalSize = 0)
        {
            const int bufferSize = 1024 * 1024; // 1 MB buffer
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            using (FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
            using (FileStream target = new FileStream(targetFile, FileMode.Create, FileAccess.Write))
            {
                if (totalSize == 0) totalSize = source.Length;
                while ((bytesRead = await source.ReadAsync(buffer, 0, bufferSize)) > 0)
                {
                    await target.WriteAsync(buffer, 0, bytesRead);
                    copiedSize += bytesRead;
                    int percentage = (int)((copiedSize * 100) / totalSize);
                    progressWindow.UpdateProgress($"Копирование: {Path.GetFileName(sourceFile)}", percentage);
                }
            }
        }
        private async Task UploadToDropbox(string localPath, string dropboxPath)
        {
            try
            {
                if (File.Exists(localPath))
                {
                    await UploadFile(localPath, dropboxPath);
                }
                else if (Directory.Exists(localPath))
                {
                    string folderName = new DirectoryInfo(localPath).Name;
                    string newDropboxPath = "/" + dropboxPath.Trim('/') + "/" + folderName;
                    await UploadFolderRecursively(localPath, newDropboxPath);
                }
                MainWindow.Instance.UpdateListBoxesWithPath("dropbox://" + dropboxPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show("Нет доступа к папке или файлу. Попробуйте запустить программу от имени администратора.", "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task UploadFolderRecursively(string localPath, string dropboxPath)
        {
            await CreateFolderInDropbox(dropboxPath);

            foreach (var file in Directory.GetFiles(localPath))
            {
                await UploadFile(file, dropboxPath);
            }

            foreach (var directory in Directory.GetDirectories(localPath))
            {
                string folderName = Path.GetFileName(directory);
                string newDropboxPath = Path.Combine(dropboxPath, folderName).Replace("\\", "/");
                await UploadFolderRecursively(directory, newDropboxPath);
            }
        }

        private async Task CreateFolderInDropbox(string path)
        {
            try
            {
                await MainWindow.Instance._dropboxService._client.Files.CreateFolderV2Async(path);
            }
            catch (ApiException<CreateFolderError> ex)
            {
                if (ex.ErrorResponse.IsPath)
                {
                    var pathError = ex.ErrorResponse.AsPath;
                    if (pathError.Value.IsConflict)
                    {
                        return;
                    }
                }
                throw;
            }
        }

        private async Task UploadFile(string localFilePath, string dropboxFolderPath)
        {
            using (var fileStream = File.OpenRead(localFilePath))
            {
                var fileName = Path.GetFileName(localFilePath);
                var fullDropboxPath = Path.Combine(dropboxFolderPath, fileName).Replace("\\", "/");
                await MainWindow.Instance._dropboxService._client.Files.UploadAsync(fullDropboxPath, WriteMode.Overwrite.Instance, body: fileStream);
            }
        }

        private async Task DownloadFromDropbox(string dropboxPath, string localPath)
        {
            using (var response = await MainWindow.Instance._dropboxService._client.Files.DownloadAsync(dropboxPath))
            {
                using (var fileStream = File.Create(localPath))
                {
                    (await response.GetContentAsStreamAsync()).CopyTo(fileStream);
                }
            }
            await Handler.HandleFolderSelection(CurrentPath);
        }

        private void RemoveItemFromSourceListBox(string path)
        {
            var itemToRemove = _items.FirstOrDefault(item => item.Path == path);
            if (itemToRemove != null)
            {
                _items.Remove(itemToRemove);
            }
        }

        private void RemoveItemFromOtherListBox(CustomListView listBox, string itemPath)
        {
            if (listBox != null)
            {
                var itemToRemove = listBox.Items.Cast<dynamic>().FirstOrDefault(i => i.Path == itemPath);
                if (itemToRemove != null)
                {
                    listBox._items.Remove(itemToRemove);
                }
            }
        }

        private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            if (!target.Exists)
            {
                target.Create();
            }

            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                DirectoryInfo newDir = target.CreateSubdirectory(dir.Name);
                CopyFilesRecursively(dir, newDir);
            }

            foreach (FileInfo file in source.GetFiles())
            {
                string targetPath = Path.Combine(target.FullName, file.Name);
                if (File.Exists(targetPath))
                {
                    // Если файл уже существует, добавляем к имени номер
                    int counter = 1;
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                    string fileExtension = Path.GetExtension(file.Name);
                    while (File.Exists(targetPath))
                    {
                        targetPath = Path.Combine(target.FullName, $"{fileNameWithoutExtension} ({counter}){fileExtension}");
                        counter++;
                    }
                }
                file.CopyTo(targetPath);
            }
        }

        private void FolderView_DragEnter(object sender, DragEventArgs e)
        {
            if (CurrentPath == null) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void FolderView_DragOver(object sender, DragEventArgs e)
        {
            if (CurrentPath == null) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private object GetItemAtPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(this, point);
            if (result != null)
            {
                DependencyObject obj = result.VisualHit;
                while (obj != null && !(obj is ListBoxItem))
                {
                    obj = VisualTreeHelper.GetParent(obj);
                }
                if (obj is ListBoxItem item)
                {
                    return ItemContainerGenerator.ItemFromContainer(item);
                }
            }
            return null;
        }
        #endregion

        #region Контекстное меню
        private void FolderView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.ActiveListBox = (CustomListView)sender;
            //var item1 = ItemContainerGenerator.ContainerFromItem((e.OriginalSource as FrameworkElement)?.DataContext) as ListBoxItem;
            //if (item1 != null)
            //{
            //    Console.WriteLine(new Size(item1.ActualWidth, item1.ActualHeight));
            //}
            var clickedItem = (e.OriginalSource as FrameworkElement)?.DataContext;
            if (clickedItem != null)
            {
                // Определяем, файл это или папка
                if (clickedItem is FileObject)
                {
                    ContextMenu = (ContextMenu)FindResource("FileContextMenu");
                }
                else if (clickedItem is FolderObject)
                {
                    ContextMenu = (ContextMenu)FindResource("FolderContextMenu");
                    var folderObject = (FolderObject)clickedItem;
                    MainWindow.AddContextMenuItems(ContextMenu, MainWindow.rr.DirectoryShellEntries, folderObject.Path);
                }
                else if (clickedItem is DriveObject)
                {
                    ContextMenu = (ContextMenu)FindResource("DriveContextMenu");
                    var driveObject = (DriveObject)clickedItem;
                    MainWindow.AddContextMenuItems(ContextMenu, MainWindow.rr.DirectoryShellEntries, driveObject.Path);
                }
                if (clickedItem is FolderObject fileSystemObject)
                {
                    var favoriteMenuItem = ContextMenu.Items.OfType<MenuItem>()
                        .FirstOrDefault(item => item.Name == "FavoriteMenuItem");

                    if (favoriteMenuItem == null)
                    {
                        // Если пункт меню не существует, создаем новый
                        favoriteMenuItem = new MenuItem { Name = "FavoriteMenuItem" };
                        ContextMenu.Items.Add(favoriteMenuItem);
                    }

                    // Обновляем заголовок и обработчик
                    favoriteMenuItem.Header = fileSystemObject.IsFavorite ? "Удалить из избранного" : "Добавить в избранное";
                    favoriteMenuItem.Click -= MainWindow.Instance.ToggleFavorite;
                    favoriteMenuItem.Click += MainWindow.Instance.ToggleFavorite;
                    var groupMenuItem = ContextMenu.Items.OfType<MenuItem>()
            .FirstOrDefault(item => item.Name == "GroupMenuItem");

                    if (groupMenuItem == null)
                    {
                        groupMenuItem = new MenuItem { Name = "GroupMenuItem", Header = "Группа" };
                        ContextMenu.Items.Add(groupMenuItem);
                    }

                    MainWindow.Instance.UpdateGroupContextMenu(groupMenuItem, clickedItem);
                }
            }
            else
            {
                // Клик на пустом месте
                ContextMenu = (ContextMenu)FindResource("EmptyContextMenu");
                if (CurrentPath != null)
                {
                    MainWindow.AddContextMenuItems(ContextMenu, MainWindow.rr.DirectoryBackgroundShellEntries, CurrentPath);
                    MenuItem existingCreateMenuItem = ContextMenu.Items.OfType<MenuItem>()
            .FirstOrDefault(item => item.Header.ToString() == "Создать");
                    if (existingCreateMenuItem == null)
                    {
                        MenuItem createMenuItem = new MenuItem();
                        createMenuItem.Header = "Создать";
                        foreach (var item in MainWindow.rr.CreateMenuItems)
                        {
                            MenuItem subItem = new MenuItem();
                            subItem.Header = item.MenuItemText.Replace("&", "_");
                            subItem.Command = new RelayCommand(() => MainWindow.CreateNewItem(item, CurrentPath));
                            if (item.IconPath != null)
                            {
                                subItem.Icon = new Image
                                {
                                    Source = IconHelper.GetSmallestExeIcon(item.IconPath)
                                };
                            }
                            createMenuItem.Items.Add(subItem);
                        }
                        ContextMenu.Items.Add(createMenuItem);
                    }
                }
            }

            // Открываем контекстное меню
            //ContextMenu.Background = (Brush)MainWindow.Instance.FindResource("RectangleBrush");
            //ContextMenu.Foreground = (Brush)MainWindow.Instance.FindResource("TextColorBrush");
            ContextMenu.IsOpen = true;
        }

        public void AdjustGridViewColumnWidths()
        {
            if (View is GridView gridView)
            {
                var totalWidth = ActualWidth - SystemParameters.VerticalScrollBarWidth;
                var columnCount = gridView.Columns.Count;

                // Если ширина столбцов еще не сохранена, инициализируем их равными значениями
                if (_columnWidths.Count != columnCount)
                {
                    _columnWidths = Enumerable.Repeat(totalWidth / columnCount, columnCount).ToList();
                }

                // Вычисляем общую ширину всех столбцов
                var totalColumnWidth = _columnWidths.Sum();

                // Корректируем ширину каждого столбца пропорционально
                for (int i = 0; i < columnCount; i++)
                {
                    var newWidth = Math.Max(0, _columnWidths[i] * totalWidth / totalColumnWidth);
                    gridView.Columns[i].Width = newWidth;
                    _columnWidths[i] = newWidth;
                }
            }
        }

        public void SwitchToTableView()
        {
            View = (GridView)FindResource("TableViewGridView");
            ItemTemplate = null;
            ItemContainerStyle = (Style)FindResource("GridViewItemStyle");
            Items.Refresh();
        }

        private void GridViewColumnHeaderContainerStyleSetter(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header != null)
            {
                header.SizeChanged += (s, args) =>
                {
                    var column = header.Column;
                    var columnIndex = (View as GridView).Columns.IndexOf(column);
                    if (columnIndex >= 0 && columnIndex < _columnWidths.Count)
                    {
                        _columnWidths[columnIndex] = column.ActualWidth;
                    }
                };
            }
        }
        public double _currentScale = 1.0;
        private const double SCALE_FACTOR = 1.1;
        private const double MIN_SCALE = 0.7;
        private const double MAX_SCALE = 2.3;
        private const double TABLE_VIEW_THRESHOLD = 0.7;
        private System.Windows.Threading.DispatcherTimer _scrollTimer;
        private double _targetVerticalOffset;

        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            var scrollViewer = GetScrollViewer();
            if (scrollViewer != null)
            {
                double currentOffset = scrollViewer.VerticalOffset;
                double difference = _targetVerticalOffset - currentOffset;

                if (Math.Abs(difference) < 0.1)
                {
                    scrollViewer.ScrollToVerticalOffset(_targetVerticalOffset);
                    _scrollTimer.Stop();
                }
                else
                {
                    // Используем фиксированный процент разницы вместо SCROLL_SPEED
                    double step = difference * 0.3; // 30% от разницы
                    scrollViewer.ScrollToVerticalOffset(currentOffset + step);
                }
            }
            else
            {
                _scrollTimer.Stop();
            }
        }


        private void CustomListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            //if (Keyboard.Modifiers == ModifierKeys.Control)
            //{
            //    e.Handled = true;
            //    if (e.Delta > 0)
            //        ZoomIn();
            //    else
            //        ZoomOut();
            //}
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                double newZoom = _currentScale * (e.Delta > 0 ? 1.1 : 0.9);
                newZoom = Math.Max(0.7, Math.Min(newZoom, 2.3)); // Ограничиваем зум от 10% до 500%
                ApplyZoom(newZoom);
                _currentScale = newZoom;
            }
            else
            {
                e.Handled = true;
                var scrollViewer = GetScrollViewer();
                if (scrollViewer != null)
                {
                    _targetVerticalOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight,
                        scrollViewer.VerticalOffset - e.Delta));

                    if (_scrollTimer == null)
                    {
                        _scrollTimer = new System.Windows.Threading.DispatcherTimer();
                        _scrollTimer.Interval = TimeSpan.FromMilliseconds(8); // ~60 FPS
                        _scrollTimer.Tick += ScrollTimer_Tick;
                    }

                    if (!_scrollTimer.IsEnabled)
                    {
                        _scrollTimer.Start();
                    }
                }
            }
        }

        private ScrollViewer GetScrollViewer()
        {
            return GetDescendantByType<ScrollViewer>(this);
        }
        private static T GetDescendantByType<T>(Visual element) where T : class
        {
            if (element == null) return null;
            if (element is T) return element as T;
            T foundElement = null;
            if (element is FrameworkElement)
            {
                (element as FrameworkElement).ApplyTemplate();
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                Visual visual = VisualTreeHelper.GetChild(element, i) as Visual;
                foundElement = GetDescendantByType<T>(visual);
                if (foundElement != null)
                    break;
            }
            return foundElement;
        }
        private void SmoothScroll(ScrollViewer scrollViewer, double targetVerticalOffset)
        {
            const int animationDuration = 300; // миллисекунды
            const int framesPerSecond = 60;
            double totalFrames = animationDuration / 1000.0 * framesPerSecond;
            double currentFrame = 0;

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000.0 / framesPerSecond);

            double startVerticalOffset = scrollViewer.VerticalOffset;
            double distance = targetVerticalOffset - startVerticalOffset;

            timer.Tick += (s, e) =>
            {
                currentFrame++;
                if (currentFrame <= totalFrames)
                {
                    double progress = currentFrame / totalFrames;
                    double easedProgress = EaseOutCubic(progress);
                    double newOffset = startVerticalOffset + (distance * easedProgress);
                    scrollViewer.ScrollToVerticalOffset(newOffset);
                }
                else
                {
                    timer.Stop();
                }
            };

            timer.Start();
        }

        private double EaseOutCubic(double t)
        {
            return 1 - Math.Pow(1 - t, 3);
        }
        //private void ZoomIn()
        //{
        //    _currentScale *= SCALE_FACTOR;
        //    ApplyZoom();
        //}

        //private void ZoomOut()
        //{
        //    _currentScale /= SCALE_FACTOR;
        //    ApplyZoom();
        //}

        //public void ApplyZoom()
        //{
        //    _currentScale = Math.Max(MIN_SCALE, Math.Min(_currentScale, MAX_SCALE));


        //    if (_currentScale == TABLE_VIEW_THRESHOLD)
        //    {
        //        if (!(View is GridView))
        //        {
        //            MainWindow.Instance.SetListViewPanel();
        //            SwitchToTableView();
        //        }
        //    }
        //    else if (_currentScale > TABLE_VIEW_THRESHOLD)// && _currentScale < LARGE_ICON_THRESHOLD)
        //    {
        //        if (View is GridView)
        //        {
        //            View = null;
        //            ItemTemplate = null;
        //            ItemContainerStyle = null;
        //            MainWindow.Instance.SetDefaultPanel();
        //        }
        //    }
        //    //if (_currentScale < TABLE_VIEW_THRESHOLD)
        //    //{

        //    //}
        //    //if (_currentScale < LARGE_ICON_THRESHOLD)
        //    //{
        //    //    if (View is GridView)
        //    //    {
        //    //        View = null;
        //    //        ItemTemplate = (DataTemplate)FindResource("ListViewTemplate");
        //    //        ItemContainerStyle = null;
        //    //    }
        //    //    //MainWindow.Instance.SetListViewPanel();
        //    //}
        //    //if (_currentScale > TABLE_VIEW_THRESHOLD)
        //    //{
        //    //    if (View is GridView)
        //    //    {
        //    //        View = null;
        //    //        ItemTemplate = null;
        //    //        ItemContainerStyle = null;
        //    //    }
        //    //    //MainWindow.Instance.SetDefaultPanel();
        //    //}

        //    // Применяем масштаб к элементам
        //    Dispatcher.BeginInvoke(new Action(() =>
        //    {
        //        for (int i = 0; i < Items.Count; i++)
        //        {
        //            var item = Items[i];
        //            var container = ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
        //            if (container != null)
        //            {
        //                if (View is GridView)
        //                {
        //                    // Для табличного вида масштабируем только иконку
        //                    //var iconElement = FindVisualChild<Image>(container);
        //                    //if (iconElement != null)
        //                    //{
        //                    //    iconElement.LayoutTransform = new ScaleTransform(_currentScale, _currentScale);
        //                    //}
        //                }
        //                else
        //                {
        //                    // Для других видов масштабируем весь контейнер
        //                    container.LayoutTransform = new ScaleTransform(_currentScale, _currentScale);
        //                }
        //            }
        //        }

        //        // Обновляем размеры столбцов для табличного вида
        //        if (View is GridView)
        //        {
        //            AdjustGridViewColumnWidths(); // вызывается всё время при уменьшении в табличном виде
        //        }
        //    }), System.Windows.Threading.DispatcherPriority.Loaded);
        //}

        public void ApplyZoom(double zoomFactor)
        {
            var panel = FindVisualChild<VirtualizingWrapPanel>(this);
            if (panel != null)
            {
                panel.UpdateZoom(zoomFactor);
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        public void Refresh()
        {
            if (CurrentPath != null)
            {
                Handler.HandleFolderSelection(CurrentPath);
            }
            else
            {
                Handler.LoadLogicalDrives();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.C)
                {
                    CopySelectedItems();
                }
                else if (e.Key == Key.V)
                {
                    PasteItems();
                }
            }
        }

        private void CopySelectedItems()
        {
            var selectedPaths = SelectedItems.Cast<FileSystemObject>()
                .Select(item => item.Path)
                .ToArray();

            if (selectedPaths.Length > 0)
            {
                var dataObject = new DataObject(DataFormats.FileDrop, selectedPaths);
                dataObject.SetData("Preferred DropEffect", DragDropEffects.Copy);
                Clipboard.SetDataObject(dataObject);
            }
        }

        //private void PasteItems()
        //{
        //    if (Clipboard.ContainsFileDropList() && CurrentPath != null)
        //    {
        //        var filePaths = Clipboard.GetFileDropList().Cast<string>().ToArray();
        //        var targetPath = CurrentPath;

        //        var shell = new Shell32.Shell();
        //        var folder = shell.NameSpace(targetPath);

        //        foreach (var filePath in filePaths)
        //        {
        //            var fileItem = shell.NameSpace(Path.GetDirectoryName(filePath)).ParseName(Path.GetFileName(filePath));

        //            if (File.Exists(Path.Combine(targetPath, Path.GetFileName(filePath))))
        //            {
        //                var result = MessageBox.Show(
        //                    $"Файл {Path.GetFileName(filePath)} уже существует. Заменить?",
        //                    "Подтверждение замены",
        //                    MessageBoxButton.YesNoCancel,
        //                    MessageBoxImage.Question);

        //                if (result == MessageBoxResult.Cancel)
        //                {
        //                    continue;
        //                }
        //                else if (result == MessageBoxResult.No)
        //                {
        //                    continue;
        //                }
        //            }

        //            folder.CopyHere(fileItem, 0);
        //        }

        //        Refresh();
        //    }
        //}
        private async void PasteItems()
        {
            if (Clipboard.ContainsFileDropList() && CurrentPath != null)
            {
                var filePaths = Clipboard.GetFileDropList().Cast<string>().ToArray();
                var targetPath = CurrentPath;

                ProgressWindow progressWindow = new ProgressWindow();
                progressWindow.Show();

                try
                {
                    int totalFiles = filePaths.Length;
                    int processedFiles = 0;

                    foreach (var filePath in filePaths)
                    {
                        if (File.Exists(Path.Combine(targetPath, Path.GetFileName(filePath))))
                        {
                            var result = MessageBox.Show(
                                $"Файл {Path.GetFileName(filePath)} уже существует. Заменить?",
                                "Подтверждение замены",
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Question);

                            if (result == MessageBoxResult.Cancel)
                            {
                                continue;
                            }
                            else if (result == MessageBoxResult.No)
                            {
                                continue;
                            }
                        }

                        await MoveOrCopyItem(filePath, targetPath, progressWindow, processedFiles, totalFiles);
                        processedFiles++;
                    }
                }
                finally
                {
                    progressWindow.Close();
                }

                Refresh();
            }
        }
        private async Task MoveOrCopyItem(string sourcePath, string targetPath, ProgressWindow progressWindow, int processedFiles, int totalFiles)
        {

            string fileName = Path.GetFileName(sourcePath);
            string destPath = Path.Combine(targetPath, fileName);
            string sourceDirectory = Path.GetDirectoryName(sourcePath);
            progressWindow.UpdateProgress($"Обработка: {fileName}", (processedFiles * 100) / totalFiles);
            if (sourcePath.StartsWith("dropbox://") && targetPath.StartsWith("dropbox://"))
            {
                string sourceDropboxPath = sourcePath.Substring(10);
                string targetDropboxPath = Path.Combine(targetPath.Substring(10), Path.GetFileName(sourcePath)).Replace("\\", "/");
                await MainWindow.Instance._dropboxService._client.Files.MoveV2Async(sourceDropboxPath, targetDropboxPath);
            }
            else if (sourcePath.StartsWith("dropbox://"))
            {
                // Загрузка из Dropbox и сохранение локально
                await DownloadFromDropbox(sourcePath.Substring(10), Path.Combine(targetPath, Path.GetFileName(sourcePath)));
            }
            else if (targetPath.StartsWith("dropbox://"))
            {
                // Загрузка локального файла в Dropbox
                await UploadToDropbox(sourcePath, targetPath.Substring(10));
            }
            else
            {
                if (Directory.Exists(sourcePath))
                {
                    if (Path.GetPathRoot(sourcePath) == Path.GetPathRoot(targetPath))
                    {
                        // Если папки на одном диске, перемещаем
                        if (Directory.Exists(destPath))
                        {
                            // Если целевая папка уже существует, создаем новую с уникальным именем
                            int counter = 1;
                            string newDestPath = destPath;
                            while (Directory.Exists(newDestPath))
                            {
                                newDestPath = Path.Combine(targetPath, $"{fileName} ({counter})");
                                counter++;
                            }
                            destPath = newDestPath;
                        }
                        Directory.Move(sourcePath, destPath);
                    }
                    else
                    {
                        // Если на разных дисках, копируем
                        CopyFilesRecursively(new DirectoryInfo(sourcePath), new DirectoryInfo(destPath));
                    }
                }
                else if (File.Exists(sourcePath))
                {
                    if (Path.GetPathRoot(sourcePath) == Path.GetPathRoot(destPath))
                    {
                        File.Move(sourcePath, destPath);
                    }
                    else
                    {
                        File.Copy(sourcePath, destPath, true);
                    }
                }
            }

            RemoveItemFromSourceListBox(sourcePath);
            MainWindow.Instance.UpdateListBoxesWithPath(sourceDirectory);
            if (targetPath != sourceDirectory)
            {
                MainWindow.Instance.UpdateListBoxesWithPath(targetPath);
            }
            progressWindow.UpdateProgress($"Завершено: {fileName}", ((processedFiles + 1) * 100) / totalFiles);
        }
        #endregion
    }
}