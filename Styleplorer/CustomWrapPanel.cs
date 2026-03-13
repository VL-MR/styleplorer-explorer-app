using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using WpfToolkit.Controls;
namespace Styleplorer
{
    public class CustomWrapPanel : WrapPanel
    {
        protected override Size ArrangeOverride(Size finalSize)
        {
            double x = 0;
            double y = 0;
            double maxHeight = 0;
            double margin = 10;

            foreach (UIElement child in Children)
            {
                double windowWidth = Parent is FrameworkElement parent ? parent.ActualWidth : finalSize.Width;

                double horizontalMargin = Math.Max(1, Math.Min(10, windowWidth / 100));

                //Console.WriteLine("horizontalMargin = " + horizontalMargin + "\n");

                if (x + child.DesiredSize.Width + horizontalMargin > windowWidth)
                {
                    x = 0;
                    y += maxHeight + margin;
                    maxHeight = 0;
                }

                child.Arrange(new Rect(new Point(x, y), child.DesiredSize));

                x += child.DesiredSize.Width + horizontalMargin;
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);

            }

            return finalSize;
        }

        //protected override Size MeasureOverride(Size constraint)
        //{
        //    double x = 0;
        //    double y = 0;
        //    double maxHeight = 0;
        //    double margin = 10;

        //    foreach (UIElement child in Children)
        //    {
        //        child.Measure(constraint);

        //        if (x + child.DesiredSize.Width + margin > constraint.Width)
        //        {
        //            x = 0;
        //            y += maxHeight + margin;
        //            maxHeight = 0;
        //        }

        //        x += child.DesiredSize.Width + margin;
        //        maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
        //    }

        //    return new Size(constraint.Width, y + maxHeight);
        //}
    }
}