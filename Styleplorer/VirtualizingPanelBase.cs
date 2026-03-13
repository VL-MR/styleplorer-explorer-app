using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfToolkit.Controls;

namespace Styleplorer;

public abstract class VirtualizingPanelBase : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ScrollLineDeltaProperty = DependencyProperty.Register("ScrollLineDelta", typeof(double), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(16.0));

    public static readonly DependencyProperty MouseWheelDeltaProperty = DependencyProperty.Register("MouseWheelDelta", typeof(double), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(48.0));

    public static readonly DependencyProperty ScrollLineDeltaItemProperty = DependencyProperty.Register("ScrollLineDeltaItem", typeof(int), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(1));

    public static readonly DependencyProperty MouseWheelDeltaItemProperty = DependencyProperty.Register("MouseWheelDeltaItem", typeof(int), typeof(VirtualizingPanelBase), new FrameworkPropertyMetadata(3));

    private DependencyObject? _itemsOwner;

    private ItemContainerGenerator? _itemContainerGenerator;

    private Visibility previousVerticalScrollBarVisibility = Visibility.Collapsed;

    private Visibility previousHorizontalScrollBarVisibility = Visibility.Collapsed;

    public ScrollViewer? ScrollOwner { get; set; }

    public bool CanVerticallyScroll { get; set; }

    public bool CanHorizontallyScroll { get; set; }

    public double ScrollLineDelta
    {
        get
        {
            return (double)GetValue(ScrollLineDeltaProperty);
        }
        set
        {
            SetValue(ScrollLineDeltaProperty, value);
        }
    }

    public double MouseWheelDelta
    {
        get
        {
            return (double)GetValue(MouseWheelDeltaProperty);
        }
        set
        {
            SetValue(MouseWheelDeltaProperty, value);
        }
    }

    public int ScrollLineDeltaItem
    {
        get
        {
            return (int)GetValue(ScrollLineDeltaItemProperty);
        }
        set
        {
            SetValue(ScrollLineDeltaItemProperty, value);
        }
    }

    public int MouseWheelDeltaItem
    {
        get
        {
            return (int)GetValue(MouseWheelDeltaItemProperty);
        }
        set
        {
            SetValue(MouseWheelDeltaItemProperty, value);
        }
    }

    protected ScrollUnit ScrollUnit => VirtualizingPanel.GetScrollUnit(ItemsControl);

    protected ScrollDirection MouseWheelScrollDirection { get; set; }

    protected bool IsVirtualizing => VirtualizingPanel.GetIsVirtualizing(ItemsControl);

    protected VirtualizationMode VirtualizationMode => VirtualizingPanel.GetVirtualizationMode(ItemsControl);

    protected bool IsRecycling => VirtualizationMode == VirtualizationMode.Recycling;

    public ItemsControl ItemsControl => System.Windows.Controls.ItemsControl.GetItemsOwner(this);

    protected DependencyObject ItemsOwner
    {
        get
        {
            if (_itemsOwner == null)
            {
                MethodInfo method = typeof(ItemsControl).GetMethod("GetItemsOwnerInternal", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[1] { typeof(DependencyObject) }, null);
                _itemsOwner = (DependencyObject)method.Invoke(null, new object[1] { this });
            }

            return _itemsOwner;
        }
    }

    protected ReadOnlyCollection<object> Items => ItemContainerGenerator.Items;

    protected IRecyclingItemContainerGenerator RecyclingItemContainerGenerator => ItemContainerGenerator;

    protected new ItemContainerGenerator ItemContainerGenerator
    {
        get
        {
            if (_itemContainerGenerator == null)
            {
                _ = base.InternalChildren;
                _itemContainerGenerator = base.ItemContainerGenerator.GetItemContainerGeneratorForPanel(this);
            }

            return _itemContainerGenerator;
        }
    }

    public double ExtentWidth => Extent.Width;

    public double ExtentHeight => Extent.Height;

    public double HorizontalOffset => ScrollOffset.X;

    public double VerticalOffset => ScrollOffset.Y;

    public double ViewportWidth => ViewportSize.Width;

    public double ViewportHeight => ViewportSize.Height;

    protected Size Extent { get; set; } = new Size(0.0, 0.0);


    protected Size ViewportSize { get; set; } = new Size(0.0, 0.0);


    protected Point ScrollOffset { get; set; } = new Point(0.0, 0.0);


    protected bool ShouldIgnoreMeasure()
    {
        IScrollInfo scrollInfo = this;
        ScrollViewer scrollViewer = ScrollOwner;
        if (ItemsOwner is GroupItem reference && VisualTreeHelper.GetParent(reference) is IScrollInfo scrollInfo2)
        {
            ScrollViewer scrollOwner = scrollInfo2.ScrollOwner;
            if (scrollOwner != null)
            {
                scrollInfo = scrollInfo2;
                scrollViewer = scrollOwner;
            }
        }

        if (scrollViewer != null)
        {
            bool num = scrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Auto && scrollViewer.ComputedVerticalScrollBarVisibility != 0 && scrollViewer.ComputedVerticalScrollBarVisibility != previousVerticalScrollBarVisibility;
            bool flag = scrollViewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && scrollViewer.ComputedHorizontalScrollBarVisibility != 0 && scrollViewer.ComputedHorizontalScrollBarVisibility != previousHorizontalScrollBarVisibility;
            previousVerticalScrollBarVisibility = scrollViewer.ComputedVerticalScrollBarVisibility;
            previousHorizontalScrollBarVisibility = scrollViewer.ComputedHorizontalScrollBarVisibility;
            if ((num && scrollInfo.ExtentHeight > scrollInfo.ViewportHeight) || (flag && scrollInfo.ExtentWidth > scrollInfo.ViewportWidth))
            {
                return true;
            }
        }

        return false;
    }

    public virtual Rect MakeVisible(Visual visual, Rect rectangle)
    {
        Rect rect = visual.TransformToAncestor(this).TransformBounds(rectangle);
        double num = 0.0;
        double num2 = 0.0;
        double x = 0.0;
        double y = 0.0;
        double width = Math.Min(rectangle.Width, ViewportWidth);
        double height = Math.Min(rectangle.Height, ViewportHeight);
        if (rect.Left < 0.0)
        {
            num = rect.Left;
        }
        else if (rect.Right > ViewportWidth)
        {
            num = Math.Min(rect.Right - ViewportWidth, rect.Left);
            if (rectangle.Width > ViewportWidth)
            {
                x = rectangle.Width - ViewportWidth;
            }
        }

        if (rect.Top < 0.0)
        {
            num2 = rect.Top;
        }
        else if (rect.Bottom > ViewportHeight)
        {
            num2 = Math.Min(rect.Bottom - ViewportHeight, rect.Top);
            if (rectangle.Height > ViewportHeight)
            {
                y = rectangle.Height - ViewportHeight;
            }
        }

        SetHorizontalOffset(HorizontalOffset + num);
        SetVerticalOffset(VerticalOffset + num2);
        return new Rect(x, y, width, height);
    }

    public void SetVerticalOffset(double offset)
    {
        if (offset < 0.0 || ViewportSize.Height >= Extent.Height)
        {
            offset = 0.0;
        }
        else if (offset + ViewportSize.Height >= Extent.Height)
        {
            offset = Extent.Height - ViewportSize.Height;
        }

        if (offset != ScrollOffset.Y)
        {
            ScrollOffset = new Point(ScrollOffset.X, offset);
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }
    }

    public void SetHorizontalOffset(double offset)
    {
        if (offset < 0.0 || ViewportSize.Width >= Extent.Width)
        {
            offset = 0.0;
        }
        else if (offset + ViewportSize.Width >= Extent.Width)
        {
            offset = Extent.Width - ViewportSize.Width;
        }

        if (offset != ScrollOffset.X)
        {
            ScrollOffset = new Point(offset, ScrollOffset.Y);
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }
    }

    public void LineUp()
    {
        ScrollVertical((ScrollUnit == ScrollUnit.Pixel) ? (0.0 - ScrollLineDelta) : GetLineUpScrollAmount());
    }

    public void LineDown()
    {
        ScrollVertical((ScrollUnit == ScrollUnit.Pixel) ? ScrollLineDelta : GetLineDownScrollAmount());
    }

    public void LineLeft()
    {
        ScrollHorizontal((ScrollUnit == ScrollUnit.Pixel) ? (0.0 - ScrollLineDelta) : GetLineLeftScrollAmount());
    }

    public void LineRight()
    {
        ScrollHorizontal((ScrollUnit == ScrollUnit.Pixel) ? ScrollLineDelta : GetLineRightScrollAmount());
    }

    public void MouseWheelUp()
    {
        if (MouseWheelScrollDirection == ScrollDirection.Vertical)
        {
            ScrollVertical((ScrollUnit == ScrollUnit.Pixel) ? (0.0 - MouseWheelDelta) : GetMouseWheelUpScrollAmount());
        }
        else
        {
            MouseWheelLeft();
        }
    }

    public void MouseWheelDown()
    {
        if (MouseWheelScrollDirection == ScrollDirection.Vertical)
        {
            ScrollVertical((ScrollUnit == ScrollUnit.Pixel) ? MouseWheelDelta : GetMouseWheelDownScrollAmount());
        }
        else
        {
            MouseWheelRight();
        }
    }

    public void MouseWheelLeft()
    {
        ScrollHorizontal((ScrollUnit == ScrollUnit.Pixel) ? (0.0 - MouseWheelDelta) : GetMouseWheelLeftScrollAmount());
    }

    public void MouseWheelRight()
    {
        ScrollHorizontal((ScrollUnit == ScrollUnit.Pixel) ? MouseWheelDelta : GetMouseWheelRightScrollAmount());
    }

    public void PageUp()
    {
        ScrollVertical((ScrollUnit == ScrollUnit.Pixel) ? (0.0 - ViewportSize.Height) : GetPageUpScrollAmount());
    }

    public void PageDown()
    {
        ScrollVertical((ScrollUnit == ScrollUnit.Pixel) ? ViewportSize.Height : GetPageDownScrollAmount());
    }

    public void PageLeft()
    {
        ScrollHorizontal((ScrollUnit == ScrollUnit.Pixel) ? (0.0 - ViewportSize.Width) : GetPageLeftScrollAmount());
    }

    public void PageRight()
    {
        ScrollHorizontal((ScrollUnit == ScrollUnit.Pixel) ? ViewportSize.Width : GetPageRightScrollAmount());
    }

    protected abstract double GetLineUpScrollAmount();

    protected abstract double GetLineDownScrollAmount();

    protected abstract double GetLineLeftScrollAmount();

    protected abstract double GetLineRightScrollAmount();

    protected abstract double GetMouseWheelUpScrollAmount();

    protected abstract double GetMouseWheelDownScrollAmount();

    protected abstract double GetMouseWheelLeftScrollAmount();

    protected abstract double GetMouseWheelRightScrollAmount();

    protected abstract double GetPageUpScrollAmount();

    protected abstract double GetPageDownScrollAmount();

    protected abstract double GetPageLeftScrollAmount();

    protected abstract double GetPageRightScrollAmount();

    private void ScrollVertical(double amount)
    {
        SetVerticalOffset(ScrollOffset.Y + amount);
    }

    private void ScrollHorizontal(double amount)
    {
        SetHorizontalOffset(ScrollOffset.X + amount);
    }
}
