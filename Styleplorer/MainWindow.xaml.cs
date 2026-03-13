using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.Text.Json;
using System.ComponentModel;
using SharpCompress.Common;
using SharpCompress.Writers;
using System.Globalization;
using System.Windows.Data;

namespace Styleplorer
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } // Ссылка на окно

        public static RegistryReader rr = new(); // Регистр для пунктов меню
        public static CustomListView ActiveListBox; // Выбранный (активный) ListView.
        public static TabControl ActiveTabContol; // Выбранный (активный) TabControl (грубо говоря выбранная панель)

        public static event Action<CustomListView> ElementsChanged; // Событие на изменение количества элементов
        public static event Action<CustomListView> SelectionChanged; // Событие на изменение количества выделенных элементов
        public static event Action<CustomListView> CurrentPathChanged; // Событие на изменение пути
        public static DataTemplateSelector dataTemplateSelector; // Определяет шаблон для элемента (файл, папка или диск)
        public static string selectedView = "Big"; // Выбранный вид. Пока что только назначается, но не используется.

        #region Переменные для перетаскивания вкладки
        private TabItem draggedTabItem; // Текущая перемещаемая вкладка
        private Point startPointTab;
        private bool isDraggingTab;
        #endregion

        #region Переменные для тем
        public Dictionary<string, object> themeSettings = new(); // Настройки тем
        private string themesSettingsFilePath; // Путь к файлу с настройками тем 
        private List<Theme> themes; // Список сохранённых тем
        private Theme lastSelectedTheme; // Последняя выбранная тема
        public string selectedMonitor = "Monitor0";
        #endregion

        public DropboxService _dropboxService; // DropBox

        #region Переменные для иконок
        public static readonly BitmapImage folderInsideFolderIcon = new(); // Иконка папки с папками
        public static readonly BitmapImage standardFolderIcon = new(); // Иконка стандартной папки проводника windows (пустой)
        public static readonly BitmapImage standardFolderIcon1 = new(); // Иконка папки (нижняя часть)
        public static readonly BitmapImage standardFolderIcon2 = new(); // Иконка папки (верхняя часть)
        public static readonly BitmapImage driveIcon = new(); // Иконка диска
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Instance = this;
            SourceInitialized += Window_SourceInitialized;
            InitializeIcons();
            rr.ReadContextMenuRegistryKeys();
            rr.ReadCreateMenuItems();
            dataTemplateSelector = (DataTemplateSelector)FindResource("FileFolderTemplateSelector");
            AppSettings.LoadSettings();
            AppSettings.ApplyWindowPosition(this);


            ActiveTabContol = TabControl1;
            InitializeTabControl(TabControl1);
            InitializeTabControl(TabControl2);
            CustomListView FolderView = new CustomListView();
            FolderView.Name = "FolderView1";
            AddNewTab(FolderView, "Этот компьютер", TabControl1, true, true);
            //CustomListView FolderView2 = new CustomListView();
            //FolderView2.Name = "FolderView1";
            //AddNewTab(FolderView2, "+", TabControl1, false, false);
            CustomListView FolderView3 = new CustomListView();
            FolderView3.Name = "FolderView2";
            AddNewTab(FolderView3, "Этот компьютер", TabControl2, true, true);
            //CustomListView FolderView4 = new CustomListView();
            //FolderView4.Name = "FolderView2";
            //AddNewTab(FolderView4, "+", TabControl2, false, false);

            Elements1.Text = "Элементов: " + FolderView.Items.Count;
            Elements2.Text = "Элементов: " + FolderView3.Items.Count;
            FolderView.Focus();
            ActiveListBox = FolderView;
            ActiveTabContol = TabControl1;
            ActiveListBox.View = null;
            LoadFavorites();
            LoadGroups();
            SetThemesFilePath();
            LoadThemes();
            DesktopColorAnalyzer.PopulateMonitorComboBox();
            LoadThemeSettings();
            PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
            PreviewMouseLeftButtonUp += Window_PreviewMouseLeftButtonUp;
            PreviewMouseMove += Window_PreviewMouseMove;
            LostMouseCapture += Window_LostMouseCapture;
            SizeChanged += Window_SizeChanged;
            ElementsChanged += OnElementsChanged;
            SelectionChanged += OnSelectionChanged;
            CurrentPathChanged += OnCurrentPathChanged;
            TabControl1.MouseMove += TabControl_MouseMove;
            TabControl1.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
            TabControl1.PreviewMouseLeftButtonUp += TabControl_PreviewMouseLeftButtonUp;

            TabControl2.MouseMove += TabControl_MouseMove;
            TabControl2.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
            TabControl2.PreviewMouseLeftButtonUp += TabControl_PreviewMouseLeftButtonUp;
            window.PreviewMouseDown += async (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.XButton1)
                {
                    string? path;
                    if (ActiveListBox.IsMouseOver)
                    {
                        path = ActiveListBox.NavManager.NavigateBack(ActiveListBox);
                        if (path != null)
                        {
                            await ActiveListBox.Handler.HandleFolderSelection(path);
                        }
                        else
                        {
                            ActiveListBox.Handler.LoadLogicalDrives();
                        }
                    }
                    e.Handled = true;
                }
                else if (e.ChangedButton == MouseButton.XButton2)
                {
                    string? path;
                    if (ActiveListBox.IsMouseOver)
                    {
                        path = ActiveListBox.NavManager.NavigateForward(ActiveListBox);
                        if (path != null)
                        {
                            await ActiveListBox.Handler.HandleFolderSelection(path);
                        }
                    }
                    e.Handled = true;
                }
            };

            DropBox.Click += ActiveListBox.Handler.DropboxButton_Click;
        }

        private void InitializeIcons() // Загрузка иконок и их настройка
        {
            folderInsideFolderIcon.BeginInit();
            folderInsideFolderIcon.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/FolderInsideFolder.jpg", UriKind.RelativeOrAbsolute);
            folderInsideFolderIcon.DecodePixelWidth = 100;
            folderInsideFolderIcon.CacheOption = BitmapCacheOption.OnDemand;
            folderInsideFolderIcon.EndInit();

            standardFolderIcon.BeginInit();
            standardFolderIcon.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/StandartFolder.jpg", UriKind.RelativeOrAbsolute);
            standardFolderIcon.DecodePixelWidth = 100;
            standardFolderIcon.CacheOption = BitmapCacheOption.OnDemand;
            standardFolderIcon.EndInit();

            standardFolderIcon1.BeginInit();
            standardFolderIcon1.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/StandartFolder1.jpg", UriKind.RelativeOrAbsolute);
            standardFolderIcon1.DecodePixelWidth = 100;
            standardFolderIcon1.CacheOption = BitmapCacheOption.OnDemand;
            standardFolderIcon1.EndInit();

            standardFolderIcon2.BeginInit();
            standardFolderIcon2.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/StandartFolder2.jpg", UriKind.RelativeOrAbsolute);
            standardFolderIcon2.DecodePixelWidth = 100;
            standardFolderIcon2.CacheOption = BitmapCacheOption.OnDemand;
            standardFolderIcon2.EndInit();

            driveIcon.BeginInit();
            driveIcon.UriSource = new Uri("pack://application:,,,/Styleplorer;component/Resources/SSD SATA.png", UriKind.RelativeOrAbsolute);
            driveIcon.DecodePixelWidth = 100;
            driveIcon.CacheOption = BitmapCacheOption.OnDemand;
            driveIcon.EndInit();
        }

        #region Обработка перемещения вкладки
        private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //ActiveListBox.SelectedItems.Clear();
            var tabItem = FindParent<TabItem>((DependencyObject)e.OriginalSource);
            if (tabItem != null && tabItem.Header.ToString() != "+")
            {
                draggedTabItem = tabItem;
                startPointTab = e.GetPosition(null);
                isDraggingTab = false;
            }
        }

        private void TabControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedTabItem == null)
                return;

            Point currentPosition = e.GetPosition(null);
            Vector difference = startPointTab - currentPosition;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(difference.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(difference.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                if (!isDraggingTab)
                {
                    isDraggingTab = true;
                    DragDrop.DoDragDrop(draggedTabItem, draggedTabItem, DragDropEffects.Move);
                }
            }
        }

        private void TabControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            draggedTabItem = null;
            isDraggingTab = false;
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            if (draggedTabItem != null && sender is TabControl targetTabControl)
            {
                TabControl sourceTabControl = FindParent<TabControl>(draggedTabItem);
                int sourceIndex = sourceTabControl.Items.IndexOf(draggedTabItem);
                int targetIndex = GetInsertIndex(targetTabControl, e.GetPosition(targetTabControl));

                if (sourceTabControl != targetTabControl || sourceIndex != targetIndex)
                {
                    sourceTabControl.Items.RemoveAt(sourceIndex);

                    TabItem plusTab = targetTabControl.Items.OfType<TabItem>().FirstOrDefault(t => t.Header.ToString() == "+");

                    if (plusTab != null)
                    {
                        targetTabControl.Items.Remove(plusTab);
                    }

                    if (targetIndex >= targetTabControl.Items.Count)
                    {
                        targetTabControl.Items.Add(draggedTabItem);
                    }
                    else
                    {
                        targetTabControl.Items.Insert(targetIndex, draggedTabItem);
                    }

                    if (plusTab != null)
                    {
                        targetTabControl.Items.Add(plusTab);
                    }

                    targetTabControl.SelectedItem = draggedTabItem;
                }
            }
        }


        private int GetInsertIndex(TabControl tabControl, Point dropPosition) // Определяет позицию перемещения вкладки. Вкладка не может стоять после вкладки "+"
        {
            int insertIndex = tabControl.Items.Count;

            for (int i = 0; i < tabControl.Items.Count; i++)
            {
                var tabItem = tabControl.ItemContainerGenerator.ContainerFromIndex(i) as TabItem;
                if (tabItem != null)
                {
                    var tabRect = VisualTreeHelper.GetDescendantBounds(tabItem);
                    var tabPosition = tabItem.TranslatePoint(new Point(0, 0), tabControl);
                    var tabWidth = tabRect.Width;

                    if (dropPosition.X < tabPosition.X + tabWidth / 2)
                    {
                        insertIndex = i;
                        break;
                    }
                }
            }

            return Math.Max(0, Math.Min(insertIndex, tabControl.Items.Count));
        }

        public void InitializeTabControl(TabControl tabControl) // Инициализация TabControl при его создании (просто добавлеяет пустую вкладку)
        {
            CustomListView FolderView = new CustomListView();
            var plusTab = new TabItem
            {
                Header = "+",
                Width = 50,
                Style = (Style)FindResource("BaseTabItemStyle"),
                Content = FolderView
            };

            tabControl.Items.Add(plusTab);
        }
        #endregion

        // Добавление вкладки
        public void AddNewTab(object content, string header, TabControl tabControl, bool isSelected = true, bool isClosed = true)
        {
            Style closableStyle = null;
            Style baseStyle = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                closableStyle = (Style)FindResource("ClosableTabItemStyle");
                baseStyle = (Style)FindResource("BaseTabItemStyle");
            });

            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TabItem newTab = new()
                    {
                        Style = isClosed ? closableStyle : baseStyle,
                        MaxWidth = isClosed ? 300 : 50,
                        Width = isClosed ? double.NaN : 50,
                        Header = header,
                        Content = content,
                        Background = Brushes.Transparent,
                        Foreground = Brushes.White
                    };
                    if (isClosed)
                    {
                        tabControl.Items.Insert(tabControl.Items.Count - 1, newTab); // Вставляем перед вкладкой "+"
                    }
                    else
                    {
                        tabControl.Items.Add(newTab);
                    }
                    if (isSelected)
                    {
                        tabControl.SelectedItem = newTab;
                    }
                });
            });
        }

        // Обработчик для кнопки для закрытия вкладки
        public void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var tabItem = button.Tag as TabItem;
            if (tabItem != null)
            {
                TabControl tabControl = FindParent<TabControl>(tabItem);
                if (tabControl != null)
                {
                    tabControl.Items.Remove(tabItem);
                    _searchCancellationTokenSource?.Cancel();
                }
            }
        }

        #region Обработка изменения размеров окна
        private double firstColumnWidth; // Длина первого столбца (панели)
        private double secondColumnWidth; // Длина второго столбца (панели)

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (TabControl2.Visibility == Visibility.Visible)
            {
                if (firstColumnWidth != 0 && secondColumnWidth != 0)
                {
                    FirstColumn.Width = new GridLength(firstColumnWidth, GridUnitType.Star);
                    SecondColumn.Width = new GridLength(secondColumnWidth, GridUnitType.Star);
                    firstColumnWidth = FirstColumn.ActualWidth;
                    secondColumnWidth = SecondColumn.ActualWidth;
                }
                else
                {
                    FirstColumn.Width = new GridLength(1, GridUnitType.Star);
                    SecondColumn.Width = new GridLength(1, GridUnitType.Star);
                    firstColumnWidth = FirstColumn.ActualWidth;
                    secondColumnWidth = SecondColumn.ActualWidth;
                }

            }
            else
            {
                FirstColumn.Width = new GridLength(1, GridUnitType.Star);
                SecondColumn.Width = new GridLength(0);
                firstColumnWidth = FirstColumn.ActualWidth;
            }
        }

        private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            firstColumnWidth = FirstColumn.ActualWidth;
            secondColumnWidth = SecondColumn.ActualWidth;
        }
        #endregion

        // Обработчик клика на кнопку, который показывает/скрывает вторую панель
        private void ShowSecondListBox_Click(object sender, RoutedEventArgs e)
        {
            ShowSecondListBox();
        }

        // Функция для показа/скрытия второй панели
        public void ShowSecondListBox()
        {
            if (TabControl2.Visibility == Visibility.Collapsed)
            {
                TabControl2.Visibility = Visibility.Visible;
                OpenSecondListBtn.Content = "<<";

                if (secondColumnWidth == 0)
                {
                    if (firstColumnWidth != 0)
                    {
                        MainGrid.ColumnDefinitions[3].Width = new GridLength(firstColumnWidth);
                        MainGrid.ColumnDefinitions[2].Width = new GridLength(3);
                        Width += firstColumnWidth;
                    }
                    else
                    {
                        MainGrid.ColumnDefinitions[3].Width = new GridLength(TabControl1.ActualWidth);
                        MainGrid.ColumnDefinitions[2].Width = new GridLength(3);
                        Width += TabControl1.ActualWidth;
                    }
                }
                else
                {
                    MainGrid.ColumnDefinitions[3].Width = new GridLength(secondColumnWidth);
                    MainGrid.ColumnDefinitions[2].Width = new GridLength(3);
                    Width += secondColumnWidth;
                }
                Splitter.Visibility = Visibility.Visible;
                SelectionCanvas2.Visibility = Visibility.Hidden;
            }
            else
            {
                TabControl2.Visibility = Visibility.Collapsed;
                SelectionCanvas2.Visibility = Visibility.Hidden;
                OpenSecondListBtn.Content = ">>";
                MainGrid.ColumnDefinitions[3].Width = new GridLength(0);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
                Splitter.Visibility = Visibility.Hidden;

                if (secondColumnWidth == 0)
                {
                    if (firstColumnWidth != 0)
                    {
                        Width -= firstColumnWidth;
                    }
                    else
                    {
                        Width -= TabControl1.ActualWidth;
                    }
                }
                else
                {
                    Width -= secondColumnWidth;
                }
            }
        }

        #region Обработка стандартного поведения окна
        // Прямоугольник для перемещения
        private void captionRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
            }
            else
            {
                DragMove();
            }
        }

        // Функция для изменения состояния размеров окна
        private void ToggleWindowState()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        // Скрыть окно
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // Окно на весь экран или наоборот
        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        // Закрыть окно
        private void CloseClick(object sender, RoutedEventArgs e)
        {
            if (AppSettings.RememberWindowSize)
            {
                AppSettings.WindowWidth = Width;
                AppSettings.WindowHeight = Height;
                AppSettings.WindowLeft = Left;
                AppSettings.WindowTop = Top;
                AppSettings.SaveSettings();
            }
            Close();
        }

        #region Обработка изменения размеров окна
        private const int WM_SYSCOMMAND = 0x112;
        private HwndSource hwndSource;

        private enum ResizeDirection
        {
            Left = 61441,
            Right = 61442,
            Top = 61443,
            TopLeft = 61444,
            TopRight = 61445,
            Bottom = 61446,
            BottomLeft = 61447,
            BottomRight = 61448,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            hwndSource = PresentationSource.FromVisual((Visual)sender) as HwndSource;
        }

        private void ResizeWindow(ResizeDirection direction)
        {
            SendMessage(hwndSource.Handle, WM_SYSCOMMAND, (IntPtr)direction, IntPtr.Zero);
        }

        protected void ResetCursor(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                Cursor = Cursors.Arrow;
            }
        }

        public static bool IsResize = false;
        protected void Resize(object sender, MouseButtonEventArgs e)
        {
            if (!IsDragging)
            {
                IsResize = true;
                if (WindowState == WindowState.Normal)
                {
                    var clickedShape = sender as Shape;

                    switch (clickedShape.Name)
                    {
                        case "ResizeN":
                            this.Cursor = Cursors.SizeNS;
                            ResizeWindow(ResizeDirection.Top);
                            break;
                        case "ResizeE":
                            this.Cursor = Cursors.SizeWE;
                            ResizeWindow(ResizeDirection.Right);
                            break;
                        case "ResizeS":
                            this.Cursor = Cursors.SizeNS;
                            ResizeWindow(ResizeDirection.Bottom);
                            break;
                        case "ResizeW":
                            this.Cursor = Cursors.SizeWE;
                            ResizeWindow(ResizeDirection.Left);
                            break;
                        case "ResizeNW":
                            this.Cursor = Cursors.SizeNWSE;
                            ResizeWindow(ResizeDirection.TopLeft);
                            break;
                        case "ResizeNE":
                            this.Cursor = Cursors.SizeNESW;
                            ResizeWindow(ResizeDirection.TopRight);
                            break;
                        case "ResizeSE":
                            this.Cursor = Cursors.SizeNWSE;
                            ResizeWindow(ResizeDirection.BottomRight);
                            break;
                        case "ResizeSW":
                            this.Cursor = Cursors.SizeNESW;
                            ResizeWindow(ResizeDirection.BottomLeft);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        protected void DisplayResizeCursor(object sender, MouseEventArgs e)
        {
            if (!IsDragging)
            {
                if (WindowState == WindowState.Normal)
                {
                    var clickedShape = sender as Shape;

                    switch (clickedShape.Name)
                    {
                        case "ResizeN":
                        case "ResizeS":
                            this.Cursor = Cursors.SizeNS;
                            break;
                        case "ResizeE":
                        case "ResizeW":
                            this.Cursor = Cursors.SizeWE;
                            break;
                        case "ResizeNW":
                        case "ResizeSE":
                            this.Cursor = Cursors.SizeNWSE;
                            break;
                        case "ResizeNE":
                        case "ResizeSW":
                            this.Cursor = Cursors.SizeNESW;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        #endregion

        #endregion

        // Функция для вывода в консоль результаты полученые с регистра для пунктов меню для отладки (нигде не используется)
        private static void PrintRegistryEntry(RegistryEntry entry)
        {
            Console.WriteLine($"Name: {entry.Name}");
            Console.WriteLine($"MenuItemText: {entry.MenuItemText}");
            Console.WriteLine($"IconPath: {entry.IconPath}");
            Console.WriteLine($"CLSIDInfo: {entry.CLSIDInfo}");
            Console.WriteLine($"Command: {entry.Command}");


            Console.WriteLine("Values:");
            foreach (var kvp in entry.Values)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine();
        }

        #region Выделяемый прямоугольник
        private Point? dragStart = null;
        private RectangleAdorner adorner;

        public static bool IsDragging { get; set; }

        public Canvas selectionCanvas = null;
        TabControl selectedTab = null;

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if (e.OriginalSource is not ScrollViewer)
            //{
            //    return;
            //}
            var tabControl = FindParent<TabControl>(e.OriginalSource as DependencyObject);
            selectedTab = tabControl;
            if (selectedTab != null)
            {
                if (selectedTab.Name == "TabControl1")
                {
                    selectionCanvas = SelectionCanvas;
                }
                else if (selectedTab.Name == "TabControl2")
                {
                    selectionCanvas = SelectionCanvas2;
                }
            }
            var mousePosition = e.GetPosition(selectionCanvas);
            if (ActiveListBox != null && selectionCanvas != null && selectedTab != null)
            {
                if (!IsPointOnListItem(mousePosition, ActiveListBox) && e.OriginalSource is ScrollViewer)
                {
                    //ClearListBoxSelections();
                    ActiveListBox.Focus();
                    dragStart = mousePosition;
                    IsDragging = true;
                    var adornerLayer = AdornerLayer.GetAdornerLayer(selectionCanvas);
                    adorner = new RectangleAdorner(selectionCanvas, dragStart);
                    adornerLayer.Add(adorner);
                    selectionCanvas.Visibility = Visibility.Visible;
                    selectionCanvas.IsHitTestVisible = true;
                    Mouse.Capture(this, CaptureMode.SubTree);
                }
                //else
                //{
                //    e.Handled = false;
                //}
            }
        }
        private void ClearListBoxSelections()
        {
            var listBoxes = FindVisualChildren<CustomListView>(Application.Current.MainWindow);

            foreach (var listBox in listBoxes)
            {
                listBox.SelectedItems.Clear();
            }
        }

        public void UpdateListBoxesWithPath(string path)
        {
            var listBoxes = FindVisualChildren<CustomListView>(Application.Current.MainWindow);

            foreach (var listBox in listBoxes)
            {
                if (listBox.CurrentPath == path)
                {
                    listBox.Refresh();
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            //if (e.OriginalSource is Button || e.OriginalSource is Border || e.OriginalSource is Rectangle || e.OriginalSource is GridSplitter)
            //{
            //    return;
            //}
            //if (!(e.OriginalSource is ScrollViewer))
            //{
            //    return;
            //}
            if (dragStart != null)
            {
                var mousePosition = e.GetPosition(selectionCanvas);

                if (ActiveListBox != null && selectionCanvas != null && selectedTab != null)
                {
                    foreach (var item in ActiveListBox.Items)
                    {
                        var listBoxItem = (ListBoxItem)ActiveListBox.ItemContainerGenerator.ContainerFromItem(item);
                        if (listBoxItem != null)
                        {
                            var itemTopLeft = listBoxItem.TransformToAncestor(ActiveListBox).Transform(new Point(0, 0));
                            var itemBottomRight = listBoxItem.TransformToAncestor(ActiveListBox).Transform(new Point(listBoxItem.ActualWidth, listBoxItem.ActualHeight));
                            var itemRect = new Rect(itemTopLeft, itemBottomRight);
                            var selectionRect = new Rect(dragStart.Value, mousePosition);

                            listBoxItem.IsSelected = IsRectangleIntersectingRectangle(itemRect, selectionRect);

                        }
                    }

                    UpdateAdorner(mousePosition, selectionCanvas);
                }
            }
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //if (e.OriginalSource is Button || e.OriginalSource is CheckBox)
            //{
            //    return;
            //}
            //if (e.Source != null)
            //{
            //    if (e.Source is CustomListView listBox)
            //    {
            //        ActiveListBox = listBox;
            //        listBox.Focus();
            //    }
            //}
            IsResize = false;
            if (selectionCanvas != null && IsDragging)
            {
                dragStart = null;
                IsDragging = false;
                if (adorner != null)
                {
                    AdornerLayer.GetAdornerLayer(selectionCanvas).Remove(adorner);
                    adorner = null;
                }
                selectionCanvas.Visibility = Visibility.Hidden;
                selectionCanvas.IsHitTestVisible = false;
                Mouse.Capture(null);
                return;
            }
        }

        private void Window_LostMouseCapture(object sender, MouseEventArgs e)
        {
            dragStart = null;

            if (adorner != null)
            {
                AdornerLayer.GetAdornerLayer(selectionCanvas).Remove(adorner);
                adorner = null;
                selectionCanvas.Visibility = Visibility.Hidden;
            }
        }

        private void UpdateAdorner(Point mousePosition, Canvas selectionCanvas)
        {
            if (adorner != null)
            {
                if (selectedTab != null)
                {
                    Point canvasPosition = selectionCanvas.TranslatePoint(new Point(0, 0), MainGrid);
                    if (selectedTab.Name == TabControl2.Name)
                    {
                        canvasPosition.X -= TabControl1.ActualWidth;
                    }
                    canvasPosition.X -= SideMenu.ActualWidth;
                    canvasPosition.Y -= 50;
                    var clipGeometry = new RectangleGeometry(new Rect(canvasPosition.X, canvasPosition.Y, selectionCanvas.ActualWidth, selectionCanvas.ActualHeight));
                    adorner.Clip = clipGeometry;
                    adorner.Update(mousePosition);
                }
            }
        }

        private bool IsRectangleIntersectingRectangle(Rect rectangle1, Rect rectangle2)
        {
            var inflatedRectangle2 = rectangle2;
            inflatedRectangle2.Inflate(10, 10);

            return rectangle1.IntersectsWith(inflatedRectangle2);
        }

        private bool IsPointOnListItem(Point point, ListBox folderView)
        {
            if (folderView != null && point != null)
            {
                var hitTestResult = VisualTreeHelper.HitTest(folderView, point);
                if (hitTestResult != null)
                {
                    var listBoxItem = FindParent<ListBoxItem>(hitTestResult.VisualHit);
                    return listBoxItem != null;
                }
            }
            return false;
        }

        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null) return parent;
            else return FindParent<T>(parentObject);
        }
        #endregion

        #region Контекстное меню
        public static async void CreateNewItem(RegistryEntry item, string selectedPath)
        {
            string extension = item.Name.TrimStart('.');
            string newItemName = GetUniqueItemName(selectedPath, item.Name, item.MenuItemText);
            string newItemPath = Path.Combine(selectedPath, newItemName);

            if (extension.Equals("Folder", StringComparison.OrdinalIgnoreCase))
            {
                //Directory.CreateDirectory(newItemPath);
                var folderItem = new FolderObject
                {
                    Name = newItemName,
                    Path = newItemPath,
                    StandartFolderIcon1 = standardFolderIcon1,
                    StandartFolderIcon2 = standardFolderIcon2
                };
                await AddNewItemToListAndRename(folderItem);
            }
            else
            {
                //using (File.Create(newItemPath)) { }
                var fileItem = new FileObject
                {
                    Name = newItemName,
                    Path = newItemPath,
                    Icon = await ActiveListBox.Handler.iconCache.GetIcon(newItemPath)
                };
                await AddNewItemToListAndRename(fileItem);
            }
        }

        private static string GetUniqueItemName(string path, string extension, string text)
        {
            string baseName = extension.Equals("Folder", StringComparison.OrdinalIgnoreCase) ? "Новая папка" : $"{text}{extension}";
            string name = baseName;
            int counter = 1;

            while (File.Exists(Path.Combine(path, name)) || Directory.Exists(Path.Combine(path, name)))
            {
                name = extension.Equals("Folder", StringComparison.OrdinalIgnoreCase)
            ? $"{baseName} ({counter})"
            : $"{text} ({counter}){extension}";
                counter++;
            }

            return name;
        }
        public static bool isRenaming;
        public static bool isRenamingInProgress = false;
        private static async Task AddNewItemToListAndRename(FileSystemObject newItem)
        {
            isRenaming = true;
            ActiveListBox._items.Add(newItem);
            ActiveListBox.UpdateLayout();
            if ((bool)Instance.themeSettings["isWallpaperMonitor"] == true)
            {
                DesktopColorAnalyzer.AnalyzeAndApplyColors(Instance);
            }

            var listBoxItem = ActiveListBox.ItemContainerGenerator.ContainerFromItem(newItem) as ListBoxItem;
            if (listBoxItem == null) return;

            var textBox = FindVisualChild<TextBox>(listBoxItem);
            if (textBox == null) return;

            textBox.IsReadOnly = false;
            textBox.Focusable = true;
            textBox.Background = Brushes.White;
            textBox.Foreground = Brushes.Black;
            textBox.BorderBrush = Brushes.Gray;
            textBox.BorderThickness = new Thickness(1);

            textBox.SelectAll();
            textBox.Focus();

            //textBox.LostFocus += async (s, e) =>
            //{
            //    if (!isRenamingInProgress)
            //    {
            //        await FinishRenaming(newItem, textBox);
            //    }
            //};
            textBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter && !isRenamingInProgress)
                {
                    await FinishRenaming(newItem, textBox);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelRenaming(newItem);
                    e.Handled = true;
                }
            };
        }
        private static async Task FinishRenaming(FileSystemObject item, TextBox textBox)
        {
            if (isRenamingInProgress) return;
            isRenamingInProgress = true;
            string newName = textBox.Text;

            if (string.IsNullOrWhiteSpace(newName))
            {
                CancelRenaming(item);
                isRenamingInProgress = false;
                return;
            }
            if (!ContainsInvalidChars(newName))
            {
                string newPath = Path.Combine(Path.GetDirectoryName(item.Path), newName);
                try
                {
                    if (File.Exists(newPath) || Directory.Exists(newPath))
                    {
                        MessageBox.Show("Файл или папка с таким именем уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        textBox.Focus();
                        textBox.SelectAll();
                        isRenamingInProgress = false;
                        return;
                    }
                    if (item is FileObject)
                    {
                        File.Create(newPath).Close();
                    }
                    else if (item is FolderObject)
                    {
                        Directory.CreateDirectory(newPath);
                    }

                    item.Name = newName;
                    item.Path = newPath;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при переименовании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    //CancelRenaming(item);
                    textBox.Focus();
                    textBox.SelectAll();
                    isRenamingInProgress = false;
                    return;
                }
            }
            else
            {
                MessageBox.Show("Имя содержит недопустимые символы", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                //CancelRenaming(item);
                textBox.Focus();
                textBox.SelectAll();
                isRenamingInProgress = false;
                return;
            }

            ResetTextBoxState(textBox);
            if (ActiveListBox != null)
            {
                if (ActiveListBox.CurrentPath != null)
                {
                    ActiveListBox.Handler.HandleFolderSelection(ActiveListBox.CurrentPath);
                }
                else
                {
                    ActiveListBox.Handler.LoadLogicalDrives();
                }
            }
            isRenaming = false;
            isRenamingInProgress = false;
        }
        private static void CancelRenaming(FileSystemObject item)
        {
            var listBoxItem = ActiveListBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
            if (listBoxItem != null)
            {
                var textBox = FindVisualChild<TextBox>(listBoxItem);
                if (textBox != null)
                {
                    textBox.Text = item.Name;
                    ResetTextBoxState(textBox);
                }
            }
            if (item is FileObject && !File.Exists(item.Path))
            {
                File.Create(item.Path).Close();
            }
            else if (item is FolderObject && !Directory.Exists(item.Path))
            {
                Directory.CreateDirectory(item.Path);
            }
            if (ActiveListBox != null)
            {
                if (ActiveListBox.CurrentPath != null)
                {
                    ActiveListBox.Handler.HandleFolderSelection(ActiveListBox.CurrentPath);
                }
                else
                {
                    ActiveListBox.Handler.LoadLogicalDrives();
                }
            }
            //Refresh(ActiveListBox, null);
        }
        private static void ResetTextBoxState(TextBox textBox)
        {
            textBox.IsReadOnly = true;
            textBox.IsHitTestVisible = false;
            textBox.Focusable = false;
            textBox.Background = Brushes.Transparent;
            textBox.Foreground = Brushes.White;
            textBox.BorderBrush = Brushes.Transparent;
            textBox.BorderThickness = new Thickness(0);
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            //IsDragging = false;
            //startPoint = null;
        }

        public static void AddContextMenuItems(ContextMenu contextMenu, List<RegistryEntry> entries, string path)
        {
            foreach (var entry in entries)
            {
                bool itemExists = contextMenu.Items.OfType<MenuItem>().Any(item => item.Header.ToString() == entry.MenuItemText.Replace("&", "_"));
                if (!itemExists)
                {
                    var menuItem = new MenuItem
                    {
                        Header = entry.MenuItemText.Replace("&", "_"),
                        //Icon = entry.Icon,
                        Icon = new Image
                        {
                            Source = IconHelper.GetSmallestExeIcon(entry.IconPath)//; new BitmapImage(new Uri(entry.IconPath))
                        },
                        Command = new RelayCommand(() => ExecuteCommand(entry.Command, path))
                    };
                    contextMenu.Items.Add(menuItem);
                }
            }
        }

        private static void ExecuteCommand(string command, string path)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{command.Replace("%1", path).Replace("%V", path)}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(processStartInfo);
        }

        #region Пункты контекстного меню
        // Пункт "Открыть"
        private void Open(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                ActiveListBox.Handler.HandleSelectionChanged(ActiveListBox, null);
            }
        }

        // Пункт "Обновить"
        private void Refresh(object sender, RoutedEventArgs e)
        {
            ActiveListBox.Refresh();
        }

        #region Обработка пункта "Переименовать"
        // Пункт "Переименовать"
        private async void Rename(object sender, RoutedEventArgs e)
        {
            if (ActiveListBox != null && ActiveListBox.SelectedItem is FileSystemObject selectedItem)
            {
                string path = selectedItem.Path;
                string name = selectedItem.Name;

                if (Path.GetPathRoot(path) == path)
                {
                    MessageBox.Show("Переименование дисков не поддерживается", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var listBoxItem = ActiveListBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                if (listBoxItem == null) return;

                var textBox = FindVisualChild<TextBox>(listBoxItem);
                if (textBox == null) return;

                // Сохраняем оригинальный текст для отмены
                string originalName = textBox.Text;

                // Включаем редактирование
                //textBox.IsEnabled = true;
                textBox.IsReadOnly = false;
                //textBox.IsHitTestVisible = true;
                textBox.Focusable = true;
                textBox.Background = Brushes.White;
                textBox.Foreground = Brushes.Black;
                textBox.BorderBrush = Brushes.Gray;
                textBox.BorderThickness = new Thickness(1);

                // Выделяем текст для редактирования
                if (selectedItem is FileObject)
                {
                    string fileName = Path.GetFileNameWithoutExtension(name);
                    string extension = Path.GetExtension(name);
                    textBox.Select(0, fileName.Length);
                }
                else
                {
                    textBox.SelectAll();
                }

                // Отключаем выделение в ListBox
                //selectedlistBox.IsEnabled = false;

                textBox.Focus();

                textBox.LostFocus += TextBox_LostFocus;
                textBox.KeyDown += TextBox_KeyDown;

                void TextBox_LostFocus(object s, RoutedEventArgs args)
                {
                    FinishRenaming();
                }

                void TextBox_KeyDown(object s, KeyEventArgs args)
                {
                    if (args.Key == Key.Enter)
                    {
                        FinishRenaming();
                        args.Handled = true;
                    }
                    else if (args.Key == Key.Escape)
                    {
                        CancelRenaming();
                        args.Handled = true;
                    }
                }

                void FinishRenaming()
                {
                    string newName = textBox.Text;

                    if (!ContainsInvalidChars(newName))
                    {
                        string newPath = Path.Combine(Path.GetDirectoryName(path), newName);
                        try
                        {
                            if (selectedItem is FileObject)
                            {
                                File.Move(path, newPath);
                            }
                            else if (selectedItem is FolderObject)
                            {
                                Directory.Move(path, newPath);
                            }

                            selectedItem.Name = newName;
                            selectedItem.Path = newPath;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при переименовании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            CancelRenaming();
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Имя содержит недопустимые символы", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        CancelRenaming();
                        return;
                    }

                    ResetTextBoxState();
                }

                void CancelRenaming()
                {
                    textBox.Text = originalName;
                    ResetTextBoxState();
                }

                void ResetTextBoxState()
                {
                    //textBox.IsEnabled = false;
                    textBox.IsReadOnly = true;
                    textBox.IsHitTestVisible = false;
                    textBox.Focusable = false;
                    textBox.Background = Brushes.Transparent;
                    textBox.Foreground = Brushes.White;
                    textBox.BorderBrush = Brushes.Transparent;
                    textBox.BorderThickness = new Thickness(0);

                    textBox.LostFocus -= TextBox_LostFocus;
                    textBox.KeyDown -= TextBox_KeyDown;
                    Refresh(ActiveListBox, e);
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                else
                {
                    T descendant = FindVisualChild<T>(child);
                    if (descendant != null)
                        return descendant;
                }
            }
            return null;
        }

        private static bool ContainsInvalidChars(string name)
        {
            return name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
        }
        #endregion

        #region Обработка пункта "Удалить"
        // Пункт "Удалить"
        private void Delete(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                if (ActiveListBox != null)
                {
                    FileSystemObject? selectedItem = ActiveListBox.SelectedItem as FileSystemObject;
                    if (selectedItem != null)
                    {
                        string path = selectedItem.Path;
                        string name = selectedItem.Name;
                        if (AppSettings.ConfirmDeletion)
                        {
                            if (ShowDeleteConfirmation(name))
                            {
                                _ = DeleteFileOrFolderAsync(path);
                                ActiveListBox._items.Remove(selectedItem);
                            }
                        }
                        else
                        {
                            _ = DeleteFileOrFolderAsync(path);
                            ActiveListBox._items.Remove(selectedItem);
                        }
                    }
                }
            }
        }

        private bool ShowDeleteConfirmation(string name)
        {
            string message = $"Вы уверены, что хотите удалить '{name}'?";
            string caption = "Подтверждение удаления";
            MessageBoxButton buttons = MessageBoxButton.YesNo;
            MessageBoxImage icon = MessageBoxImage.Warning;

            MessageBoxResult result = MessageBox.Show(message, caption, buttons, icon);
            return result == MessageBoxResult.Yes;
        }

        private async Task DeleteFileOrFolderAsync(string path)
        {
            try
            {
                if (path.StartsWith("dropbox://"))
                {
                    // Удаление файла или папки из Dropbox
                    string dropboxPath = path.Substring(10); // Убираем "dropbox://"
                    await _dropboxService._client.Files.DeleteV2Async(dropboxPath);
                }
                else
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Обработка пункта "Архивировать"
        // Пункт "Архивировать"
        private async void Archive_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveListBox.SelectedItem is FileSystemObject selectedItem)
            {
                await CreateArchiveAsync(selectedItem.Path);
                Refresh(null, null);
            }
        }

        private async Task CreateArchiveAsync(string selectedItemPath)
        {
            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            try
            {
                await Task.Run(() =>
                {
                    string archiveName = Path.GetFileNameWithoutExtension(selectedItemPath) + ".zip";
                    string archivePath = Path.Combine(Path.GetDirectoryName(selectedItemPath), archiveName);

                    using (var stream = File.Create(archivePath))
                    using (var writer = WriterFactory.Open(stream, ArchiveType.Zip, new WriterOptions(CompressionType.LZMA)))
                    {
                        if (File.Exists(selectedItemPath))
                        {
                            progressWindow.Dispatcher.Invoke(() => progressWindow.UpdateProgress("Архивирование файла...", 0));
                            writer.Write(Path.GetFileName(selectedItemPath), selectedItemPath);
                            progressWindow.Dispatcher.Invoke(() => progressWindow.UpdateProgress("Архивирование завершено", 100));
                        }
                        else if (Directory.Exists(selectedItemPath))
                        {
                            string folderName = new DirectoryInfo(selectedItemPath).Name;
                            string[] files = Directory.GetFiles(selectedItemPath, "*", SearchOption.AllDirectories);
                            int totalFiles = files.Length;
                            int processedFiles = 0;

                            foreach (string filePath in files)
                            {
                                string entryPath = folderName + filePath.Substring(selectedItemPath.Length);
                                writer.Write(entryPath, filePath);

                                processedFiles++;
                                int percentage = (int)((double)processedFiles / totalFiles * 100);
                                progressWindow.Dispatcher.Invoke(() => progressWindow.UpdateProgress($"Архивирование: {entryPath}", percentage));
                            }
                        }
                    }
                });

                progressWindow.UpdateProgress("Архивирование завершено", 100);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании архива: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressWindow.Close();
            }
        }
        #endregion

        #region Обработка пункта "Свойства"
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
        private const int SW_SHOW = 5;

        private void ShowFileProperties(string filePath)
        {
            SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
            info.cbSize = Marshal.SizeOf(info);
            info.lpVerb = "properties";
            info.lpFile = filePath;
            info.nShow = SW_SHOW;
            info.fMask = SEE_MASK_INVOKEIDLIST;
            if (!ShellExecuteEx(ref info))
            {
                MessageBox.Show("Не удалось открыть окно свойств.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Пункт "Свойства"
        private void Properties(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                if (ActiveListBox != null)
                {
                    FileSystemObject? selectedItem = ActiveListBox.SelectedItem as FileSystemObject;
                    if (selectedItem != null)
                    {
                        ShowFileProperties(selectedItem.Path);
                    }
                }
            }
        }
        #endregion

        #region Смена вида
        // Обработчик кнопки для смены вида
        private void ViewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                switch (menuItem.Name.ToString())
                {
                    case "ViewList":
                        selectedView = "Small";
                        ActiveListBox.View = null;
                        ActiveListBox.ItemTemplate = (DataTemplate)FindResource("ListViewTemplate");
                        ActiveListBox.ItemContainerStyle = null;
                        ActiveListBox._currentScale = 0.7;
                        SetListViewPanel();
                        Refresh(null, null);
                        Dispatcher.BeginInvoke(new Action(ActiveListBox.AttachEventsToItems), DispatcherPriority.Loaded);
                        break;
                    case "ViewTable":
                        //selectedView = "Small";
                        ActiveListBox.SwitchToTableView();
                        ActiveListBox.ItemTemplate = null;
                        ActiveListBox.ItemContainerStyle = (Style)FindResource("GridViewItemStyle");
                        //ActiveListBox._currentScale = 0.7;
                        SetListViewPanel();
                        ActiveListBox.AdjustGridViewColumnWidths();
                        Refresh(null, null);
                        Dispatcher.BeginInvoke(new Action(ActiveListBox.AttachEventsToItems), DispatcherPriority.Loaded);
                        break;
                    case "SmallIcons":
                        selectedView = "Big";
                        ActiveListBox.View = null;
                        ActiveListBox.ItemContainerStyle = null;
                        ActiveListBox.ItemTemplate = null;
                        ActiveListBox._currentScale = 0.8;
                        SetDefaultPanel();
                        //Refresh(null, null);
                        Scale();
                        Dispatcher.BeginInvoke(new Action(ActiveListBox.AttachEventsToItems), DispatcherPriority.Loaded);
                        break;
                    case "NormalIcons":
                        selectedView = "Big";
                        ActiveListBox.View = null;
                        ActiveListBox.ItemContainerStyle = null;
                        ActiveListBox.ItemTemplate = null;
                        //ActiveListBox._currentScale = 1;
                        SetDefaultPanel();
                        Refresh(null, null);
                        //Scale();
                        Dispatcher.BeginInvoke(new Action(ActiveListBox.AttachEventsToItems), DispatcherPriority.Loaded);
                        break;
                    case "BigIcons":
                        selectedView = "Big";
                        ActiveListBox.View = null;
                        ActiveListBox.ItemContainerStyle = null;
                        ActiveListBox.ItemTemplate = null;
                        ActiveListBox._currentScale = 1.6;
                        SetDefaultPanel();
                        //Refresh(null, null);
                        Scale();
                        Dispatcher.BeginInvoke(new Action(ActiveListBox.AttachEventsToItems), DispatcherPriority.Loaded);
                        break;
                    case "HugeIcons":
                        selectedView = "Big";
                        ActiveListBox.View = null;
                        ActiveListBox.ItemContainerStyle = null;
                        ActiveListBox.ItemTemplate = null;
                        ActiveListBox._currentScale = 2.3;
                        SetDefaultPanel();
                        //Refresh(null, null);
                        Scale();
                        Dispatcher.BeginInvoke(new Action(ActiveListBox.AttachEventsToItems), DispatcherPriority.Loaded);
                        break;
                    default:
                        selectedView = "Big";
                        ActiveListBox.View = null;
                        ActiveListBox.ItemContainerStyle = null;
                        ActiveListBox.ItemTemplate = null;
                        ActiveListBox._currentScale = 1;
                        SetDefaultPanel();
                        //Refresh(null, null);
                        Scale();
                        Dispatcher.BeginInvoke(new Action(ActiveListBox.AttachEventsToItems), DispatcherPriority.Loaded);
                        break;
                }
            }
        }

        // Установка панели для табличного вида
        public void SetListViewPanel()
        {
            ItemsPanelTemplate panelTemplate = new ItemsPanelTemplate();
            FrameworkElementFactory stackPanel = new FrameworkElementFactory(typeof(CustomVirtualizingStackPanel));
            stackPanel.SetValue(CustomVirtualizingStackPanel.OrientationProperty, Orientation.Vertical);
            panelTemplate.VisualTree = stackPanel;
            ActiveListBox.ItemsPanel = panelTemplate;
        }

        // Установка панели для значкового вида
        public void SetDefaultPanel()
        {
            ItemsPanelTemplate panelTemplate = new ItemsPanelTemplate();
            FrameworkElementFactory wrapPanel = new FrameworkElementFactory(typeof(VirtualizingWrapPanel));
            wrapPanel.SetValue(VirtualizingWrapPanel.OrientationProperty, Orientation.Horizontal);
            panelTemplate.VisualTree = wrapPanel;
            ActiveListBox.ItemsPanel = panelTemplate;
        }

        // Изменение размеров для значков при разных видах (не работает из-за панели)
        public void Scale()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ActiveListBox.View is not GridView)
                {
                    for (int i = 0; i < ActiveListBox.Items.Count; i++)
                    {
                        var container = ActiveListBox.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                        if (container != null)
                        {

                            container.LayoutTransform = new ScaleTransform(ActiveListBox._currentScale, ActiveListBox._currentScale);
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #endregion

        #endregion

        #endregion

        #region OnChangeds
        public static void UpdateElementCount(CustomListView listBox)
        {
            ElementsChanged?.Invoke(listBox);
        }

        private void OnElementsChanged(CustomListView listBox)
        {
            var tabControl = FindParent<TabControl>(listBox);

            if (tabControl != null)
            {
                if (tabControl.Name == "TabControl1")
                {
                    Elements1.Text = "Элементов: " + listBox.Items.Count;
                }
                else if (tabControl.Name == "TabControl2")
                {
                    Elements2.Text = "Элементов: " + listBox.Items.Count;
                }
            }
        }

        public static void UpdateSelectionCount(CustomListView listBox)
        {
            SelectionChanged?.Invoke(listBox);
        }

        private void OnSelectionChanged(CustomListView listBox)
        {
            var tabControl = FindParent<TabControl>(listBox);
            TextBlock selectedElements = SelectedElements1;
            if (tabControl != null)
            {
                if (tabControl.Name == "TabControl1")
                {
                    selectedElements = SelectedElements1;
                }
                else if (tabControl.Name == "TabControl2")
                {
                    selectedElements = SelectedElements2;
                }
                if (listBox.SelectedItems.Count != 0)
                {
                    selectedElements.Text = $"Выбрано элементов: {listBox.SelectedItems.Count}";
                    selectedElements.Visibility = Visibility.Visible;
                }
                else
                {
                    selectedElements.Visibility = Visibility.Hidden;
                }
            }
        }

        public static void UpdateCurrentPath(CustomListView listBox)
        {
            CurrentPathChanged?.Invoke(listBox);
        }
        ItemsControl targetPathPanel;
        private void OnCurrentPathChanged(CustomListView listBox)
        {
            var tabControl = FindParent<TabControl>(listBox);
            TabItem? tabItem = (TabItem?)listBox.Parent;
            if (tabItem != null)
            {
                if (tabItem.Header == "+")
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tabItem.Style = (Style)FindResource("ClosableTabItemStyle");
                        tabItem.MaxWidth = 300;
                        tabItem.Width = double.NaN;
                    });
                    AddNewTab(new CustomListView(), "+", tabControl, false, false);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        tabItem.Style = (Style)FindResource("ClosableTabItemStyle");
                    });
                }
                string GetDisplayName(string path)
                {
                    if (string.IsNullOrEmpty(path))
                        return "Этот компьютер";

                    string fileName = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        return "Локальный диск (" + Path.GetPathRoot(path).TrimEnd('\\') + ")"; // DropBox учитывать надо
                    }
                    return fileName;
                }
                string newHeader = GetDisplayName(listBox.CurrentPath);
                if (tabItem.Header.ToString() != newHeader)
                {
                    tabItem.Header = newHeader;

                    if (tabControl is CustomTabControl customTabControl)
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            tabItem.Tag = new TabItemData { CurrentPath = listBox.CurrentPath, DesiredWidth = tabItem.DesiredSize.Width };
                            customTabControl.UpdateTabWidths();
                        });
                    }
                }
            }
            targetPathPanel = (tabControl.Name == "TabControl1") ? PathPanel1 : PathPanel2;

            if (listBox.CurrentPath != null)
            {
                string[] pathParts = listBox.CurrentPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                string currentPath = "";
                var pathElements = new List<PathElement>();

                for (int i = 0; i < pathParts.Length; i++)
                {
                    string part = pathParts[i];
                    currentPath += part + "\\";
                    pathElements.Add(new PathElement
                    {
                        Name = part,
                        FullPath = currentPath,
                        IsLast = i == pathParts.Length - 1
                    });
                }

                targetPathPanel.ItemsSource = pathElements;
                targetPathPanel.Tag = listBox.CurrentPath;
                if (targetPathPanel.Items.Count > 0)
                {
                    if (targetPathPanel.Name == "PathPanel1")
                    {
                        PathScrollViewer1.ScrollToRightEnd();
                        CurrentPathBlock.Text = listBox.CurrentPath;
                    }
                    else
                    {
                        PathScrollViewer2.ScrollToRightEnd();
                        CurrentPathBlock2.Text = listBox.CurrentPath;
                    }
                }
            }
            else
            {
                targetPathPanel.ItemsSource = null;
            }
        }
        private void ScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            if (e.OriginalSource is Button || e.Source is Button) return;

            Point clickPoint = e.GetPosition(scrollViewer);

            Button clickedButton = FindVisualChildBtn<Button>(scrollViewer, child =>
            {
                Point relativePoint = child.TranslatePoint(new Point(0, 0), scrollViewer);
                return child.IsVisible
                    && clickPoint.X >= relativePoint.X
                    && clickPoint.X <= relativePoint.X + child.ActualWidth
                    && clickPoint.Y >= relativePoint.Y
                    && clickPoint.Y <= relativePoint.Y + child.ActualHeight;
            });

            if (clickedButton != null)
            {
                return;
            }

            if (scrollViewer.Name == "PathScrollViewer1")
            {
                targetPathPanel = PathPanel1;
                PathTextBox1.Text = (string)targetPathPanel.Tag;
                PathTextBox1.Visibility = Visibility.Visible;
                PathTextBox1.Focus();
            }
            else
            {
                targetPathPanel = PathPanel2;
                PathTextBox2.Text = (string)targetPathPanel.Tag;
                PathTextBox2.Visibility = Visibility.Visible;
                PathTextBox2.Focus();
            }
            scrollViewer.Visibility = Visibility.Collapsed;

            e.Handled = true;
        }
        private T FindVisualChildBtn<T>(DependencyObject parent, Func<T, bool> condition) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && condition(typedChild))
                    return typedChild;

                var result = FindVisualChildBtn<T>(child, condition);
                if (result != null)
                    return result;
            }
            return null;
        }
        private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string newPath = (targetPathPanel.Name == "PathPanel1") ? PathTextBox1.Text : PathTextBox2.Text;

                if (Directory.Exists(newPath))
                {
                    ActiveListBox.CurrentPath = newPath;
                    ActiveListBox.NavManager.NavigateTo(newPath, ActiveListBox);
                    _ = ActiveListBox.Handler.HandleFolderSelection(newPath);
                    ActiveListBox.Focus();
                }
                else if (File.Exists(newPath))
                {
                    ActiveListBox.Handler.OpenFileWithDefaultProgram(newPath);
                }
                else
                {
                    MessageBox.Show("Указанный путь не существует.");
                }

                HideTextBoxShowScrollViewer();
            }
            else if (e.Key == Key.Escape)
            {
                HideTextBoxShowScrollViewer();
            }
        }

        private void PathTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            HideTextBoxShowScrollViewer();
        }

        private void HideTextBoxShowScrollViewer()
        {
            if (targetPathPanel.Name == "PathPanel1")
            {
                PathTextBox1.Visibility = Visibility.Collapsed;
                PathScrollViewer1.Visibility = Visibility.Visible;
            }
            else
            {
                PathTextBox2.Visibility = Visibility.Collapsed;
                PathScrollViewer2.Visibility = Visibility.Visible;
            }
        }
        private void PathElement_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            string targetPath = clickedButton.Tag as string;
            ActiveListBox.NavManager.NavigateTo(targetPath, ActiveListBox);
            if (targetPath != null)
            {
                ActiveListBox.Handler.HandleFolderSelection(targetPath);
            }
            else
            {
                ActiveListBox.Handler.LoadLogicalDrives();
            }
        }
        #endregion

        #region Обработка поиска
        private CancellationTokenSource _searchCancellationTokenSource;

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateSearchResultsTab(Search1.Text, ActiveListBox.CurrentPath);
            }
        }

        private void Search2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateSearchResultsTab(Search2.Text, ActiveListBox.CurrentPath);
            }
        }

        public void CreateSearchResultsTab(string searchQuery, string currentPath)
        {
            CustomListView resultsListBox = new CustomListView(true);
            resultsListBox.CurrentPath = currentPath;
            selectedView = "Small";
            resultsListBox.View = null;
            resultsListBox.ItemTemplate = (DataTemplate)FindResource("ListViewTemplate");
            resultsListBox.ItemContainerStyle = null;
            resultsListBox._currentScale = 0.7;
            ItemsPanelTemplate panelTemplate = new ItemsPanelTemplate();
            FrameworkElementFactory stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            panelTemplate.VisualTree = stackPanel;
            resultsListBox.ItemsPanel = panelTemplate;

            // Получение TabControl
            TabControl tabControl = FindParent<TabControl>(ActiveListBox);

            // Добавление новой вкладки с результатами
            AddNewTab(resultsListBox, $"Результаты поиска: {searchQuery}", tabControl);
            _searchCancellationTokenSource = new CancellationTokenSource();
            // Выполнение поиска и заполнение ListBox
            PerformSearch(searchQuery, currentPath, resultsListBox, _searchCancellationTokenSource.Token);
        }

        private List<string> GetAllDrives()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .ToList();
        }

        public async Task PerformSearch(string searchQuery, string currentPath, CustomListView resultListBox, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(currentPath))
            {
                var drives = GetAllDrives();
                foreach (var drive in drives)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;
                    await SearchInFoldersAndFiles(drive, searchQuery, resultListBox, cancellationToken);
                }
            }
            else
            {
                await SearchInFoldersAndFiles(currentPath, searchQuery, resultListBox, cancellationToken);
            }

            resultListBox.SetChanges();
        }

        private async Task SearchInFoldersAndFiles(string rootPath, string searchQuery, CustomListView resultListBox, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    SearchFilesAndFolders(rootPath, searchQuery, resultListBox, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    // Игнорируем недоступные папки
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during search: {ex.Message}");
                }
            }, cancellationToken);
        }

        private void SearchFilesAndFolders(string rootPath, string searchQuery, CustomListView resultListBox, CancellationToken cancellationToken)
        {
            if (!FileFolderHandler.HasAccessToFolder(rootPath) || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var files = Directory.EnumerateFiles(rootPath)
                    .Where(file => Path.GetFileName(file).ToLower().Contains(searchQuery.ToLower()));

                foreach (var file in files)
                {
                    var fileObject = new FileObject
                    {
                        Name = Path.GetFileName(file),
                        Path = file,
                    };
                    resultListBox.Dispatcher.Invoke(() =>
                    {
                        fileObject.Icon = IconHelper.GetIcon(file);
                        resultListBox._items.Add(fileObject);
                    });
                }

                var folders = Directory.EnumerateDirectories(rootPath)
                    .Where(dir => Path.GetFileName(dir).ToLower().Contains(searchQuery.ToLower()));

                foreach (var folder in folders)
                {
                    resultListBox.Dispatcher.Invoke(() =>
                    {
                        resultListBox._items.Add(new FolderObject
                        {
                            Name = Path.GetFileName(folder),
                            Path = folder,
                            Icon = standardFolderIcon
                        });
                    });
                }

                // Рекурсивный вызов для подпапок
                foreach (var subdir in Directory.EnumerateDirectories(rootPath))
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;
                    SearchFilesAndFolders(subdir, searchQuery, resultListBox, cancellationToken);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Игнорируем недоступные папки
            }
        }

        private void OnSearchTabClosed(object sender, EventArgs e)
        {
            _searchCancellationTokenSource?.Cancel();
        }
        #endregion

        #region Консоль

        #region Показ консоли
        private bool _isConsoleVisible = false;
        private double consoleHeight = 200;
        private void ShowConsole_Click(object sender, RoutedEventArgs e)
        {
            Storyboard storyboard;
            if (_isConsoleVisible)
            {
                storyboard = (Storyboard)FindResource("HideConsole");
                ConsoleRow.Height = new GridLength(0);
                ConsoleRow.MinHeight = 0;
                SplitterRow.Height = new GridLength(0);
                TabRow.Height = new GridLength(1, GridUnitType.Star);
                ConsoleBorder.Visibility = Visibility.Collapsed;
                OpenConsoleBtn.Content = "^";
            }
            else
            {
                storyboard = (Storyboard)FindResource("ShowConsole");
                ConsoleRow.Height = new GridLength(200);
                ConsoleRow.MinHeight = 100;
                //ConsoleRow.MaxHeight = 200;
                //TabRow.Height = new GridLength(TabRow.ActualHeight - 200);
                double calculatedHeight = TabRow.ActualHeight - consoleHeight;
                TabRow.Height = calculatedHeight > 0 ? new GridLength(calculatedHeight) : new GridLength(0);
                ConsoleRow.Height = new GridLength(1, GridUnitType.Star);
                //ConsoleRow.MaxHeight = double.PositiveInfinity;
                ConsoleBorder.Visibility = Visibility.Visible;
                OpenConsoleBtn.Content = "v";
            }

            if (storyboard != null)
            {
                storyboard.Begin(this);
            }
            else
            {
                Console.WriteLine("Storyboard not found!");
            }

            _isConsoleVisible = !_isConsoleVisible;
        }

        private void ShowConsole_Completed(object sender, EventArgs e)
        {
            SplitterRow.Height = new GridLength(2);
        }

        private void ConsoleSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            consoleHeight = ConsoleRow.ActualHeight;
        }

        private void ConsoleOutput_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                TextBox textBox = (TextBox)sender;
                e.Handled = true;

                double oldZoom = currentZoom;
                if (e.Delta > 0)
                    currentZoom = Math.Min(currentZoom + zoomFactor, maxZoom);
                else
                    currentZoom = Math.Max(currentZoom - zoomFactor, minZoom);

                textBox.FontSize = 12 * currentZoom;

                double zoomFactor2 = currentZoom / oldZoom;
                textBox.ScrollToVerticalOffset(textBox.VerticalOffset * zoomFactor2);
                textBox.ScrollToHorizontalOffset(textBox.HorizontalOffset * zoomFactor2);
            }
        }

        #endregion

        private async void ConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string command = ConsoleInput.Text.Trim();
                ConsoleOutput.AppendText($"{CurrentPathBlock.Text}>{command}\n");
                ConsoleInput.Clear();

                await ExecuteCommandAsync(command);
            }
        }

        private async Task ExecuteCommandAsync(string command)
        {
            try
            {
                if (command.TrimStart().StartsWith("cd", StringComparison.OrdinalIgnoreCase))
                {
                    HandleCdCommand(command);
                    return;
                }
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = CurrentPathBlock.Text.Trim('>'),
                        StandardOutputEncoding = Encoding.GetEncoding(866),
                        StandardErrorEncoding = Encoding.GetEncoding(866)
                    }
                };

                //process.OutputDataReceived += (sender, e) =>
                //{
                //    if (!string.IsNullOrEmpty(e.Data))
                //    {
                //        Dispatcher.Invoke(() => ConsoleOutput.AppendText(e.Data + "\n"));
                //        Dispatcher.Invoke(() => ConsoleOutput.ScrollToEnd());
                //    }
                //};

                //string lastLine = "";
                //process.OutputDataReceived += (sender, e) =>
                //{
                //    if (!string.IsNullOrEmpty(e.Data))
                //    {
                //        if (e.Data.Contains("%"))
                //        {
                //            Dispatcher.Invoke(() =>
                //            {
                //                // Удаляем последнюю строку, если она содержала проценты
                //                if (lastLine.Contains("%"))
                //                {
                //                    ConsoleOutput.Text = ConsoleOutput.Text.Remove(ConsoleOutput.Text.LastIndexOf(lastLine));
                //                }
                //                ConsoleOutput.AppendText(e.Data + "\n");
                //                ConsoleOutput.ScrollToEnd();
                //            });
                //            lastLine = e.Data;
                //        }
                //        else
                //        {
                //            Dispatcher.Invoke(() =>
                //            {
                //                ConsoleOutput.AppendText(e.Data + "\n");
                //                ConsoleOutput.ScrollToEnd();
                //            });
                //            lastLine = e.Data;
                //        }
                //    }
                //};

                string lastLine = "";
                bool lastLineHadPercentage = false;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string currentLine = e.Data.TrimEnd();
                        bool currentLineIsEmpty = string.IsNullOrWhiteSpace(currentLine);

                        if (!string.IsNullOrWhiteSpace(currentLine) || !string.IsNullOrWhiteSpace(lastLine))
                        {
                            bool currentLineHasPercentage = currentLine.Contains("%");
                            Dispatcher.Invoke(() =>
                            {
                                if (currentLineHasPercentage)
                                {
                                    // Удаляем последнюю строку, если она содержала проценты
                                    if (lastLineHadPercentage)
                                    {
                                        ConsoleOutput.Text = ConsoleOutput.Text.Remove(ConsoleOutput.Text.LastIndexOf(lastLine));
                                    }
                                    ConsoleOutput.AppendText(currentLine + "\n");
                                }
                                else
                                {
                                    // Если текущая строка без процентов, удаляем предыдущую строку с процентами
                                    if (lastLineHadPercentage)
                                    {
                                        ConsoleOutput.Text = ConsoleOutput.Text.Remove(ConsoleOutput.Text.LastIndexOf(lastLine));
                                    }
                                    ConsoleOutput.AppendText(currentLine + "\n");
                                }
                                ConsoleOutput.ScrollToEnd();
                            });

                            lastLine = currentLine;
                            lastLineHadPercentage = currentLineHasPercentage;
                        }
                    }
                };


                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => ConsoleOutput.AppendText("Error: " + e.Data + "\n"));
                        Dispatcher.Invoke(() => ConsoleOutput.ScrollToEnd());
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    ConsoleOutput.AppendText("Команда не выполнена. Возможно, требуются права администратора.\n");
                }
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception win32Ex && win32Ex.NativeErrorCode == 5) // Отказано в доступе
                {
                    ConsoleOutput.AppendText("Ошибка выполнения команды: Отказано в доступе. Требуются права администратора.\n");
                }
                else
                {
                    ConsoleOutput.AppendText($"Ошибка выполнения команды: {ex.Message}\n");
                }
            }

            ConsoleOutput.AppendText("\n");
            ConsoleOutput.ScrollToEnd();
        }

        private async void HandleCdCommand(string command)
        {
            string[] parts = command.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                ConsoleOutput.AppendText("Invalid cd command. Usage: cd <path>\n");
                return;
            }

            string newPath = parts[1].Trim();
            string currentPath = CurrentPathBlock.Text.Trim('>');

            if (newPath == "..")
            {
                DirectoryInfo? parentDir = Directory.GetParent(currentPath);
                if (parentDir != null)
                {
                    newPath = parentDir.FullName;
                }
                else
                {
                    ActiveListBox.Handler.LoadLogicalDrives();
                    ActiveListBox.CurrentPath = null;
                    CurrentPathBlock.Text = ">";
                    //ConsoleOutput.AppendText("Already at the root directory.\n");
                    return;
                }
            }
            else if (!Path.IsPathRooted(newPath))
            {
                newPath = Path.GetFullPath(Path.Combine(currentPath, newPath));
            }

            if (Directory.Exists(newPath))
            {
                CurrentPathBlock.Text = newPath + ">";
                ActiveListBox.NavManager.NavigateTo(newPath, ActiveListBox);
                await ActiveListBox.Handler.HandleFolderSelection(newPath);
                ConsoleOutput.AppendText($"Changed directory to: {newPath}\n");
            }
            else
            {
                ConsoleOutput.AppendText($"Directory not found: {newPath}\n");
            }
        }
        #endregion
        #region Консоль 2

        private bool _isConsoleVisible2 = false;
        private double consoleHeight2 = 200;

        private void ShowConsole2_Click(object sender, RoutedEventArgs e)
        {
            Storyboard storyboard;
            if (_isConsoleVisible2)
            {
                storyboard = (Storyboard)FindResource("HideConsole2");
                ConsoleRow2.Height = new GridLength(0);
                ConsoleRow2.MinHeight = 0;
                SplitterRow2.Height = new GridLength(0);
                TabRow2.Height = new GridLength(1, GridUnitType.Star);
                ConsoleBorder2.Visibility = Visibility.Collapsed;
                OpenConsoleBtn2.Content = "^";
            }
            else
            {
                storyboard = (Storyboard)FindResource("ShowConsole2");
                ConsoleRow2.Height = new GridLength(200);
                ConsoleRow2.MinHeight = 100;
                double calculatedHeight = TabRow2.ActualHeight - consoleHeight2;
                TabRow2.Height = calculatedHeight > 0 ? new GridLength(calculatedHeight) : new GridLength(0);
                ConsoleRow2.Height = new GridLength(1, GridUnitType.Star);
                ConsoleBorder2.Visibility = Visibility.Visible;
                OpenConsoleBtn2.Content = "v";
            }

            if (storyboard != null)
            {
                storyboard.Begin(this);
            }
            else
            {
                Console.WriteLine("Storyboard not found!");
            }

            _isConsoleVisible2 = !_isConsoleVisible2;
        }

        private void ShowConsole2_Completed(object sender, EventArgs e)
        {
            SplitterRow2.Height = new GridLength(2);
        }

        private void ConsoleSplitter2_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            consoleHeight2 = ConsoleRow2.ActualHeight;
        }

        private async void ConsoleInput2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string command = ConsoleInput2.Text.Trim();
                ConsoleOutput2.AppendText($"{CurrentPathBlock2.Text}>{command}\n");
                ConsoleInput2.Clear();

                await ExecuteCommandAsync2(command);
            }
        }

        private async Task ExecuteCommandAsync2(string command)
        {
            try
            {
                if (command.TrimStart().StartsWith("cd", StringComparison.OrdinalIgnoreCase))
                {
                    HandleCdCommand2(command);
                    return;
                }
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = CurrentPathBlock.Text.Trim('>'),
                        StandardOutputEncoding = Encoding.GetEncoding(866),
                        StandardErrorEncoding = Encoding.GetEncoding(866)
                    }
                };

                string lastLine = "";
                bool lastLineHadPercentage = false;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string currentLine = e.Data.TrimEnd();
                        bool currentLineIsEmpty = string.IsNullOrWhiteSpace(currentLine);

                        if (!string.IsNullOrWhiteSpace(currentLine) || !string.IsNullOrWhiteSpace(lastLine))
                        {
                            bool currentLineHasPercentage = currentLine.Contains("%");
                            Dispatcher.Invoke(() =>
                            {
                                if (currentLineHasPercentage)
                                {
                                    if (lastLineHadPercentage)
                                    {
                                        ConsoleOutput2.Text = ConsoleOutput2.Text.Remove(ConsoleOutput2.Text.LastIndexOf(lastLine));
                                    }
                                    ConsoleOutput2.AppendText(currentLine + "\n");
                                }
                                else
                                {
                                    if (lastLineHadPercentage)
                                    {
                                        ConsoleOutput2.Text = ConsoleOutput2.Text.Remove(ConsoleOutput2.Text.LastIndexOf(lastLine));
                                    }
                                    ConsoleOutput2.AppendText(currentLine + "\n");
                                }
                                ConsoleOutput2.ScrollToEnd();
                            });

                            lastLine = currentLine;
                            lastLineHadPercentage = currentLineHasPercentage;
                        }
                    }
                };


                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => ConsoleOutput2.AppendText("Error: " + e.Data + "\n"));
                        Dispatcher.Invoke(() => ConsoleOutput2.ScrollToEnd());
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    ConsoleOutput2.AppendText("Команда не выполнена. Возможно, требуются права администратора.\n");
                }
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception win32Ex && win32Ex.NativeErrorCode == 5)
                {
                    ConsoleOutput2.AppendText("Ошибка выполнения команды: Отказано в доступе. Требуются права администратора.\n");
                }
                else
                {
                    ConsoleOutput2.AppendText($"Ошибка выполнения команды: {ex.Message}\n");
                }
            }

            ConsoleOutput2.AppendText("\n");
            ConsoleOutput2.ScrollToEnd();
        }

        private async void HandleCdCommand2(string command)
        {
            string[] parts = command.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                ConsoleOutput2.AppendText("Invalid cd command. Usage: cd <path>\n");
                return;
            }

            string newPath = parts[1].Trim();
            string currentPath = CurrentPathBlock2.Text.Trim('>');

            if (newPath == "..")
            {
                DirectoryInfo? parentDir = Directory.GetParent(currentPath);
                if (parentDir != null)
                {
                    newPath = parentDir.FullName;
                }
                else
                {
                    ActiveListBox.Handler.LoadLogicalDrives();
                    ActiveListBox.CurrentPath = null;
                    CurrentPathBlock2.Text = ">";
                    return;
                }
            }
            else if (!Path.IsPathRooted(newPath))
            {
                newPath = Path.GetFullPath(Path.Combine(currentPath, newPath));
            }

            if (Directory.Exists(newPath))
            {
                CurrentPathBlock2.Text = newPath + ">";
                ActiveListBox.NavManager.NavigateTo(newPath, ActiveListBox);
                await ActiveListBox.Handler.HandleFolderSelection(newPath);
                ConsoleOutput2.AppendText($"Changed directory to: {newPath}\n");
            }
            else
            {
                ConsoleOutput2.AppendText($"Directory not found: {newPath}\n");
            }
        }
        #endregion

        // Кнопка для разворачивания/сворачивания группы, подгруппы и избранных
        private void ExpanderButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var expander = FindParent<Expander>(button);
            if (expander != null)
            {
                expander.IsExpanded = !expander.IsExpanded;

                //var listView = FindVisualChild<ListView>(expander);
                //if (listView != null && listView.Items.Count > 0)
                //{
                //    expander.IsExpanded = !expander.IsExpanded;
                //}
            }
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            var expandedExpander = (Expander)sender;

            //var listView = FindVisualChild<ListView>(expandedExpander);
            //if (listView == null || listView.Items.Count == 0)
            //{
            //    // Если элементов нет, отменяем раскрытие
            //    expandedExpander.IsExpanded = false;
            //    return;
            //}

            // Проверяем, является ли родительский элемент ItemsControl
            var parentItemsControl = FindParent<ItemsControl>(expandedExpander);

            if (parentItemsControl != null)
            {
                // Если это вложенный Expander в ItemsControl
                foreach (GroupListItem item in parentItemsControl.Items)
                {
                    var itemContainer = parentItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (itemContainer != null)
                    {
                        var innerExpander = FindVisualChild<Expander>(itemContainer);
                        if (innerExpander != null && innerExpander != expandedExpander)
                        {
                            innerExpander.IsExpanded = false;
                        }
                    }
                }
            }
            else
            {
                // Если это корневой Expander
                foreach (var child in ExpanderContainer.Children)
                {
                    if (child is Expander expander && expander != expandedExpander)
                    {
                        expander.IsExpanded = false;
                    }
                }
            }
        }

        #region Обработка избранных и групп
        public ObservableCollection<FileSystemObject> favoritesCollection;

        private void LoadFavorites()
        {
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string favoritesFile = Path.Combine(projectPath, "Favorites.txt");
            var icon = standardFolderIcon;

            favoritesCollection = new ObservableCollection<FileSystemObject>();

            if (File.Exists(favoritesFile))
            {
                string[] lines = File.ReadAllLines(favoritesFile);
                bool isFavoritesSection = false;
                List<string> updatedLines = new List<string>();

                foreach (string line in lines)
                {
                    if (line == "##Favorites")
                    {
                        isFavoritesSection = true;
                        updatedLines.Add(line);
                        continue;
                    }
                    else if (line.StartsWith("#") && isFavoritesSection)
                    {
                        isFavoritesSection = false;
                    }

                    if (isFavoritesSection && !string.IsNullOrWhiteSpace(line))
                    {
                        string path = line.Trim();
                        if (Directory.Exists(path) || File.Exists(path))
                        {
                            string name = Path.GetFileName(path);
                            favoritesCollection.Add(new FileSystemObject { Path = path, Name = name, Icon = icon });
                            updatedLines.Add(line);
                        }
                    }
                    else
                    {
                        updatedLines.Add(line);
                    }
                }

                File.WriteAllLines(favoritesFile, updatedLines);
            }

            FavoritesListView.ItemsSource = favoritesCollection;
        }

        private void AddToFavorite(FileSystemObject item)
        {
            if (!favoritesCollection.Any(f => f.Path == item.Path))
            {
                var newFavorite = new FileSystemObject
                {
                    Path = item.Path,
                    Name = item.Name,
                    Icon = item.Icon,
                };
                favoritesCollection.Add(newFavorite);

                string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                string favoritesFile = Path.Combine(projectPath, "Favorites.txt");
                List<string> lines = new List<string>();

                if (File.Exists(favoritesFile))
                {
                    lines = File.ReadAllLines(favoritesFile).ToList();
                }

                if (!lines.Contains("##Favorites"))
                {
                    lines.Insert(0, "##Favorites");
                }

                int favoritesEndIndex = lines.FindIndex(line => line.StartsWith("#") && line != "##Favorites");

                if (favoritesEndIndex == -1)
                {
                    lines.Add(item.Path);
                }
                else
                {
                    lines.Insert(favoritesEndIndex, item.Path);
                }

                File.WriteAllLines(favoritesFile, lines);
            }
        }

        private void RemoveFromFavorite(FileSystemObject item)
        {
            var favoriteToRemove = favoritesCollection.FirstOrDefault(f => f.Path == item.Path);
            if (favoriteToRemove != null)
            {
                favoritesCollection.Remove(favoriteToRemove);

                string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                string favoritesFile = Path.Combine(projectPath, "Favorites.txt");

                if (File.Exists(favoritesFile))
                {
                    var lines = File.ReadAllLines(favoritesFile).ToList();
                    bool isFavoritesSection = false;
                    List<string> updatedLines = new List<string>();

                    foreach (var line in lines)
                    {
                        if (line == "##Favorites")
                        {
                            isFavoritesSection = true;
                            updatedLines.Add(line);
                        }
                        else if (line.StartsWith("#") && isFavoritesSection)
                        {
                            isFavoritesSection = false;
                            updatedLines.Add(line);
                        }
                        else if (isFavoritesSection && line.Trim() != item.Path)
                        {
                            updatedLines.Add(line);
                        }
                        else if (!isFavoritesSection)
                        {
                            updatedLines.Add(line);
                        }
                    }

                    File.WriteAllLines(favoritesFile, updatedLines);
                }
            }
        }

        public void ToggleFavorite(object sender, RoutedEventArgs e)
        {
            if (sender != null && ActiveListBox != null)
            {
                FileSystemObject? selectedItem = ActiveListBox.SelectedItem as FileSystemObject;
                if (selectedItem != null)
                {
                    if (selectedItem.IsFavorite)
                    {
                        RemoveFromFavorite(selectedItem);
                    }
                    else
                    {
                        AddToFavorite(selectedItem);
                    }
                    selectedItem.IsFavorite = !selectedItem.IsFavorite;

                    var menuItem = sender as MenuItem;
                    if (menuItem != null)
                    {
                        menuItem.Header = selectedItem.IsFavorite ? "Удалить из избранного" : "Добавить в избранное";
                    }
                }
            }
        }

        public ObservableCollection<GroupListItem> groupsCollection;

        private void LoadGroups()
        {
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string groupsFile = Path.Combine(projectPath, "Favorites.txt");
            string groupImagesFolder = Path.Combine(projectPath, "GroupImages");
            var icon = standardFolderIcon;

            groupsCollection = new ObservableCollection<GroupListItem>();

            if (File.Exists(groupsFile))
            {
                string[] lines = File.ReadAllLines(groupsFile);
                GroupListItem currentGroup = null;

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    if (line.StartsWith("#") && !line.StartsWith("##"))
                    {
                        string[] parts = line.Substring(1).Split('#');
                        string groupName = parts[0].Trim();
                        ImageSource groupImage = null;

                        if (parts.Length > 1)
                        {
                            string imageFileName = parts[1].Trim();
                            string imagePath = Path.Combine(groupImagesFolder, imageFileName);
                            if (File.Exists(imagePath))
                            {
                                groupImage = new BitmapImage(new Uri(imagePath));
                            }
                        }

                        if (groupImage == null)
                        {
                            groupImage = new BitmapImage(new Uri("pack://application:,,,/Styleplorer;component/Resources/GroupFolderIcon.png"));
                        }

                        currentGroup = new GroupListItem { Name = groupName, Image = groupImage, Items = new ObservableCollection<FileSystemObject>() };
                        groupsCollection.Add(currentGroup);
                    }
                    else if (currentGroup != null && !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    {
                        string path = line.Trim();
                        if (Directory.Exists(path) || File.Exists(path))
                        {
                            string name = Path.GetFileName(path);
                            currentGroup.Items.Add(new FileSystemObject { Path = path, Name = name, Icon = icon });
                        }
                    }
                }
            }

            GroupsListView.ItemsSource = groupsCollection;
            CleanupUnusedGroupImages();
        }

        private void CleanupUnusedGroupImages()
        {
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string favoritesFile = Path.Combine(projectPath, "Favorites.txt");
            string groupImagesFolder = Path.Combine(projectPath, "GroupImages");

            // Получаем все имена файлов изображений из Favorites.txt
            HashSet<string> usedImages = new HashSet<string>();
            string[] lines = File.ReadAllLines(favoritesFile);
            foreach (string line in lines)
            {
                if (line.StartsWith("#") && line.Contains("#"))
                {
                    string[] parts = line.Split('#');
                    if (parts.Length > 2)
                    {
                        usedImages.Add(parts[2].Trim());
                    }
                }
            }

            // Проверяем каждый файл в папке GroupImages
            foreach (string file in Directory.GetFiles(groupImagesFolder))
            {
                string fileName = Path.GetFileName(file);
                if (!usedImages.Contains(fileName))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        private void FavoritesListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var listView = (ListView)sender;
            if (listView != null)
            {
                if (listView.SelectedItem != null)
                {
                    var listViewItem = listView.SelectedItem as FileSystemObject;
                    if (listViewItem != null)
                    {
                        ActiveListBox.Handler.HandleFolderSelection(listViewItem.Path);
                        ActiveListBox.NavManager.NavigateTo(listViewItem.Path, ActiveListBox);
                    }
                }
            }
        }

        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            AddGroupPanel.Visibility = Visibility.Visible;
            GroupScrollViewer.ScrollToEnd();
        }

        private void GroupNameTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[a-zA-Z0-9\s\p{IsCyrillic}]+$");
        }

        bool isNewImage = false;
        private void GroupImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Файлы изображений (*.png;*.jpeg;*.jpg;*.bmp;*.ico;*.exe)|*.png;*.jpeg;*.jpg;*.bmp;*.ico;*.exe",
                Title = "Выберите изображение или исполняемый файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                isNewImage = true;
                string filePath = openFileDialog.FileName;
                BitmapSource iconSource;

                if (Path.GetExtension(filePath).ToLower() == ".exe")
                {
                    iconSource = IconHelper.GetFileIcon(filePath);
                    IconHelper.ResizeImage(iconSource, 20, 20);
                }
                else
                {
                    iconSource = new BitmapImage(new Uri(filePath));
                    IconHelper.ResizeImage(iconSource, 20, 20);
                }

                GroupImage.Source = iconSource;
            }
            else
            {
                isNewImage = false;
            }
        }

        private void AddGroup(string groupName, ImageSource groupImage)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string favoritesFile = Path.Combine(projectPath, "Favorites.txt");
            string groupImagesFolder = Path.Combine(projectPath, "GroupImages");

            if (!Directory.Exists(groupImagesFolder))
            {
                Directory.CreateDirectory(groupImagesFolder);
            }

            string imageFileName = null;
            if (isNewImage && groupImage != null && !(groupImage is BitmapImage defaultImage && defaultImage.UriSource.ToString().Contains("GroupFolderIcon.png")))
            {
                imageFileName = $"{Guid.NewGuid()}.png";
                string imagePath = Path.Combine(groupImagesFolder, imageFileName);

                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)groupImage));
                using (var fileStream = new FileStream(imagePath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }

            GroupListItem newGroup = new GroupListItem { Name = groupName, Image = groupImage, Items = new ObservableCollection<FileSystemObject>() };
            groupsCollection.Add(newGroup);

            List<string> lines = File.ReadAllLines(favoritesFile).ToList();

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            lines.Add(imageFileName != null ? $"#{groupName}#{imageFileName}" : $"#{groupName}");

            File.WriteAllLines(favoritesFile, lines);
        }

        private void AddItemToGroup(string groupName, string itemPath)
        {
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string favoritesFile = Path.Combine(projectPath, "Favorites.txt");

            List<string> lines = File.ReadAllLines(favoritesFile).ToList();

            // Удаляем запись о прошлой группе
            lines.RemoveAll(line => line.Trim() == itemPath);

            int groupIndex = lines.FindIndex(line => line.StartsWith($"#{groupName}"));
            if (groupIndex != -1)
            {
                // Находим конец группы
                int nextGroupIndex = lines.FindIndex(groupIndex + 1, line => line.StartsWith("#"));
                if (nextGroupIndex == -1)
                {
                    nextGroupIndex = lines.Count;
                }

                // Вставляем новый элемент перед следующей группой или в конец файла
                lines.Insert(nextGroupIndex, itemPath);

                // Удаляем пустые строки в конце файла
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                {
                    lines.RemoveAt(lines.Count - 1);
                }

                File.WriteAllLines(favoritesFile, lines);

                // Обновляем коллекцию в памяти
                var group = groupsCollection.FirstOrDefault(g => g.Name == groupName);
                if (group != null)
                {
                    string name = Path.GetFileName(itemPath);
                    var icon = folderInsideFolderIcon;
                    var existingItem = group.Items.FirstOrDefault(i => i.Path == itemPath);
                    if (existingItem == null)
                    {
                        group.Items.Add(new FileSystemObject { Path = itemPath, Name = name, Icon = icon });
                    }
                }
            }
        }

        private void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            string groupName = GroupNameTextBox.Text.Trim();

            if (currentEditingGroup != null)
            {
                // Режим редактирования
                if (groupName != currentEditingGroup.Name &&
                    groupsCollection.Any(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Группа с таким названием уже существует. Пожалуйста, выберите другое название.",
                                    "Повторяющееся название группы", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                UpdateGroup(currentEditingGroup, groupName, GroupImage.Source);
                currentEditingGroup = null;
            }
            else
            {
                // Режим создания
                if (groupsCollection.Any(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Группа с таким названием уже существует. Пожалуйста, выберите другое название.",
                                    "Повторяющееся название группы", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AddGroup(groupName, GroupImage.Source);
            }

            GroupNameTextBox.Text = "NewGroup";
            GroupImage.Source = new BitmapImage(new Uri("pack://application:,,,/Styleplorer;component/Resources/GroupFolderIcon.png"));
            AddGroupPanel.Visibility = Visibility.Collapsed;
            CreateGroupButton.Content = "Создать";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            AddGroupPanel.Visibility = Visibility.Collapsed;
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var button = sender as Button;
            var group = button.DataContext as GroupListItem;

            if (MessageBox.Show($"Вы уверены, что хотите удалить группу '{group.Name}'?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RemoveGroup(group);
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var button = sender as Button;
            var item = button.DataContext as FileSystemObject;

            var expander = FindParent<Expander>(button);

            if (expander != null)
            {
                var groupListItem = expander.DataContext as GroupListItem;
                if (groupListItem != null)
                {
                    if (MessageBox.Show($"Вы уверены, что хотите удалить элемент '{item.Name}' из группы '{groupListItem.Name}'?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        RemoveItemFromGroup(groupListItem.Name, item);
                    }
                }
                else
                {
                    if (MessageBox.Show($"Вы уверены, что хотите удалить элемент '{item.Name}' из избранного?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        RemoveFromFavorite(item);
                    }
                }
            }
        }

        private void RemoveGroup(GroupListItem group)
        {
            groupsCollection.Remove(group);

            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string favoritesFile = Path.Combine(projectPath, "Favorites.txt");

            List<string> lines = File.ReadAllLines(favoritesFile).ToList();
            int startIndex = lines.FindIndex(line => line.StartsWith($"#{group.Name}"));

            if (startIndex != -1)
            {
                int endIndex = lines.FindIndex(startIndex + 1, line => line.StartsWith("#"));
                if (endIndex == -1) endIndex = lines.Count;

                lines.RemoveRange(startIndex, endIndex - startIndex);
                File.WriteAllLines(favoritesFile, lines);
            }
            UpdateActiveListBoxItemsAfterRemove();
        }

        private void RemoveItemFromGroup(string groupName, FileSystemObject item)
        {
            var group = groupsCollection.FirstOrDefault(g => g.Name == groupName);
            if (group != null)
            {
                var itemToRemove = group.Items.FirstOrDefault(i => i.Path == item.Path);

                if (itemToRemove != null)
                {
                    group.Items.Remove(itemToRemove);

                    string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                    string favoritesFile = Path.Combine(projectPath, "Favorites.txt");

                    List<string> lines = File.ReadAllLines(favoritesFile).ToList();
                    int groupIndex = lines.FindIndex(line => line.StartsWith($"#{groupName}"));

                    if (groupIndex != -1)
                    {
                        int itemIndex = lines.FindIndex(groupIndex + 1, line => line.Trim() == item.Path);
                        if (itemIndex != -1)
                        {
                            lines.RemoveAt(itemIndex);
                            File.WriteAllLines(favoritesFile, lines);
                        }
                    }
                }
                UpdateActiveListBoxItemsAfterRemove();
            }
        }

        private void EditGroup_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var button = sender as Button;
            var group = button.DataContext as GroupListItem;

            ShowEditGroupPanel(group);
        }

        private void ShowEditGroupPanel(GroupListItem group)
        {
            GroupNameTextBox.Text = group.Name;
            GroupImage.Source = group.Image;

            CreateGroupButton.Content = "Изменить";

            AddGroupPanel.Visibility = Visibility.Visible;
            GroupScrollViewer.ScrollToEnd();

            currentEditingGroup = group;
        }

        private void UpdateGroup(GroupListItem group, string newName, ImageSource newImage)
        {
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string favoritesFile = Path.Combine(projectPath, "Favorites.txt");
            string groupImagesFolder = Path.Combine(projectPath, "GroupImages");

            List<string> lines = File.ReadAllLines(favoritesFile).ToList();
            int groupIndex = lines.FindIndex(line => line.StartsWith($"#{group.Name}"));

            if (groupIndex != -1)
            {
                string oldImageFileName = null;
                string[] parts = lines[groupIndex].Split('#');
                if (parts.Length > 2)
                {
                    oldImageFileName = parts[2].Trim();
                }

                string newImageFileName = null;
                if (isNewImage && newImage != null && !(newImage is BitmapImage defaultImage && defaultImage.UriSource.ToString().Contains("GroupFolderIcon.png")))
                {
                    newImageFileName = $"{Guid.NewGuid()}.png";
                    string imagePath = Path.Combine(groupImagesFolder, newImageFileName);

                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create((BitmapSource)newImage));
                    using (var fileStream = new FileStream(imagePath, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }

                    // Удалить старое изображение, если оно существует
                    if (!string.IsNullOrEmpty(oldImageFileName))
                    {
                        string oldImagePath = Path.Combine(groupImagesFolder, oldImageFileName);
                        if (File.Exists(oldImagePath))
                        {
                            File.Delete(oldImagePath);
                        }
                    }
                }
                else if (!isNewImage)
                {
                    newImageFileName = oldImageFileName;
                }

                lines[groupIndex] = newImageFileName != null ? $"#{newName}#{newImageFileName}" : $"#{newName}";

                File.WriteAllLines(favoritesFile, lines);

                group.Name = newName;
                group.Image = newImage;

                GroupsListView.Items.Refresh();
            }
        }

        private GroupListItem currentEditingGroup;

        public void UpdateGroupContextMenu(MenuItem groupMenuItem, object clickedItem)
        {
            groupMenuItem.Items.Clear();

            // Добавляем пункт "Нет группы"
            var noGroupMenuItem = new MenuItem { Header = "Нет группы" };
            noGroupMenuItem.Click += (sender, e) => ChangeItemGroup(clickedItem, null);
            groupMenuItem.Items.Add(noGroupMenuItem);

            // Добавляем существующие группы
            foreach (var group in groupsCollection)
            {
                var groupSubMenuItem = new MenuItem { Header = group.Name };
                groupSubMenuItem.Click += (sender, e) => ChangeItemGroup(clickedItem, group);
                groupMenuItem.Items.Add(groupSubMenuItem);
            }

            // Отмечаем текущую группу
            var currentGroup = (clickedItem as FileSystemObject)?.Group;
            foreach (MenuItem item in groupMenuItem.Items)
            {
                if ((currentGroup == null && item.Header.ToString() == "Нет группы") ||
                    (currentGroup != null && item.Header.ToString() == currentGroup))
                {
                    item.IsChecked = true;
                    break;
                }
            }
        }

        private void ChangeItemGroup(object item, GroupListItem newGroup)
        {
            if (item is FileSystemObject fileSystemObject)
            {
                // Удаляем из старой группы, если она была
                if (fileSystemObject.Group != null)
                {
                    RemoveItemFromGroup(fileSystemObject.Group, fileSystemObject);
                }

                // Добавляем в новую группу, если она не null
                if (newGroup != null)
                {
                    AddItemToGroup(newGroup.Name, fileSystemObject.Path);
                    fileSystemObject.Group = newGroup.Name;
                    fileSystemObject.GroupImage = newGroup.Image;
                }
                else
                {
                    fileSystemObject.Group = null;
                    fileSystemObject.GroupImage = null;
                }
            }
        }

        private void UpdateActiveListBoxItemsAfterRemove()
        {
            foreach (var item in ActiveListBox.Items)
            {
                if (item is FileSystemObject fileSystemObject)
                {
                    fileSystemObject.Group = null;
                    fileSystemObject.GroupImage = null;
                }
            }
        }
        #endregion

        #region Пункты меню
        bool isEditThemeShow = false; // Нужно для определиние открыто ли создание/редактирование темы

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            MainButton.Background = (Brush)window.Resources["SideMenuHighlightBrush"];
            ThemesButton.Background = Brushes.Transparent;
            SettingsButton.Background = Brushes.Transparent;

            TabControl1.Visibility = Visibility.Visible;
            ThemesGrid.Visibility = Visibility.Hidden;
            SettingsGrid.Visibility = Visibility.Hidden;
            if (EditThemeGrid.Visibility == Visibility.Visible)
            {
                isEditThemeShow = true;
            }
            else
            {
                isEditThemeShow = false;
            }
            EditThemeGrid.Visibility = Visibility.Hidden;
        }

        private void ThemesButton_Click(object sender, RoutedEventArgs e)
        {
            MainButton.Background = Brushes.Transparent;
            ThemesButton.Background = (Brush)window.Resources["SideMenuHighlightBrush"];
            SettingsButton.Background = Brushes.Transparent;

            TabControl1.Visibility = Visibility.Hidden;
            ThemesGrid.Visibility = Visibility.Visible;
            SettingsGrid.Visibility = Visibility.Hidden;
            if (isEditThemeShow)
            {
                EditThemeGrid.Visibility = Visibility.Visible;
            }
            else
            {
                EditThemeGrid.Visibility = Visibility.Hidden;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainButton.Background = Brushes.Transparent;
            ThemesButton.Background = Brushes.Transparent;
            SettingsButton.Background = (Brush)window.Resources["SideMenuHighlightBrush"];

            TabControl1.Visibility = Visibility.Hidden;
            ThemesGrid.Visibility = Visibility.Hidden;
            SettingsGrid.Visibility = Visibility.Visible;
            if (EditThemeGrid.Visibility == Visibility.Visible)
            {
                isEditThemeShow = true;
            }
            else
            {
                isEditThemeShow = false;
            }
            EditThemeGrid.Visibility = Visibility.Hidden;
        }
        #endregion

        #region Страница настроек тем
        #region Установка тёмной/светлой темы
        private void DarkLightTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DarkLightTheme.SelectedItem is ComboBoxItem selectedItem && DarkLightTheme.IsEnabled)
            {
                bool isDarkTheme = selectedItem.Name == "DarkTheme";
                themeSettings["isDarkLightTheme"] = true;
                if (isDarkTheme)
                {
                    DesktopColorAnalyzer.ResetStyles(this);
                    themeSettings["isDarkTheme"] = true;
                    SaveThemeSettings();
                }
                else
                {
                    SetLightTheme();
                    themeSettings["isDarkTheme"] = false;
                    SaveThemeSettings();
                }
            }
        }

        public void SetLightTheme()
        {
            Resources["TextColorBrush"] = new SolidColorBrush(Colors.Black);
            Resources["BorderColorBrush"] = new SolidColorBrush(Colors.LightGray);
            Resources["WindowBorderColorBrush"] = new SolidColorBrush(Color.FromRgb(0xc4, 0xc9, 0xce));
            Resources["RectangleBrush"] = new SolidColorBrush(Color.FromRgb(0xe3, 0xe5, 0xe8));
            Resources["SideMenuBrush"] = new SolidColorBrush(Color.FromRgb(0xf2, 0xf3, 0xf5));
            Resources["MainGridBrush"] = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
            Resources["TopPanelBrush"] = new SolidColorBrush(Color.FromRgb(0xeb, 0xed, 0xef));
            Resources["BottomPanelBrush"] = new SolidColorBrush(Color.FromRgb(0xeb, 0xed, 0xef));
            Resources["RectangleBaseColor"] = (Color)window.Resources["DefaultRectangleBaseColor"];
            Resources["FolderColor"] = (Color)window.Resources["DefaultFolderColor"];
            SelectedFolderColor = Colors.Transparent;
            DesktopColorAnalyzer.UpdateHighlightColors(window);
            DesktopColorAnalyzer.UpdateButtonBackgrounds();
            SetFoldersColor();
        }
        #endregion

        #region Установка адаптивных тем под обои рабочего стола
        private void WallpaperColorCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DarkLightTheme.IsEnabled = false;
            SetSelectedThemeCheckBox.IsChecked = false;
            themeListBox.IsEnabled = false;
            MonitorComboBox.IsEnabled = true;
            lastSelectedTheme = themeListBox.SelectedItem as Theme;
            themeSettings["isWallpaperMonitor"] = true;
            themeSettings["isDarkLightTheme"] = false;
            SaveThemeSettings();
            DesktopColorAnalyzer.AnalyzeAndApplyColors(this);
            WallpaperMonitor.Start(this);
        }

        private void WallpaperColorCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            MonitorComboBox.IsEnabled = false;
            themeSettings["isWallpaperMonitor"] = false;
            SaveThemeSettings();
            WallpaperMonitor.Stop();
            if (SetSelectedThemeCheckBox.IsChecked != true)
            {
                DarkLightTheme.IsEnabled = true;
                themeSettings["isDarkLightTheme"] = true;
                SaveThemeSettings();
                if (DarkTheme.IsSelected)
                {
                    DesktopColorAnalyzer.ResetStyles(this);
                }
                else
                {
                    SetLightTheme();
                }
            }
        }
        #endregion

        #region Использовать выбранную тему
        private void SetSelectedThemeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DarkLightTheme.IsEnabled = false;
            WallpaperColorCheckBox.IsChecked = false;
            themeListBox.IsEnabled = true;
            themeSettings["isSetSelectedTheme"] = true;
            themeSettings["isDarkLightTheme"] = false;
            SaveThemeSettings();
            if (lastSelectedTheme != null)
            {
                themeListBox.SelectedItem = lastSelectedTheme;
                ApplyTheme(lastSelectedTheme);
            }
        }

        private void SetSelectedThemeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            themeSettings["isSetSelectedTheme"] = false;
            SaveThemeSettings();
            themeListBox.IsEnabled = false;
            if (WallpaperColorCheckBox.IsChecked != true)
            {
                themeSettings["isDarkLightTheme"] = true;
                SaveThemeSettings();
                DarkLightTheme.IsEnabled = true;
                if (DarkTheme.IsSelected)
                {
                    DesktopColorAnalyzer.ResetStyles(this);
                }
                else
                {
                    SetLightTheme();
                }
            }
        }
        #endregion

        // Выбор монитора
        private void MonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MonitorComboBox.SelectedItem != null && WallpaperColorCheckBox.IsChecked == true)
            {
                selectedMonitor = MonitorComboBox.SelectedItem.ToString();
                themeSettings["selectedMonitor"] = selectedMonitor;
                SaveThemeSettings();
                string wallpaperPath = DesktopColorAnalyzer.GetWallpaperPathForMonitor(selectedMonitor);
                if (!string.IsNullOrEmpty(wallpaperPath))
                {
                    DesktopColorAnalyzer.AnalyzeAndApplyColors(this);
                }
            }
        }
        #endregion

        #region Страница настроек
        private void ShowFileExtensionsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.ShowFileExtensions = true;
            AppSettings.SaveSettings();
            ActiveListBox?.Refresh();
        }

        private void ShowFileExtensionsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.ShowFileExtensions = false;
            AppSettings.SaveSettings();
            ActiveListBox?.Refresh();
        }

        private void ShowHiddenFilesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.ShowHiddenFiles = true;
            AppSettings.SaveSettings();
            ActiveListBox?.Refresh();
        }

        private void ShowHiddenFilesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.ShowHiddenFiles = false;
            AppSettings.SaveSettings();
            ActiveListBox?.Refresh();
        }

        private void ShowSystemFilesCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.ShowSystemFiles = true;
            AppSettings.SaveSettings();
            ActiveListBox?.Refresh();
        }

        private void ShowSystemFilesCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.ShowSystemFiles = false;
            AppSettings.SaveSettings();
            ActiveListBox?.Refresh();
        }

        private void EnablePreviewCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.EnablePreview = true;
            AppSettings.SaveSettings();
        }

        private void EnablePreviewCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.EnablePreview = false;
            AppSettings.SaveSettings();
        }

        private void ConfirmDeletionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.ConfirmDeletion = true;
            AppSettings.SaveSettings();
        }

        private void ConfirmDeletionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.ConfirmDeletion = false;
            AppSettings.SaveSettings();
        }

        private void OpenFilesWithSingleClickCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.OpenFilesWithSingleClick = true;
            AppSettings.SaveSettings();
        }

        private void OpenFilesWithSingleClickCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.OpenFilesWithSingleClick = false;
            AppSettings.SaveSettings();
        }

        private void RememberWindowSizeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.RememberWindowSize = true;
            AppSettings.SaveSettings();
        }

        private void RememberWindowSizeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.RememberWindowSize = false;
            AppSettings.SaveSettings();
        }

        private void OpenLastActiveFolderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.OpenLastActiveFolder = true;
            AppSettings.SaveSettings();
        }

        private void OpenLastActiveFolderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.OpenLastActiveFolder = false;
            AppSettings.SaveSettings();
        }
        #endregion

        #region Работа с темами
        private void SetThemesFilePath() // Установка пути для файла с настройками тем
        {
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            themesSettingsFilePath = Path.Combine(projectPath, "ThemeSettings.json");
        }

        private void LoadThemeSettings() // Загрузка настроек тем
        {
            if (File.Exists(themesSettingsFilePath))
            {
                string jsonString = File.ReadAllText(themesSettingsFilePath);
                var jsonDocument = JsonDocument.Parse(jsonString);
                var root = jsonDocument.RootElement;

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.True || property.Value.ValueKind == JsonValueKind.False)
                    {
                        themeSettings[property.Name] = property.Value.GetBoolean();
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        themeSettings[property.Name] = property.Value.GetString();
                    }
                }
            }
            else
            {
                themeSettings = new Dictionary<string, object>();
            }

            if (!themeSettings.ContainsKey("isDarkLightTheme"))
            {
                themeSettings["isDarkLightTheme"] = false;
            }
            if (!themeSettings.ContainsKey("isDarkTheme"))
            {
                themeSettings["isDarkTheme"] = false;
            }
            if (!themeSettings.ContainsKey("isWallpaperMonitor"))
            {
                themeSettings["isWallpaperMonitor"] = false;
            }
            if (!themeSettings.ContainsKey("isSetSelectedTheme"))
            {
                themeSettings["isSetSelectedTheme"] = false;
            }
            if (!themeSettings.ContainsKey("selectedTheme"))
            {
                themeSettings["selectedTheme"] = "";
            }
            if (!themeSettings.ContainsKey("selectedMonitor"))
            {
                themeSettings["selectedMonitor"] = "Monitor0";
            }

            bool isLightDarkTheme = Convert.ToBoolean(themeSettings["isDarkLightTheme"]);
            if (isLightDarkTheme)
            {
                DarkLightTheme.IsEnabled = true;
            }
            else
            {
                DarkLightTheme.IsEnabled = false;
            }
            bool isDarkTheme = Convert.ToBoolean(themeSettings["isDarkTheme"]);

            if (isDarkTheme)
            {
                DarkLightTheme.SelectedItem = DarkLightTheme.Items.Cast<object>().FirstOrDefault(item => (item as dynamic).Name == "DarkTheme");
            }
            else
            {
                DarkLightTheme.SelectedItem = DarkLightTheme.Items.Cast<object>().FirstOrDefault(item => (item as dynamic).Name == "LightTheme");
            }

            WallpaperColorCheckBox.IsChecked = Convert.ToBoolean(themeSettings["isWallpaperMonitor"]);
            SetSelectedThemeCheckBox.IsChecked = Convert.ToBoolean(themeSettings["isSetSelectedTheme"]);

            string selectedThemeName = (string)themeSettings["selectedTheme"];
            if (!string.IsNullOrEmpty(selectedThemeName))
            {
                var selectedTheme = themes.FirstOrDefault(t => t.Name == selectedThemeName);
                if (selectedTheme != null)
                {
                    themeListBox.SelectedItem = selectedTheme;
                    lastSelectedTheme = selectedTheme;
                }
                else
                {
                    themeSettings["selectedTheme"] = "";
                    SaveThemeSettings();
                }
            }

            string savedMonitor = (string)themeSettings["selectedMonitor"];
            if (!string.IsNullOrEmpty(savedMonitor))
            {
                MonitorComboBox.SelectedItem = savedMonitor;
            }
        }

        private void LoadThemes() // Загрузка тем
        {
            themes = new List<Theme>();
            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            var themesPath = Path.Combine(projectPath, "Themes");
            if (!Directory.Exists(themesPath))
            {
                Directory.CreateDirectory(themesPath);
            }

            foreach (var themeFolder in Directory.GetDirectories(themesPath))
            {
                var themeName = new DirectoryInfo(themeFolder).Name;
                var stylePath = Path.Combine(themeFolder, "style.xaml");
                var imagePath = GetImageFile(themeFolder, "image");
                var previewPath = GetImageFile(themeFolder, "preview");

                if (File.Exists(stylePath))
                {
                    themes.Add(new Theme
                    {
                        Name = themeName,
                        StylePath = stylePath,
                        Image = LoadImageSource(imagePath),
                        Preview = LoadImageSource(previewPath)
                    });
                }
            }
            themeListBox.ItemsSource = themes;
        }

        private string GetImageFile(string folderPath, string prefix) // Проверка на существующую картинку
        {
            string[] extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
            foreach (var ext in extensions)
            {
                var file = Path.Combine(folderPath, prefix + ext);
                if (File.Exists(file))
                {
                    return file;
                }
            }
            return null;
        }

        private ImageSource LoadImageSource(string path) // Загрузка картинки
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ApplyTheme(Theme selectedTheme) // Применение темы
        {
            if (selectedTheme != null)
            {
                ThemeManager.ApplyTheme(new Uri(selectedTheme.StylePath, UriKind.Absolute));
                DesktopColorAnalyzer.UpdateHighlightColors(this);
                SelectedFolderColor = (Color)Resources["FolderColor"];
                SetFoldersColor();
            }
        }

        private void ThemeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) // Выбор темы и её установка
        {
            if (themeListBox.SelectedItem is Theme selectedTheme && SetSelectedThemeCheckBox.IsChecked == true)
            {
                ApplyTheme(selectedTheme);
                lastSelectedTheme = selectedTheme;
                themeSettings["selectedTheme"] = selectedTheme.Name;
                SaveThemeSettings();
            }
        }

        private void SaveThemeSettings() // Сохранение настроек тем
        {
            string jsonString = JsonSerializer.Serialize(themeSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(themesSettingsFilePath, jsonString);
        }

        private double currentZoom = 1.0;
        private const double zoomFactor = 0.1;
        private const double minZoom = 0.7;
        private const double maxZoom = 2.6;

        // Установка выбранных цветов из ColorPickers
        public void SetColorPickerColor()
        {
            SolidColorBrush brush = (SolidColorBrush)FindResource("TextColorBrush");
            TextColorPicker.SelectedColor = brush.Color;
            brush = (SolidColorBrush)FindResource("BorderColorBrush");
            WindowBorderColorPicker.SelectedColor = brush.Color;
            brush = (SolidColorBrush)FindResource("WindowBorderColorBrush");
            BorderBrushColorPicker.SelectedColor = brush.Color;
            brush = (SolidColorBrush)FindResource("MainGridBrush");
            MainGridColorPicker.SelectedColor = brush.Color;
            brush = (SolidColorBrush)FindResource("SideMenuBrush");
            SideMenuColorPicker.SelectedColor = brush.Color;
            brush = (SolidColorBrush)FindResource("RectangleBrush");
            TitleBarColorPicker.SelectedColor = brush.Color;
            brush = (SolidColorBrush)FindResource("TopPanelBrush");
            TopPanelColorPicker.SelectedColor = brush.Color;
            brush = (SolidColorBrush)FindResource("BottomPanelBrush");
            BottomPanelColorPicker.SelectedColor = brush.Color;
            Color color = (Color)FindResource("RectangleBaseColor");
            brush = new SolidColorBrush(color);
            SelectPanelColorPicker.SelectedColor = brush.Color;
            color = (Color)FindResource("FolderColor");
            brush = new SolidColorBrush(color);
            FolderColorPicker.SelectedColor = brush.Color;
        }

        // Переход на страницу создания темы
        private void CreateNewTheme_Click(object sender, RoutedEventArgs e)
        {
            EditThemeGrid.Visibility = Visibility.Visible;
            ThemesGrid.Visibility = Visibility.Hidden;
            SetColorPickerColor();
            CreateTheme1.Content = "Создать тему";
        }

        // Обработка создания темы
        private void CreateTheme_Click(object sender, RoutedEventArgs e)
        {
            CreateTheme();
            EditThemeGrid.Visibility = Visibility.Hidden;
            ThemesGrid.Visibility = Visibility.Visible;
            isEdit = false;
        }

        // Функция создания темы
        public void CreateTheme()
        {
            string themeName = ThemeName.Text.Trim();
            if (string.IsNullOrEmpty(themeName))
            {
                MessageBox.Show("Пожалуйста, введите имя темы.");
                return;
            }

            string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            string themesPath = Path.Combine(projectPath, "Themes");
            string themeFolder = Path.Combine(themesPath, themeName);
            string stylePath = Path.Combine(themeFolder, "style.xaml");

            if (Directory.Exists(themeFolder))
            {
                if (!isEdit)
                {
                    var result = MessageBox.Show("Тема с таким именем уже существует. Хотите перезаписать?", "Подтверждение", MessageBoxButton.YesNo);
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(themeFolder);
            }
            EditThemeGrid.Visibility = Visibility.Hidden;
            TabControl1.Visibility = Visibility.Visible;
            BitmapSource screenshot = CreateScreenshot(this);
            EditThemeGrid.Visibility = Visibility.Visible;
            TabControl1.Visibility = Visibility.Hidden;
            string screenshotPath = Path.Combine(themeFolder, "preview.png");
            using (var fileStream = new FileStream(screenshotPath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(screenshot));
                encoder.Save(fileStream);
            }
            // Создаем содержимое файла style.xaml
            string xamlContent = $@"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <SolidColorBrush x:Key=""MainGridBrush"" Color=""{MainGridColorPicker.SelectedColor}"" />
    <SolidColorBrush x:Key=""RectangleBrush"" Color=""{TitleBarColorPicker.SelectedColor}"" />
    <SolidColorBrush x:Key=""SideMenuBrush"" Color=""{SideMenuColorPicker.SelectedColor}"" />
    <SolidColorBrush x:Key=""TextColorBrush"" Color=""{TextColorPicker.SelectedColor}"" />
    <SolidColorBrush x:Key=""BorderColorBrush"" Color=""{WindowBorderColorPicker.SelectedColor}"" />
    <SolidColorBrush x:Key=""WindowBorderColorBrush"" Color=""{BorderBrushColorPicker.SelectedColor}"" />
    <SolidColorBrush x:Key=""TopPanelBrush"" Color=""{TopPanelColorPicker.SelectedColor}"" />
    <SolidColorBrush x:Key=""BottomPanelBrush"" Color=""{BottomPanelColorPicker.SelectedColor}"" />
    <Color x:Key=""RectangleBaseColor"">{SelectPanelColorPicker.SelectedColor}</Color>
    <Color x:Key=""FolderColor"">{FolderColorPicker.SelectedColor}</Color>
</ResourceDictionary>";

            // Сохраняем файл style.xaml
            File.WriteAllText(stylePath, xamlContent);

            //MessageBox.Show($"Тема '{themeName}' успешно создана!");

            // Обновляем список тем
            LoadThemes();
        }

        // Создание preview картинки для темы
        public BitmapSource CreateScreenshot(Visual target)
        {
            if (target == null) return null;

            Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(target);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
            }

            rtb.Render(dv);
            return rtb;
        }

        // Отмена создания темы
        private void CancelCreateTheme_Click(object sender, RoutedEventArgs e)
        {
            EditThemeGrid.Visibility = Visibility.Hidden;
            ThemesGrid.Visibility = Visibility.Visible;
            SelectedFolderColor = Colors.Transparent;
            if (SetSelectedThemeCheckBox.IsChecked == false && WallpaperColorCheckBox.IsChecked == false)
            {
                DesktopColorAnalyzer.ResetStyles(this);
            }
            else if (SetSelectedThemeCheckBox.IsChecked == true)
            {
                if (lastSelectedTheme != null)
                {
                    themeListBox.SelectedItem = lastSelectedTheme;
                    ApplyTheme(lastSelectedTheme);
                }
            }
            else if (WallpaperColorCheckBox.IsChecked == true)
            {
                DesktopColorAnalyzer.AnalyzeAndApplyColors(this);
                WallpaperMonitor.Start(this);
            }
            SetFoldersColor();
        }

        #region ColorPickers
        private void WindowBorderColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["WindowBorderColorBrush"] = new SolidColorBrush(e.NewValue.Value);
            }
        }

        private void BorderBrushColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["BorderColorBrush"] = new SolidColorBrush(e.NewValue.Value);
            }
        }

        private void TextColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["TextColorBrush"] = new SolidColorBrush(e.NewValue.Value);
            }
        }

        private void MainGridColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["MainGridBrush"] = new SolidColorBrush(e.NewValue.Value);
            }
        }

        private void SideMenuColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["SideMenuBrush"] = new SolidColorBrush(e.NewValue.Value);
                DesktopColorAnalyzer.UpdateHighlightColors(this);
            }
        }

        private void TitleBarColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["RectangleBrush"] = new SolidColorBrush(e.NewValue.Value);
                DesktopColorAnalyzer.UpdateHighlightColors(this);
            }
        }

        private void TopPanelColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["TopPanelBrush"] = new SolidColorBrush(e.NewValue.Value);
            }
        }

        private void BottomPanelColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["BottomPanelBrush"] = new SolidColorBrush(e.NewValue.Value);
            }
        }

        private void SelectPanelColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["RectangleBaseColor"] = e.NewValue.Value;
            }
        }

        private void FolderColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (e.NewValue.HasValue)
            {
                Resources["FolderColor"] = e.NewValue.Value;
                SelectedFolderColor = e.NewValue.Value;
                SetFoldersColor();
            }
        }
        #endregion

        // Выбранный цвет для папок
        public static Color SelectedFolderColor { get; set; } = Colors.Transparent;

        // Устновка/смена цвета для папок
        public static BitmapSource ChangeImageColor(BitmapSource originalImage)
        {
            if (originalImage == null)
            {
                return null;
            }
            if (SelectedFolderColor == Colors.Transparent)
            {
                return originalImage;
            }

            var width = originalImage.PixelWidth;
            var height = originalImage.PixelHeight;
            var stride = width * ((originalImage.Format.BitsPerPixel + 7) / 8);
            var pixels = new byte[height * stride];

            originalImage.CopyPixels(pixels, stride, 0);

            // Преобразуем новый цвет в HSL
            var (newHue, newSaturation, newLightness) = RgbToHsl(SelectedFolderColor.R, SelectedFolderColor.G, SelectedFolderColor.B);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] != 0) // Если пиксель не прозрачный
                {
                    byte alpha = pixels[i + 3];

                    if (alpha > 0)
                    {
                        // Преобразуем текущий пиксель в HSL
                        var (hue, _, lightness) = RgbToHsl(pixels[i + 2], pixels[i + 1], pixels[i]);

                        // Если оттенок жёлтый
                        if (hue >= 45 && hue <= 65)
                        {
                            // Корректируем яркость в зависимости от нового цвета
                            double lightnessAdjustment = (newLightness - 0.5) * 0.7;
                            double blendedLightness = lightness + lightnessAdjustment;

                            // Применяем квадратичную функцию к яркости, чтобы усилить контраст
                            // и сделать тёмные области темнее, а светлые - светлее
                            blendedLightness = Math.Pow(blendedLightness, 2);

                            // Ограничиваем значение яркости в пределах [0, 1]
                            blendedLightness = Math.Max(0, Math.Min(1, blendedLightness));

                            // Применяем новый оттенок и насыщенность, сохраняя исходную яркость
                            var (r, g, b) = HslToRgb(newHue, newSaturation, blendedLightness);

                            // Применяем альфа-смешивание
                            pixels[i] = (byte)((b * alpha + pixels[i] * (255 - alpha)) / 255);
                            pixels[i + 1] = (byte)((g * alpha + pixels[i + 1] * (255 - alpha)) / 255);
                            pixels[i + 2] = (byte)((r * alpha + pixels[i + 2] * (255 - alpha)) / 255);
                        }
                    }
                }
            }

            var newImage = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            newImage.Freeze();

            return newImage;
        }

        public static (double Hue, double Saturation, double Lightness) RgbToHsl(byte r, byte g, byte b)
        {
            var rf = r / 255.0;
            var gf = g / 255.0;
            var bf = b / 255.0;

            var max = Math.Max(rf, Math.Max(gf, bf));
            var min = Math.Min(rf, Math.Min(gf, bf));
            var delta = max - min;

            var lightness = (max + min) / 2.0;
            var saturation = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * lightness - 1));
            double hue;

            if (delta == 0)
            {
                hue = 0;
            }
            else if (max == rf)
            {
                hue = 60 * (((gf - bf) / delta) % 6);
            }
            else if (max == gf)
            {
                hue = 60 * (((bf - rf) / delta) + 2);
            }
            else
            {
                hue = 60 * (((rf - gf) / delta) + 4);
            }

            hue = (hue + 360) % 360;

            return (hue, saturation, lightness);
        }

        public static (byte R, byte G, byte B) HslToRgb(double hue, double saturation, double lightness)
        {
            var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            var m = lightness - c / 2;

            double rf, gf, bf;

            if (hue < 60)
            {
                (rf, gf, bf) = (c, x, 0);
            }
            else if (hue < 120)
            {
                (rf, gf, bf) = (x, c, 0);
            }
            else if (hue < 180)
            {
                (rf, gf, bf) = (0, c, x);
            }
            else if (hue < 240)
            {
                (rf, gf, bf) = (0, x, c);
            }
            else if (hue < 300)
            {
                (rf, gf, bf) = (x, 0, c);
            }
            else
            {
                (rf, gf, bf) = (c, 0, x);
            }

            byte r = (byte)Math.Round((rf + m) * 255);
            byte g = (byte)Math.Round((gf + m) * 255);
            byte b = (byte)Math.Round((bf + m) * 255);

            return (r, g, b);
        }

        // Установка цветов для папок
        public void SetFoldersColor()
        {
            foreach (var listBox in FindVisualChildren<CustomListView>(this))
            {
                foreach (var item in listBox.Items)
                {
                    if (listBox.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
                    {
                        FolderColorHelper.SetFolderColor(container, (Color)Resources["FolderColor"]);
                    }
                }
            }
        }

        bool isEdit = false;
        // Редактирование темы
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var item = (Theme)button.DataContext;
            themeListBox.SelectedItem = item;
            ApplyTheme(item);
            lastSelectedTheme = item;
            themeSettings["selectedTheme"] = item.Name;
            SaveThemeSettings();
            ThemeName.Text = item.Name;
            EditThemeGrid.Visibility = Visibility.Visible;
            SetColorPickerColor();
            isEdit = true;
            CreateTheme1.Content = "Изменить";
        }

        // Удаление темы
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var item = (Theme)button.DataContext;

            var result = MessageBox.Show($"Вы действительно хотите удалить тему {item.Name}?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                themes.Remove(item);
                themeListBox.ItemsSource = null;
                themeListBox.ItemsSource = themes;

                string projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                var themesPath = Path.Combine(projectPath, "Themes");
                var themeFolder = Path.Combine(themesPath, item.Name);

                if (Directory.Exists(themeFolder))
                {
                    try
                    {
                        Directory.Delete(themeFolder, true);
                        //MessageBox.Show($"Тема {item.Name} успешно удалена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        if (themeListBox.Items.Count > 0)
                        {
                            themeListBox.SelectedItem = themeListBox.Items[0];
                            ApplyTheme((Theme)themeListBox.Items[0]);
                        }
                        else
                        {
                            DesktopColorAnalyzer.ResetStyles(this);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при удалении папки темы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        #endregion
    }

    public class GroupListItem // Класс группы
    {
        public string Name { get; set; } // Имя группы
        public ImageSource Image { get; set; } // Картинка группы
        public ObservableCollection<FileSystemObject> Items { get; set; } // Элементы группы (пока что только папки, но можно любые)
    }

    public class Theme // Класс темы
    {
        public string Name { get; set; } // Имя темы
        public string StylePath { get; set; } // Путь к стилю темы
        public ImageSource Image { get; set; } // Путь к изображению (это должно было быть изображением заднего фона, но пока что оно не устанавливается и нигде не используется, кроме загрузки)
        public ImageSource Preview { get; set; } // Превью картинка. Атоматически устанавливается при создании темы
    }

    public class PathElement // Элемент строки с полным текущим путём
    {
        public string Name { get; set; } // Имя элемента (отображается)
        public string FullPath { get; set; } // Полный путь элемента (для перехода)
        public bool IsLast { get; set; } // Определяет последний ли это элемент, если да, то сдвигать его (нужно, чтобы элементы не выходили за границу контейнера)
    }

    // Конвертер для обрезания текста и добавления многоточия, если текст длинный
    public class TextTrimConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверка типа значения и параметра
            if (value is string text && parameter is string maxWidthStr && double.TryParse(maxWidthStr, out double maxWidth))
            {
                // Форматирование текста
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

                // Проверка ширины текста
                if (formattedText.Width > maxWidth)
                {
                    // Обрезка текста и добавление многоточия
                    return text.Substring(0, 3) + "...";
                }
            }
            return value; // Возврат исходного значения, если проверки не пройдены
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Конвертер для инвертирования логического значения
    //public class InverseBooleanConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        if (value is bool boolValue)
    //            return !boolValue; // Инвертирование логического значения
    //        return value; // Возврат исходного значения, если тип не совпадает
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        if (value is bool boolValue)
    //            return !boolValue; // Инвертирование логического значения
    //        return value; // Возврат исходного значения, если тип не совпадает
    //    }
    //}

    public class TabItemData // Данные для вкладки (устанавливаются в Tag свойстве элемента)
    {
        public string CurrentPath { get; set; } // Текущий полный путь
        public double DesiredWidth { get; set; } // Длинна вкладки
    }

}