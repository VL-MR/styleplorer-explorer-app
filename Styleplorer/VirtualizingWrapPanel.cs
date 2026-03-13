using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Media;
using WpfToolkit.Controls;
using System.Windows.Media.Imaging;
using System.Collections.Concurrent;
using Dropbox.Api.Files;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Globalization;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Styleplorer;

public class VirtualizingWrapPanel : VirtualizingPanelBase
{
    private IconCache iconCache;
    private CancellationTokenSource cts;

    public VirtualizingWrapPanel()
    {
        iconCache = new IconCache();
        cts = new CancellationTokenSource();
        measureTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        measureTimer.Tick += MeasureTimer_Tick;
    }

    private DispatcherTimer measureTimer;
    private struct ItemRangeStruct
    {
        public int StartIndex { get; }

        public int EndIndex { get; }

        public ItemRangeStruct(int startIndex, int endIndex)
        {
            this = default(ItemRangeStruct);
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public bool Contains(int itemIndex)
        {
            if (itemIndex >= StartIndex)
            {
                return itemIndex <= EndIndex;
            }

            return false;
        }
    }

    public static readonly DependencyProperty SpacingModeProperty = DependencyProperty.Register("SpacingMode", typeof(SpacingMode), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(SpacingMode.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(Orientation), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure, delegate (DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        ((VirtualizingWrapPanel)obj).Orientation_Changed();
    }));

    public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register("ItemSize", typeof(Size), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(Size.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty StretchItemsProperty = DependencyProperty.Register("StretchItems", typeof(bool), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ExpandedItemTemplateProperty = DependencyProperty.Register("ExpandedItemTemplate", typeof(DataTemplate), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ExpandedItemProperty = DependencyProperty.Register("ExpandedItem", typeof(object), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, delegate (DependencyObject o, DependencyPropertyChangedEventArgs a)
    {
        ((VirtualizingWrapPanel)o).ExpandedItemPropertyChanged(a);
    }));

    private VirtualizationCacheLength cacheLength;

    private VirtualizationCacheLengthUnit cacheLengthUnit;

    private Size childSize;

    private int rowCount;

    private int itemsPerRowCount;

    private FrameworkElement? expandedItemChild;

    private int itemIndexFollwingExpansion;

    public SpacingMode SpacingMode
    {
        get
        {
            return (SpacingMode)GetValue(SpacingModeProperty);
        }
        set
        {
            SetValue(SpacingModeProperty, value);
        }
    }

    public Orientation Orientation
    {
        get
        {
            return (Orientation)GetValue(OrientationProperty);
        }
        set
        {
            SetValue(OrientationProperty, value);
        }
    }

    public Size ItemSize
    {
        get
        {
            return (Size)GetValue(ItemSizeProperty);
        }
        set
        {
            SetValue(ItemSizeProperty, value);
        }
    }

    public bool StretchItems
    {
        get
        {
            return (bool)GetValue(StretchItemsProperty);
        }
        set
        {
            SetValue(StretchItemsProperty, value);
        }
    }

    public DataTemplate? ExpandedItemTemplate
    {
        get
        {
            return (DataTemplate)GetValue(ExpandedItemTemplateProperty);
        }
        set
        {
            SetValue(ExpandedItemTemplateProperty, value);
        }
    }

    public object? ExpandedItem
    {
        get
        {
            return GetValue(ExpandedItemProperty);
        }
        set
        {
            SetValue(ExpandedItemProperty, value);
        }
    }

    private ItemRangeStruct ItemRange { get; set; }

    private int ExpandedItemIndex
    {
        get
        {
            if (ExpandedItem != null)
            {
                return base.Items.IndexOf(ExpandedItem);
            }

            return -1;
        }
    }

    protected override void OnClearChildren()
    {
        base.OnClearChildren();
        expandedItemChild = null;
    }

    private HashSet<int> animatedItems = new HashSet<int>();

    private ConcurrentDictionary<int, Size> itemSizes = new ConcurrentDictionary<int, Size>();

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        animatedItems.Clear();
        //Dispatcher.BeginInvoke(new Action(MeasureItems), DispatcherPriority.Background);
        //MeasureItemsTask = MeasureItemsAsync();
        //isPopulating = true;
        //measureTimer.Stop();
        //measureTimer.Start();
        if (!measureTimer.IsEnabled)
        {
            measureTimer.Start();
        }

        //Dispatcher.BeginInvoke(new Action(async () =>
        //{
        //    MeasureItemsTask = await MeasureItemsAsync();
        //}), DispatcherPriority.Background);


        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                break;
            case NotifyCollectionChangedAction.Move:
                RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
                break;
        }
    }
    private void MeasureTimer_Tick(object sender, EventArgs e)
    {
        measureTimer.Stop();
        //if (isPopulating)
        //{
        //    isPopulating = false;
        MeasureItemsTask = MeasureItemsAsync();
        //}
    }
    //public void MeasureItems()
    //{
    //    itemSizes.Clear();
    //    for (int i = 0; i < ItemsControl.Items.Count; i++)
    //    {
    //        var container = ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
    //        if (container != null)
    //        {
    //            container.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

    //            itemSizes[i] = container.DesiredSize;
    //        }
    //    }
    //}
    //private void MeasureItems()
    //{
    //    if (Items == null || Items.Count == 0)
    //        return;

    //    double maxWidth = 110; // Фиксированная ширина
    //    double singleLineHeight = 119.96;
    //    double additionalHeightPerLine = 15.96;

    //    for (int i = 0; i < Items.Count; i++)
    //    {
    //        var item = Items[i] as FileSystemObject;
    //        if (item != null)
    //        {
    //            // Получаем контейнер для элемента
    //            var container = ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;

    //            if (container != null)
    //            {
    //                // Если контейнер существует, используем его для измерения
    //                container.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
    //                itemSizes[i] = container.DesiredSize;
    //            }
    //            else
    //            {
    //                // Если контейнер не существует, рассчитываем размер на основе имени
    //                int lineCount = CalculateLineCount(item.Name, maxWidth);
    //                double height = singleLineHeight + (lineCount - 1) * additionalHeightPerLine;
    //                itemSizes[i] = new Size(maxWidth, height);
    //            }
    //        }
    //    }

    //    InvalidateMeasure();
    //}

    //private int CalculateLineCount(string text, double maxWidth)
    //{
    //    var formattedText = new FormattedText(
    //        text,
    //        CultureInfo.CurrentCulture,
    //        FlowDirection.LeftToRight,
    //        new Typeface("Segoe UI"),
    //        12,
    //        Brushes.Black,
    //        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    //    int lineCount = 1;
    //    int lastSpace = -1;
    //    double lineWidth = 0;

    //    for (int i = 0; i < text.Length; i++)
    //    {
    //        char c = text[i];
    //        double charWidth = formattedText.WidthIncludingTrailingWhitespace;

    //        if (c == ' ')
    //            lastSpace = i;

    //        if (lineWidth + charWidth > maxWidth)
    //        {
    //            lineCount++;
    //            if (lastSpace != -1)
    //            {
    //                i = lastSpace;
    //                lastSpace = -1;
    //            }
    //            lineWidth = 0;
    //        }
    //        else
    //        {
    //            lineWidth += charWidth;
    //        }
    //    }

    //    return lineCount;
    //}
    private Task MeasureItemsTask;

    //private Size CalculateExtent(Size availableSize)
    //{
    //    double totalWidth = 0;
    //    double totalHeight = 0;
    //    double rowWidth = 0;
    //    double rowHeight = 0;

    //    for (int i = 0; i < base.Items.Count; i++)
    //    {
    //        //Size itemSize = itemSizes.ContainsKey(i) ? itemSizes[i] : childSize;
    //        Size itemSize = itemSizes.TryGetValue(i, out Size value) ? value : childSize;

    //        if (rowWidth + itemSize.Width > GetWidth(availableSize))
    //        {
    //            totalWidth = Math.Max(totalWidth, rowWidth);
    //            totalHeight += rowHeight;
    //            rowWidth = itemSize.Width;
    //            rowHeight = itemSize.Height;
    //        }
    //        else
    //        {
    //            rowWidth += itemSize.Width;
    //            rowHeight = Math.Max(rowHeight, itemSize.Height);
    //        }
    //    }

    //    totalWidth = Math.Max(totalWidth, rowWidth);
    //    totalHeight += rowHeight;

    //    if (expandedItemChild != null)
    //    {
    //        if (Orientation == Orientation.Horizontal)
    //        {
    //            totalHeight += expandedItemChild.DesiredSize.Height;
    //        }
    //        else
    //        {
    //            totalWidth = Math.Max(totalWidth, expandedItemChild.DesiredSize.Width);
    //        }
    //    }

    //    return CreateSize(Math.Max(totalWidth, GetWidth(availableSize)), totalHeight);
    //}
    //private Size CalculateExtent(Size availableSize)
    //{
    //    double totalWidth = 0;
    //    double totalHeight = 0;
    //    double rowWidth = 0;
    //    double rowHeight = 0;
    //    double maxWidth = GetWidth(availableSize);

    //    for (int i = 0; i < base.Items.Count; i++)
    //    {
    //        Size itemSize = itemSizes.TryGetValue(i, out Size value) ? value : childSize;

    //        if (rowWidth + itemSize.Width > maxWidth)
    //        {
    //            totalWidth = Math.Max(totalWidth, rowWidth);
    //            totalHeight += rowHeight;
    //            rowWidth = itemSize.Width;
    //            rowHeight = itemSize.Height;
    //        }
    //        else
    //        {
    //            rowWidth += itemSize.Width;
    //            rowHeight = Math.Max(rowHeight, itemSize.Height);
    //        }
    //    }

    //    totalWidth = Math.Max(totalWidth, rowWidth);
    //    totalHeight += rowHeight;

    //    //if (expandedItemChild != null)
    //    //{ 
    //    //    if (Orientation == Orientation.Horizontal)
    //    //    {
    //    //        totalHeight += expandedItemChild.DesiredSize.Height;
    //    //    }
    //    //    else
    //    //    {
    //    //        totalWidth = Math.Max(totalWidth, expandedItemChild.DesiredSize.Width);
    //    //    }
    //    //}

    //    return CreateSize(Math.Max(totalWidth, maxWidth), totalHeight);
    //}



    private Size CalculateExtent(Size availableSize)
    {
        //Console.Clear();
        double totalHeight = 0;
        double rowHeight = 0;
        int itemsInCurrentRow = 0;
        double maxWidth = GetWidth(availableSize);

        for (int i = 0; i < base.Items.Count; i++)
        {
            Size itemSize = itemSizes.TryGetValue(i, out Size value) ? value : new Size(0, 0);// childSize;
            
            //Console.WriteLine($"Item {i}: Name:{((FileSystemObject)Items[i]).Name} Size = {itemSize}");

            itemsInCurrentRow++;
            rowHeight = Math.Max(rowHeight, itemSize.Height);

            if (itemsInCurrentRow == itemsPerRowCount)
            {
                totalHeight += rowHeight;
                //Console.WriteLine("5rowHeight = " + rowHeight);
                rowHeight = 0;
                itemsInCurrentRow = 0;
                //Console.WriteLine("\n");
            }

            //Console.WriteLine("itemSize = " + itemSize);
        }

        if (itemsInCurrentRow > 0)
        {
            totalHeight += rowHeight;
        }

        //Console.WriteLine("Final Size = " + new Size(maxWidth, totalHeight + 10));
        return CreateSize(maxWidth, totalHeight + 10);
    }





    private void UpdateScrollInfo(Size availableSize, Size extent)
    {
        //Console.WriteLine("UpdateScrollInfo: " + extent);
        bool flag = false;
        if (extent != base.Extent)
        {
            base.Extent = extent;
            flag = true;
        }

        if (availableSize != base.ViewportSize)
        {
            base.ViewportSize = availableSize;
            flag = true;
        }

        // Обеспечиваем, чтобы ScrollOffset не выходил за пределы допустимого диапазона
        double maxVerticalOffset = Math.Max(0, extent.Height - availableSize.Height);
        double maxHorizontalOffset = Math.Max(0, extent.Width - availableSize.Width);

        base.ScrollOffset = new Point(
            Math.Min(Math.Max(base.ScrollOffset.X, 0), maxHorizontalOffset),
            Math.Min(Math.Max(base.ScrollOffset.Y, 0), maxVerticalOffset)
        );

        if (flag)
        {
            base.ScrollOwner?.InvalidateScrollInfo();
        }
    }







    private async Task<Task> MeasureItemsAsync()
    {
        if (Items == null || Items.Count == 0)
            return Task.CompletedTask;

        double maxWidth = 110;
        double singleLineHeight = 119.96000000000001;
        double additionalHeightPerLine = 15.96000000000001;

        var newItemSizes = new ConcurrentDictionary<int, Size>();

        return Task.Run(async () =>
        {
            Parallel.For(0, Items.Count, i =>
            {
                if (Items[i] is DriveObject)
                {
                    maxWidth = 290;
                }
                var item = Items[i] as FileSystemObject;
                if (item != null)
                {
                    int lineCount = QuickCalculateLineCount(item.Name, maxWidth);
                    double height = singleLineHeight + (lineCount - 1) * additionalHeightPerLine;
                    lock (newItemSizes)
                    {
                        newItemSizes[i] = new Size(maxWidth, height);
                    }
                }
            });

            //itemSizes = newItemSizes;
            await Dispatcher.InvokeAsync(() =>
            {
                itemSizes = newItemSizes;
                InvalidateMeasure();
            }, DispatcherPriority.Background);
            //InvalidateArrange();
        });
    }

    //private async Task<Task> MeasureItemsAsync()
    //{
    //    if (Items == null || Items.Count == 0)
    //        return Task.CompletedTask;

    //    double maxWidth = 110;
    //    var newItemSizes = new ConcurrentDictionary<int, Size>();

    //    return Task.Run(async () =>
    //    {
    //        for (int i = 0; i < Items.Count; i++)
    //        {
    //            var item = Items[i] as FileSystemObject;
    //            if (item != null)
    //            {
    //                await Dispatcher.InvokeAsync(() =>
    //                {
    //                    var container = ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
    //                    if (container != null)
    //                    {
    //                        container.Measure(new Size(maxWidth, double.PositiveInfinity));
    //                        newItemSizes[i] = container.DesiredSize;
    //                    }
    //                    else
    //                    {

    //                    }
    //                });
    //            }
    //        }

    //        await Dispatcher.InvokeAsync(() =>
    //        {
    //            itemSizes = newItemSizes;
    //            InvalidateMeasure();
    //        }, DispatcherPriority.Background);
    //    });
    //}

    //private async Task MeasureItemsAsync()
    //{
    //    if (Items == null || Items.Count == 0)
    //        return;

    //    double maxWidth = 110;
    //    var newItemSizes = new ConcurrentDictionary<int, Size>();
    //    //for (int i = 0; i < Items.Count; i++)
    //    //{
    //    //    newItemSizes[i] = Size.Empty;
    //    //}
    //    await Task.Run(() =>
    //    {
    //        Parallel.For(0, Items.Count, i =>
    //        {
    //            var item = Items[i] as FileSystemObject;
    //            if (item is DriveObject)
    //            {
    //                maxWidth = 290;
    //            }
    //            if (item != null)
    //            {
    //                var size = MeasureItemWithTemplate(item.Name, maxWidth);
    //                lock (newItemSizes)
    //                {
    //                    newItemSizes[i] = size;
    //                }
    //            }
    //        });
    //    });

    //    await Dispatcher.InvokeAsync(() =>
    //    {
    //        itemSizes = newItemSizes;
    //        InvalidateMeasure();
    //    }, DispatcherPriority.Background);
    //}


    private Size MeasureItemWithTemplate(string text, double maxWidth)
    {
        Size result = new Size();
        Dispatcher.Invoke(() =>
        {
            var template = (DataTemplate)FindResource("FileTemplate");
            var container = new ContentPresenter
            {
                Content = new FileSystemObject { Name = text },
                ContentTemplate = template,
                Width = maxWidth
            };

            container.Measure(new Size(maxWidth, double.PositiveInfinity));
            result.Height = container.DesiredSize.Height + 4;
            result.Width = container.DesiredSize.Width;
        });
        return result;
    }


    private int QuickCalculateLineCount(string text, double maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        // Приблизительный расчет без использования FormattedText
        int approximateCharactersPerLine = (int)(maxWidth / 7); // Предполагаем среднюю ширину символа 7 пикселей
        return Math.Max(1, (int)Math.Ceiling((double)text.Length / approximateCharactersPerLine));
    }

    private void AnimateElement(UIElement element)
    {
        element.Opacity = 0;
        element.RenderTransform = new TranslateTransform(0, 20);

        var opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        var translateAnimation = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300));
        translateAnimation.EasingFunction = new QuadraticEase();

        element.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        ((TranslateTransform)element.RenderTransform).BeginAnimation(TranslateTransform.YProperty, translateAnimation);
    }

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register("ZoomFactor", typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double ZoomFactor
    {
        get { return (double)GetValue(ZoomFactorProperty); }
        set { SetValue(ZoomFactorProperty, value); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        //if (MeasureItemsTask.IsCompleted)
        //{
        //    MeasureItemsTask = MeasureItemsAsync();
        //}
        //if (MeasureItemsTask != null && !MeasureItemsTask.IsCompleted)
        //{
        //    //MeasureItemsTask.Wait();
        //    return base.MeasureOverride(availableSize);
        //}
        //Console.WriteLine($"MeasureOverride called. Available size: {availableSize}");
        UpdateChildSize(availableSize);
        if (ShouldIgnoreMeasure())
        {
            return availableSize;
        }

        Size size2;
        if (base.ItemsOwner is IHierarchicalVirtualizationAndScrollInfo hierarchicalVirtualizationAndScrollInfo)
        {
            Size size = hierarchicalVirtualizationAndScrollInfo.Constraints.Viewport.Size;
            Size pixelSize = hierarchicalVirtualizationAndScrollInfo.HeaderDesiredSizes.PixelSize;
            double width = Math.Max(size.Width - 5.0, 0.0);
            double height = Math.Max(size.Height - pixelSize.Height, 0.0);
            availableSize = new Size(width, height);
            Size extent = CalculateExtent(availableSize);
            size2 = new Size(extent.Width, extent.Height);
            base.Extent = extent;
            base.ScrollOffset = hierarchicalVirtualizationAndScrollInfo.Constraints.Viewport.Location;
            base.ViewportSize = hierarchicalVirtualizationAndScrollInfo.Constraints.Viewport.Size;
            cacheLength = hierarchicalVirtualizationAndScrollInfo.Constraints.CacheLength;
            cacheLengthUnit = hierarchicalVirtualizationAndScrollInfo.Constraints.CacheLengthUnit;
        }
        else
        {
            Size extent = CalculateExtent(availableSize);
            double width2 = Math.Min(availableSize.Width, extent.Width);
            double height2 = Math.Min(availableSize.Height, extent.Height);
            size2 = new Size(width2, height2);
            UpdateScrollInfo(size2, extent);
            cacheLength = VirtualizingPanel.GetCacheLength(base.ItemsOwner);
            cacheLengthUnit = VirtualizingPanel.GetCacheLengthUnit(base.ItemsOwner);
        }
        // zoom
        //childSize = new Size(childSize.Width * ZoomFactor, childSize.Height * ZoomFactor);
        //RecalculateItemsPerRow(availableSize);
        //
        ItemRange = UpdateItemRange();
        //Console.WriteLine($"Updated ItemRange: Start={ItemRange.StartIndex}, End={ItemRange.EndIndex}");
        RealizeItems();
        VirtualizeItems();
        return size2;
    }

    public void UpdateZoom(double newZoomFactor)
    {
        var a = itemSizes;
        //ZoomFactor = newZoomFactor;
        //InvalidateMeasure();
        //InvalidateArrange();
    }

    private void RecalculateItemsPerRow(Size availableSize)
    {
        if (double.IsInfinity(GetWidth(availableSize)))
        {
            itemsPerRowCount = base.Items.Count;
        }
        else
        {
            itemsPerRowCount = Math.Max(1, (int)Math.Floor(GetWidth(availableSize) / GetWidth(childSize)));
        }
        rowCount = (int)Math.Ceiling((double)base.Items.Count / (double)itemsPerRowCount);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        //if (MeasureItemsTask != null && !MeasureItemsTask.IsCompleted)
        //{
        //    //MeasureItemsTask.Wait();
        //    return base.ArrangeOverride(finalSize);
        //}
        //Console.WriteLine($"ArrangeOverride called. Final size: {finalSize}");
        double num = 0.0;
        Size size = CalculateChildArrangeSize(finalSize);
        CalculateSpacing(finalSize, out var innerSpacing, out var outerSpacing);

        // Calculate the maximum height for each row
        Dictionary<int, double> rowMaxHeights = new Dictionary<int, double>();

        for (int i = 0; i < base.InternalChildren.Count; i++)
        {
            UIElement uIElement = base.InternalChildren[i];
            if (uIElement != expandedItemChild)
            {
                int itemIndexFromChildIndex = GetItemIndexFromChildIndex(i);
                int rowIndex = itemIndexFromChildIndex / itemsPerRowCount;
                double elementHeight = uIElement.DesiredSize.Height;

                if (!rowMaxHeights.ContainsKey(rowIndex) || elementHeight > rowMaxHeights[rowIndex])
                {
                    rowMaxHeights[rowIndex] = elementHeight;
                }
            }

        }

        for (int i = 0; i < base.InternalChildren.Count; i++)
        {
            UIElement uIElement = base.InternalChildren[i];
            if (uIElement == expandedItemChild)
            {
                // Existing code for expanded item
                int num2 = ExpandedItemIndex / itemsPerRowCount + 1;
                double num3 = outerSpacing;
                double num4 = (double)num2 * GetHeight(size);
                double num5 = GetWidth(finalSize) - 2.0 * outerSpacing;
                double height = GetHeight(expandedItemChild.DesiredSize);
                if (SpacingMode == SpacingMode.None)
                {
                    num5 = (double)itemsPerRowCount * GetWidth(size);
                }

                if (Orientation == Orientation.Horizontal)
                {
                    expandedItemChild.Arrange(CreateRect(num3 - GetX(base.ScrollOffset), num4 - GetY(base.ScrollOffset), num5, height));
                }
                else
                {
                    expandedItemChild.Arrange(CreateRect(num3 - GetX(base.ScrollOffset), num4 - GetY(base.ScrollOffset), height, num5));
                }

                num = height;
            }
            else
            {
                // Modified code for dynamic sizing of other elements
                int itemIndexFromChildIndex = GetItemIndexFromChildIndex(i);
                int num6 = itemIndexFromChildIndex % itemsPerRowCount;
                int num7 = itemIndexFromChildIndex / itemsPerRowCount;
                double num8 = outerSpacing + (double)num6 * (GetWidth(size) + innerSpacing);
                double num9 = 0;

                // Calculate the Y position based on the maximum heights of previous rows
                for (int j = 0; j < num7; j++)
                {
                    num9 += rowMaxHeights.ContainsKey(j) ? rowMaxHeights[j] : GetHeight(size); // старый метод
                    //num9 += rowMaxHeights.ContainsKey(j) ? rowMaxHeights[j] * ZoomFactor : GetHeight(size) * ZoomFactor; // zoom
                    //num9 += rowMaxHeights.ContainsKey(j) ? rowMaxHeights[j] + 10 : GetHeight(size) + 10; // исчезают элементы сверх из-за 10 (расстояние вертикальное).
                }
                num9 += num;

                // Use the element's desired size for width and height
                double elementWidth = Math.Min(uIElement.DesiredSize.Width, GetWidth(size));
                double elementHeight = uIElement.DesiredSize.Height;
                //Console.WriteLine($"Arranging item {i}: Position=({num8}, {num9}), Size=({elementWidth}, {elementHeight})");
                uIElement.Arrange(CreateRect(num8 - GetX(base.ScrollOffset), num9 - GetY(base.ScrollOffset), elementWidth, elementHeight));



                // zoom
                //uIElement.RenderTransform = new ScaleTransform(ZoomFactor, ZoomFactor);
                //

                if (!animatedItems.Contains(itemIndexFromChildIndex))
                {
                    AnimateElement(uIElement);
                    animatedItems.Add(itemIndexFromChildIndex);
                }

            }

        }

        //_ = LoadIconsForVisibleItems();
        Dispatcher.BeginInvoke(new Action(() => LoadIconsForVisibleItems()), DispatcherPriority.Background);
        return finalSize;
    }

    protected override void BringIndexIntoView(int index)
    {
        double num = (double)(index / itemsPerRowCount) * GetHeight(childSize);
        if (expandedItemChild != null && index > itemIndexFollwingExpansion)
        {
            num += GetHeight(expandedItemChild.DesiredSize);
        }

        if (Orientation == Orientation.Horizontal)
        {
            SetHorizontalOffset(num);
        }
        else
        {
            SetVerticalOffset(num);
        }
    }

    private void Orientation_Changed()
    {
        base.MouseWheelScrollDirection = ((Orientation != 0) ? ScrollDirection.Horizontal : ScrollDirection.Vertical);
    }

    private void ExpandedItemPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        if (args.OldValue != null)
        {
            int num = base.InternalChildren.IndexOf(expandedItemChild);
            if (num != -1)
            {
                expandedItemChild = null;
                RemoveInternalChildRange(num, 1);
            }
        }
    }

    private int GetItemIndexFromChildIndex(int childIndex)
    {
        GeneratorPosition generatorPositionFromChildIndex = GetGeneratorPositionFromChildIndex(childIndex);
        return base.RecyclingItemContainerGenerator.IndexFromGeneratorPosition(generatorPositionFromChildIndex);
    }

    private GeneratorPosition GetGeneratorPositionFromChildIndex(int childIndex)
    {
        int num = base.InternalChildren.IndexOf(expandedItemChild);
        if (num != -1 && childIndex > num)
        {
            return new GeneratorPosition(childIndex - 1, 0);
        }

        return new GeneratorPosition(childIndex, 0);
    }

    //private void UpdateScrollInfo(Size availableSize, Size extent)
    //{
    //    //Console.WriteLine($"UpdateScrollInfo called. Available size: {availableSize}, Extent: {extent}");
    //    bool flag = false;
    //    if (extent != base.Extent)
    //    {
    //        base.Extent = extent;
    //        flag = true;
    //    }

    //    if (availableSize != base.ViewportSize)
    //    {
    //        base.ViewportSize = availableSize;
    //        flag = true;
    //    }

    //    if (base.ViewportHeight != 0.0 && base.VerticalOffset != 0.0 && base.VerticalOffset + base.ViewportHeight + 1.0 >= base.ExtentHeight)
    //    {
    //        base.ScrollOffset = new Point(base.ScrollOffset.X, extent.Height - availableSize.Height);
    //        flag = true;
    //    }

    //    if (base.ViewportWidth != 0.0 && base.HorizontalOffset != 0.0 && base.HorizontalOffset + base.ViewportWidth + 1.0 >= base.ExtentWidth)
    //    {
    //        base.ScrollOffset = new Point(extent.Width - availableSize.Width, base.ScrollOffset.Y);
    //        flag = true;
    //    }

    //    if (flag)
    //    {
    //        //Console.WriteLine($"ScrollInfo updated. New offset: {base.ScrollOffset}, ViewportSize: {base.ViewportSize}, Extent: {base.Extent}");
    //        base.ScrollOwner?.InvalidateScrollInfo();
    //    }
    //}

    private void RealizeItems()
    {
        GeneratorPosition position = base.RecyclingItemContainerGenerator.GeneratorPositionFromIndex(ItemRange.StartIndex);
        int num = ((position.Offset == 0) ? position.Index : (position.Index + 1));
        int val = ((ExpandedItemIndex != -1) ? ((ExpandedItemIndex / itemsPerRowCount + 1) * itemsPerRowCount - 1) : (-1));
        val = Math.Min(val, base.Items.Count - 1);
        if (val != itemIndexFollwingExpansion && expandedItemChild != null)
        {
            RemoveInternalChildRange(base.InternalChildren.IndexOf(expandedItemChild), 1);
            expandedItemChild = null;
        }

        using (base.RecyclingItemContainerGenerator.StartAt(position, GeneratorDirection.Forward, allowStartAtRealizedItem: true))
        {
            int num2 = ItemRange.StartIndex;
            while (num2 <= ItemRange.EndIndex)
            {
                bool isNewlyRealized;
                UIElement uIElement = (UIElement)base.RecyclingItemContainerGenerator.GenerateNext(out isNewlyRealized);
                if (isNewlyRealized || !base.InternalChildren.Contains(uIElement))
                {
                    if (num >= base.InternalChildren.Count)
                    {
                        AddInternalChild(uIElement);
                    }
                    else
                    {
                        InsertInternalChild(num, uIElement);
                    }

                    base.RecyclingItemContainerGenerator.PrepareItemContainer(uIElement);
                    if (ItemSize == Size.Empty)
                    {
                        uIElement.Measure(CreateSize(GetWidth(base.ViewportSize), double.MaxValue));
                    }
                    else
                    {
                        uIElement.Measure(ItemSize);
                    }
                }

                if (num2 == val && ExpandedItemTemplate != null)
                {
                    if (expandedItemChild == null)
                    {
                        expandedItemChild = (FrameworkElement)ExpandedItemTemplate.LoadContent();
                        expandedItemChild.DataContext = base.Items[ExpandedItemIndex];
                        expandedItemChild.Measure(CreateSize(GetWidth(base.ViewportSize), double.MaxValue));
                    }

                    if (!base.InternalChildren.Contains(expandedItemChild))
                    {
                        num++;
                        if (num >= base.InternalChildren.Count)
                        {
                            AddInternalChild(expandedItemChild);
                        }
                        else
                        {
                            InsertInternalChild(num, expandedItemChild);
                        }
                    }
                }

                num2++;
                num++;
            }

            itemIndexFollwingExpansion = val;
        }
    }

    private void VirtualizeItems()
    {
        for (int num = base.InternalChildren.Count - 1; num >= 0; num--)
        {
            FrameworkElement frameworkElement = (FrameworkElement)base.InternalChildren[num];
            if (frameworkElement == expandedItemChild)
            {
                if (!ItemRange.Contains(ExpandedItemIndex))
                {
                    expandedItemChild = null;
                    RemoveInternalChildRange(num, 1);
                }
            }
            else
            {
                int itemIndex = base.Items.IndexOf(frameworkElement.DataContext);
                GeneratorPosition position = base.RecyclingItemContainerGenerator.GeneratorPositionFromIndex(itemIndex);
                if (!ItemRange.Contains(itemIndex))
                {
                    if (base.IsRecycling)
                    {
                        base.RecyclingItemContainerGenerator.Recycle(position, 1);
                    }
                    else
                    {
                        base.RecyclingItemContainerGenerator.Remove(position, 1);
                    }

                    RemoveInternalChildRange(num, 1);
                }
            }
        }
    }

    private void UpdateChildSize(Size availableSize)
    {
        if (base.ItemsOwner is IHierarchicalVirtualizationAndScrollInfo hierarchicalVirtualizationAndScrollInfo && VirtualizingPanel.GetIsVirtualizingWhenGrouping(base.ItemsControl))
        {
            if (Orientation == Orientation.Vertical)
            {
                availableSize.Width = hierarchicalVirtualizationAndScrollInfo.Constraints.Viewport.Size.Width;
                availableSize.Width = Math.Max(availableSize.Width - (base.Margin.Left + base.Margin.Right), 0.0);
            }
            else
            {
                availableSize.Height = hierarchicalVirtualizationAndScrollInfo.Constraints.Viewport.Size.Height;
                availableSize.Height = Math.Max(availableSize.Height - (base.Margin.Top + base.Margin.Bottom), 0.0);
            }
        }

        //if (ItemSize != Size.Empty)
        //{
        //    childSize = ItemSize;
        //}
        if (itemSizes.Count > 0)
        {
            // Используем среднее значение размеров элементов
            double avgWidth = itemSizes.Values.Average(s => s.Width);
            double avgHeight = itemSizes.Values.Average(s => s.Height);
            childSize = new Size(avgWidth, avgHeight);
        }
        else if (base.InternalChildren.Count != 0)
        {
            // Используем медианные размеры из первых N элементов
            int sampleSize = Math.Min(10, base.InternalChildren.Count);
            List<double> widths = new List<double>();
            List<double> heights = new List<double>();

            for (int i = 0; i < sampleSize; i++)
            {
                var child = base.InternalChildren[i];
                widths.Add(child.DesiredSize.Width);
                heights.Add(child.DesiredSize.Height);
            }

            widths.Sort();
            heights.Sort();

            double medianWidth = widths[sampleSize / 2];
            double medianHeight = heights[sampleSize / 2];

            childSize = new Size(medianWidth, medianHeight);
            //childSize = CalculateAverageChildSize();
        }
        else
        {
            childSize = CalculateChildSize(availableSize);
        }

        if (double.IsInfinity(GetWidth(availableSize)))
        {
            itemsPerRowCount = base.Items.Count;
        }
        else
        {
            itemsPerRowCount = Math.Max(1, (int)Math.Floor(GetWidth(availableSize) / GetWidth(childSize)));
        }

        rowCount = (int)Math.Ceiling((double)base.Items.Count / (double)itemsPerRowCount);
    }
    private Size CalculateAverageChildSize()
    {
        Dictionary<int, List<Size>> rowSizes = new Dictionary<int, List<Size>>();

        for (int i = 0; i < base.InternalChildren.Count; i++)
        {
            UIElement child = base.InternalChildren[i];
            int rowIndex = i / itemsPerRowCount;

            if (!rowSizes.ContainsKey(rowIndex))
            {
                rowSizes[rowIndex] = new List<Size>();
            }

            rowSizes[rowIndex].Add(child.DesiredSize);
        }

        double avgWidth = rowSizes.Values.SelectMany(sizes => sizes).Average(s => s.Width);
        double avgHeight = rowSizes.Values.Select(sizes => sizes.Max(s => s.Height)).Average();

        return new Size(avgWidth, avgHeight);
    }
    private Size CalculateChildSize(Size availableSize)
    {
        if (base.Items.Count == 0)
        {
            return new Size(0.0, 0.0);
        }

        GeneratorPosition position = base.RecyclingItemContainerGenerator.GeneratorPositionFromIndex(0);
        using (base.RecyclingItemContainerGenerator.StartAt(position, GeneratorDirection.Forward, allowStartAtRealizedItem: true))
        {
            UIElement uIElement = (UIElement)base.RecyclingItemContainerGenerator.GenerateNext();
            AddInternalChild(uIElement);
            base.RecyclingItemContainerGenerator.PrepareItemContainer(uIElement);
            uIElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return uIElement.DesiredSize;
        }
    }

    //private Size CalculateExtent(Size availableSize)
    //{
    //    double num = ((SpacingMode != 0 && !double.IsInfinity(GetWidth(availableSize))) ? GetWidth(availableSize) : (GetWidth(childSize) * (double)itemsPerRowCount));
    //    if (base.ItemsOwner is IHierarchicalVirtualizationAndScrollInfo)
    //    {
    //        num = ((Orientation != Orientation.Vertical) ? Math.Max(num - (base.Margin.Top + base.Margin.Bottom), 0.0) : Math.Max(num - (base.Margin.Left + base.Margin.Right), 0.0));
    //    }

    //    double height = GetHeight(childSize) * (double)rowCount;
    //    Size result = CreateSize(num, height);
    //    if (expandedItemChild != null)
    //    {
    //        if (Orientation == Orientation.Horizontal)
    //        {
    //            result.Height += expandedItemChild.DesiredSize.Height;
    //        }
    //        else
    //        {
    //            result.Width += expandedItemChild.DesiredSize.Width;
    //        }
    //    }

    //    return result;
    //}
    //private Size CalculateExtent(Size availableSize)
    //{
    //    double totalWidth = 0;
    //    double totalHeight = 0;
    //    int itemsInCurrentRow = 0;
    //    double currentRowHeight = 0;

    //    for (int i = 0; i < base.Items.Count; i++)
    //    {
    //        if (itemSizes.TryGetValue(i, out Size itemSize))
    //        {
    //            if (totalWidth + itemSize.Width > GetWidth(availableSize) && itemsInCurrentRow > 0)
    //            {
    //                // Переход на новую строку
    //                totalHeight += currentRowHeight;
    //                totalWidth = itemSize.Width;
    //                itemsInCurrentRow = 1;
    //                currentRowHeight = itemSize.Height;
    //            }
    //            else
    //            {
    //                totalWidth += itemSize.Width;
    //                itemsInCurrentRow++;
    //                currentRowHeight = Math.Max(currentRowHeight, itemSize.Height);
    //            }
    //        }
    //    }

    //    // Добавляем высоту последней строки
    //    totalHeight += currentRowHeight;

    //    Size result = CreateSize(Math.Max(totalWidth, GetWidth(availableSize)), totalHeight);

    //    if (expandedItemChild != null)
    //    {
    //        if (Orientation == Orientation.Horizontal)
    //        {
    //            result.Height += expandedItemChild.DesiredSize.Height;
    //        }
    //        else
    //        {
    //            result.Width += expandedItemChild.DesiredSize.Width;
    //        }
    //    }

    //    return result;
    //}


    private void CalculateSpacing(Size finalSize, out double innerSpacing, out double outerSpacing)
    {
        Size size = CalculateChildArrangeSize(finalSize);
        double width = GetWidth(finalSize);
        double num = Math.Min(GetWidth(size) * (double)itemsPerRowCount, width);
        double num2 = width - num;
        switch (SpacingMode)
        {
            case SpacingMode.Uniform:
                innerSpacing = (outerSpacing = num2 / (double)(itemsPerRowCount + 1));
                break;
            case SpacingMode.BetweenItemsOnly:
                innerSpacing = num2 / (double)Math.Max(itemsPerRowCount - 1, 1);
                outerSpacing = 0.0;
                break;
            case SpacingMode.StartAndEndOnly:
                innerSpacing = 0.0;
                outerSpacing = num2 / 2.0;
                break;
            default:
                innerSpacing = 0.0;
                outerSpacing = 0.0;
                break;
        }
    }

    private Size CalculateChildArrangeSize(Size finalSize)
    {
        if (StretchItems)
        {
            if (Orientation == Orientation.Horizontal)
            {
                double val = ReadItemContainerStyle(FrameworkElement.MaxWidthProperty, double.PositiveInfinity);
                return new Size(Math.Min(finalSize.Width / (double)itemsPerRowCount, val), childSize.Height);
            }

            double val2 = ReadItemContainerStyle(FrameworkElement.MaxHeightProperty, double.PositiveInfinity);
            double height = Math.Min(finalSize.Height / (double)itemsPerRowCount, val2);
            return new Size(childSize.Width, height);
        }

        return childSize;
    }

    private T ReadItemContainerStyle<T>(DependencyProperty property, T fallbackValue) where T : notnull
    {
        DependencyProperty property2 = property;
        return (T)(base.ItemsControl.ItemContainerStyle?.Setters.OfType<Setter>().FirstOrDefault((Setter setter) => setter.Property == property2)?.Value ?? ((object)fallbackValue));
    }

    private ItemRangeStruct UpdateItemRange()
    {
        if (!base.IsVirtualizing)
        {
            return new ItemRangeStruct(0, base.Items.Count - 1);
        }

        int num5;
        int num6;
        if (base.ItemsOwner is IHierarchicalVirtualizationAndScrollInfo hierarchicalVirtualizationAndScrollInfo)
        {
            if (!VirtualizingPanel.GetIsVirtualizingWhenGrouping(base.ItemsControl))
            {
                return new ItemRangeStruct(0, base.Items.Count - 1);
            }

            Point point = new Point(base.ScrollOffset.X, hierarchicalVirtualizationAndScrollInfo.Constraints.Viewport.Location.Y);
            int num;
            double num2;
            if (base.ScrollUnit == ScrollUnit.Item)
            {
                num = ((GetY(point) >= 1.0) ? ((int)GetY(point) - 1) : 0);
                num2 = (double)num * GetHeight(childSize);
            }
            else
            {
                num2 = Math.Min(Math.Max(GetY(point) - GetHeight(hierarchicalVirtualizationAndScrollInfo.HeaderDesiredSizes.PixelSize), 0.0), GetHeight(base.Extent));
                num = GetRowIndex(num2);
            }

            double num3 = Math.Min(GetHeight(base.ViewportSize), Math.Max(GetHeight(base.Extent) - num2, 0.0));
            int num4 = (int)Math.Ceiling((num2 + num3) / GetHeight(childSize)) - (int)Math.Floor(num2 / GetHeight(childSize));
            num5 = num * itemsPerRowCount;
            num6 = Math.Min((num + num4) * itemsPerRowCount - 1, base.Items.Count - 1);
            if (cacheLengthUnit == VirtualizationCacheLengthUnit.Pixel)
            {
                double num7 = Math.Min(cacheLength.CacheBeforeViewport, num2);
                double num8 = Math.Min(cacheLength.CacheAfterViewport, GetHeight(base.Extent) - num3 - num2);
                int num9 = (int)(num7 / GetHeight(childSize));
                int num10 = (int)Math.Ceiling((num2 + num3 + num8) / GetHeight(childSize)) - (int)Math.Ceiling((num2 + num3) / GetHeight(childSize));
                num5 = Math.Max(num5 - num9 * itemsPerRowCount, 0);
                num6 = Math.Min(num6 + num10 * itemsPerRowCount, base.Items.Count - 1);
            }
            else if (cacheLengthUnit == VirtualizationCacheLengthUnit.Item)
            {
                num5 = Math.Max(num5 - (int)cacheLength.CacheBeforeViewport, 0);
                num6 = Math.Min(num6 + (int)cacheLength.CacheAfterViewport, base.Items.Count - 1);
            }
        }
        else
        {
            double num11 = GetY(base.ScrollOffset);
            double num12 = GetY(base.ScrollOffset) + GetHeight(base.ViewportSize);
            if (cacheLengthUnit == VirtualizationCacheLengthUnit.Pixel)
            {
                num11 = Math.Max(num11 - cacheLength.CacheBeforeViewport, 0.0);
                num12 = Math.Min(num12 + cacheLength.CacheAfterViewport, GetHeight(base.Extent));
            }

            num5 = GetRowIndex(num11) * itemsPerRowCount;
            num6 = Math.Min(GetRowIndex(num12) * itemsPerRowCount + (itemsPerRowCount - 1), base.Items.Count - 1);
            if (cacheLengthUnit == VirtualizationCacheLengthUnit.Page)
            {
                int num13 = num6 - num5 + 1;
                num5 = Math.Max(num5 - (int)cacheLength.CacheBeforeViewport * num13, 0);
                num6 = Math.Min(num6 + (int)cacheLength.CacheAfterViewport * num13, base.Items.Count - 1);
            }
            else if (cacheLengthUnit == VirtualizationCacheLengthUnit.Item)
            {
                num5 = Math.Max(num5 - (int)cacheLength.CacheBeforeViewport, 0);
                num6 = Math.Min(num6 + (int)cacheLength.CacheAfterViewport, base.Items.Count - 1);
            }
        }

        //var result = new ItemRangeStruct(num5, num6);
        //Console.WriteLine($"UpdateItemRange result: Start={result.StartIndex}, End={result.EndIndex}");
        //return result;
        return new ItemRangeStruct(num5, num6);
    }

    private int GetRowIndex(double location)
    {
        int val = (int)Math.Floor(location / GetHeight(childSize));
        //int val = (int)Math.Floor(location / GetHeight(childSize) * ZoomFactor);
        int val2 = (int)Math.Ceiling((double)base.Items.Count / (double)itemsPerRowCount);
        //int result = Math.Max(Math.Min(val, val2), 0);
        //Console.WriteLine($"GetRowIndex: location={location}, result={result}");
        //return result;
        return Math.Max(Math.Min(val, val2), 0);
    }
    //private int GetRowIndex(double location)
    //{
    //    double currentHeight = 0;
    //    int rowIndex = 0;
    //    double rowHeight = 0;

    //    for (int i = 0; i < base.Items.Count; i++)
    //    {
    //        if (itemSizes.TryGetValue(i, out Size itemSize))
    //        {
    //            if (currentHeight + itemSize.Height > location)
    //            {
    //                return rowIndex;
    //            }

    //            rowHeight = Math.Max(rowHeight, itemSize.Height);

    //            if ((i + 1) % itemsPerRowCount == 0)
    //            {
    //                currentHeight += rowHeight;
    //                rowIndex++;
    //                rowHeight = 0;
    //            }
    //        }
    //    }

    //    return rowIndex;
    //}


    //protected override double GetLineUpScrollAmount()
    //{
    //    return 0.0 - Math.Min(childSize.Height * (double)base.ScrollLineDeltaItem, base.ViewportSize.Height);
    //}

    //protected override double GetLineDownScrollAmount()
    //{
    //    return Math.Min(childSize.Height * (double)base.ScrollLineDeltaItem, base.ViewportSize.Height);
    //}
    protected override double GetLineUpScrollAmount()
    {
        return -Math.Min(itemSizes.Values.Average(s => s.Height), base.ViewportSize.Height);
    }

    protected override double GetLineDownScrollAmount()
    {
        return Math.Min(itemSizes.Values.Average(s => s.Height), base.ViewportSize.Height);
    }


    protected override double GetLineLeftScrollAmount()
    {
        return 0.0 - Math.Min(childSize.Width * (double)base.ScrollLineDeltaItem, base.ViewportSize.Width);
    }

    protected override double GetLineRightScrollAmount()
    {
        return Math.Min(childSize.Width * (double)base.ScrollLineDeltaItem, base.ViewportSize.Width);
    }

    protected override double GetMouseWheelUpScrollAmount()
    {
        return 0.0 - Math.Min(childSize.Height * (double)base.MouseWheelDeltaItem, base.ViewportSize.Height);
    }

    protected override double GetMouseWheelDownScrollAmount()
    {
        return Math.Min(childSize.Height * (double)base.MouseWheelDeltaItem, base.ViewportSize.Height);
    }
    //protected override double GetMouseWheelUpScrollAmount()
    //{
    //    return -Math.Min(itemSizes.Values.Average(s => s.Height) * base.MouseWheelDeltaItem, base.ViewportSize.Height);
    //}

    //protected override double GetMouseWheelDownScrollAmount()
    //{
    //    return Math.Min(itemSizes.Values.Average(s => s.Height) * base.MouseWheelDeltaItem, base.ViewportSize.Height);
    //}

    protected override double GetMouseWheelLeftScrollAmount()
    {
        return 0.0 - Math.Min(childSize.Width * (double)base.MouseWheelDeltaItem, base.ViewportSize.Width);
    }

    protected override double GetMouseWheelRightScrollAmount()
    {
        return Math.Min(childSize.Width * (double)base.MouseWheelDeltaItem, base.ViewportSize.Width);
    }

    protected override double GetPageUpScrollAmount()
    {
        return 0.0 - base.ViewportSize.Height;
    }

    protected override double GetPageDownScrollAmount()
    {
        return base.ViewportSize.Height;
    }

    protected override double GetPageLeftScrollAmount()
    {
        return 0.0 - base.ViewportSize.Width;
    }

    protected override double GetPageRightScrollAmount()
    {
        return base.ViewportSize.Width;
    }

    private double GetX(Point point)
    {
        if (Orientation != 0)
        {
            return point.Y;
        }

        return point.X;
    }

    private double GetY(Point point)
    {
        if (Orientation != 0)
        {
            return point.X;
        }

        return point.Y;
    }

    private double GetWidth(Size size)
    {
        if (Orientation != 0)
        {
            return size.Height;
        }

        return size.Width;
    }

    private double GetHeight(Size size)
    {
        if (Orientation != 0)
        {
            return size.Width;
        }

        return size.Height;
    }

    private Size CreateSize(double width, double height)
    {
        if (Orientation != 0)
        {
            return new Size(height, width);
        }

        return new Size(width, height);
    }

    private Rect CreateRect(double x, double y, double width, double height)
    {
        if (Orientation != 0)
        {
            return new Rect(y, x, width, height);
        }

        return new Rect(x, y, width, height);
    }

    public static explicit operator VirtualizingWrapPanel(ItemsPanelTemplate v)
    {
        throw new NotImplementedException();
    }

    #region Загрузка иконок
    private async Task LoadIconsForVisibleItems()
    {
        cts.Cancel();
        cts = new CancellationTokenSource();
        var token = cts.Token;
        try
        {
            var tasks = new ConcurrentBag<Task<(int Index, FileSystemObject Item)>>();
            var itemsSnapshot = new List<FileSystemObject>(Items.Cast<FileSystemObject>());

            await Task.Run(() =>
            {
                try
                {
                    Parallel.For(ItemRange.StartIndex, Math.Min(ItemRange.EndIndex + 1, itemsSnapshot.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cts.Token },
                (i, loopState) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        loopState.Stop();
                        return;
                    }
                    var item = itemsSnapshot[i];
                    if (item != null && item.Icon == null && item is FileObject)// && File.Exists(item.Path))
                    {
                        tasks.Add(LoadIconForItem(item, i));
                    }
                    else if (item is FolderObject folderItem && folderItem.Icon == null)
                    {
                        tasks.Add(LoadIconsForFolder(folderItem, i));
                    }
                });
                }
                catch (Exception)
                {

                }
            }, token);

            var results = await Task.WhenAll(tasks);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var (index, item) in results)
                {
                    if (index >= 0 && index < Items.Count && Items[index] == item)
                    {
                        UpdateUIForItem(index, item);
                    }
                }
            });
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<(int Index, FileSystemObject Item)> LoadIconForItem(FileSystemObject item, int index)
    {
        try
        {
            if (item.Icon == null)
            {
                //var icon = await iconCache.GetIcon(item.Path);
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

                if (!cts.Token.IsCancellationRequested)
                {
                    if (icon != null)
                    {
                        if (icon.CanFreeze)
                        {
                            icon.Freeze();
                        }
                    }
                    item.Icon = icon;
                }
            }
        }
        catch
        {
        }
        return (index, item);
    }

    private async Task<(int Index, FileSystemObject Item)> LoadIconsForFolder(FolderObject folderItem, int index)
    {

        try
        {
            if (folderItem.Icon == null && folderItem.StandartFolderIcon1 == null)
            {
                if (!folderItem.Path.StartsWith("dropbox://"))
                {
                    var subdirectoriesTask = Task.Run(() => Directory.GetDirectories(folderItem.Path));
                    var filesInDirectoryTask = Task.Run(() => Directory.GetFiles(folderItem.Path));

                    await Task.WhenAll(subdirectoriesTask, filesInDirectoryTask);

                    var subdirectories = subdirectoriesTask.Result;
                    var filesInDirectory = filesInDirectoryTask.Result;

                    if (!cts.Token.IsCancellationRequested)
                    {
                        //folderItem.Icon = filesInDirectory.Length == 0 && subdirectories.Length > 0 ? folderInsideFolderIcon : standardFolderIcon;

                        if (filesInDirectory.Length == 0 && subdirectories.Length > 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                folderItem.Icon = MainWindow.ChangeImageColor(MainWindow.folderInsideFolderIcon);
                            });
                        }
                        else
                        {
                            //folderItem.StandartFolderIcon1 = standardFolderIcon1;
                            //folderItem.StandartFolderIcon2 = standardFolderIcon2;

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                folderItem.StandartFolderIcon1 = MainWindow.ChangeImageColor(MainWindow.standardFolderIcon1);
                                folderItem.StandartFolderIcon2 = MainWindow.ChangeImageColor(MainWindow.standardFolderIcon2);
                            });
                        }

                        if (filesInDirectory.Length > 0)
                        {
                            if (folderItem.FileIcon1 == null)
                            {
                                folderItem.FileIcon1 = await LoadAndFreezeIcon(filesInDirectory[0]);
                            }
                            if (folderItem.FileIcon2 == null)
                            {
                                if (filesInDirectory.Length > 1)
                                {
                                    folderItem.FileIcon2 = await LoadAndFreezeIcon(filesInDirectory[1]);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Обработка папки Dropbox
                    string dropboxPath = folderItem.Path.Substring("dropbox://".Length);
                    var entries = await MainWindow.Instance._dropboxService.ListFilesAsync(dropboxPath);

                    var subdirectories = entries.OfType<FolderMetadata>().ToList();
                    var filesInDirectory = entries.OfType<FileMetadata>().ToList();

                    if (!cts.Token.IsCancellationRequested)
                    {
                        if (filesInDirectory.Count == 0 && subdirectories.Count > 0)
                        {
                            folderItem.Icon = MainWindow.folderInsideFolderIcon;
                        }
                        else
                        {
                            folderItem.StandartFolderIcon1 = MainWindow.standardFolderIcon1;
                            folderItem.StandartFolderIcon2 = MainWindow.standardFolderIcon2;
                        }

                        if (filesInDirectory.Count > 0)
                        {
                            if (folderItem.FileIcon1 == null)
                            {
                                folderItem.FileIcon1 = await LoadAndFreezeIcon(filesInDirectory[0].PathLower);
                            }
                            if (folderItem.FileIcon2 == null && filesInDirectory.Count > 1)
                            {
                                folderItem.FileIcon2 = await LoadAndFreezeIcon(filesInDirectory[1].PathLower);
                            }
                        }
                    }
                }

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in LoadIconsForFolder: {ex.Message}");
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

                // Создаем временный файл с нужным расширением
                string tempFilePath = Path.Combine(Path.GetTempPath(), $"temp{extension}");
                File.Create(tempFilePath).Close();

                try
                {
                    // Получаем системную иконку для данного расширения
                    return GetSystemIcon(tempFilePath);
                }
                finally
                {
                    // Удаляем временный файл
                    File.Delete(tempFilePath);
                }
            }

            // Если это папка, возвращаем стандартную иконку папки
            return GetSystemIcon(Environment.GetFolderPath(Environment.SpecialFolder.System));
        }
        catch (Exception ex)
        {
            // Логирование ошибки
            Console.WriteLine($"Ошибка при получении иконки для {dropboxPath}: {ex.Message}");

            // Возвращаем стандартную иконку файла в случае ошибки
            return GetSystemIcon(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"));
        }
    }

    private async Task<BitmapSource> LoadAndFreezeIcon(string path)
    {
        //var icon = await iconCache.GetIcon2(path);
        BitmapSource icon;
        if (path.StartsWith("dropbox://") && !File.Exists(path))
        {
            string dropboxPath = path.Substring("dropbox://".Length);
            icon = await GetDropboxFileIcon(dropboxPath);
        }
        else
        {
            icon = await iconCache.GetIcon2(path);
        }
        if (icon != null)
        {
            if (icon.CanFreeze)
            {
                icon.Freeze();
            }
        }
        return icon;
    }

    private void UpdateUIForItem(int index, FileSystemObject updatedItem)
    {
        var container = ItemsControl.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
        if (container != null)
        {
            container.DataContext = updatedItem;
        }
    }
    #endregion
}
