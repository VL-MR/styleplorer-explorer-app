using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows;

namespace Styleplorer
{
    public class RectangleAdorner : Adorner
    {
        private Point? startPoint; // Начальная точка прямоугольника
        private Point? endPoint; // Конечная точка прямоугольника
        private Pen pen; // Перо для рисования границы прямоугольника
        private SolidColorBrush fillBrush; // Кисть для заполнения прямоугольника

        // Конструктор класса RectangleAdorner
        public RectangleAdorner(UIElement adornedElement, Point? startPoint) : base(adornedElement)
        {
            this.startPoint = startPoint;
            var baseColor = (Color)MainWindow.Instance.Resources["RectangleBaseColor"];

            // Создаем цвет границы (непрозрачный и темнее)
            var borderColor = DarkenColor(baseColor, 0.2);

            // Создаем цвет заполнения (полупрозрачный)
            var fillColor = Color.FromArgb(64, baseColor.R, baseColor.G, baseColor.B);

            // var borderColor = Color.FromArgb(255, 100, 180, 250); // Cтандартный цвет выделяемого прямоугольника в Windows
            pen = new Pen(new SolidColorBrush(borderColor), 1); // Brushes.DodgerBlue, 0.5);
            pen.DashStyle = DashStyles.Solid;
            fillBrush = new SolidColorBrush(fillColor);
        }

        // Метод для затемнения цвета
        private Color DarkenColor(Color color, double factor)
        {
            return Color.FromArgb(
                color.A,
                (byte)(color.R * (1 - factor)),
                (byte)(color.G * (1 - factor)),
                (byte)(color.B * (1 - factor))
            );
        }

        // Метод для обновления конечной точки и перерисовки
        public void Update(Point? endPoint)
        {
            this.endPoint = endPoint;
            this.InvalidateVisual();
        }

        // Переопределенный метод для отрисовки прямоугольника
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (this.startPoint.HasValue && this.endPoint.HasValue)
            {
                var rect = new Rect(this.startPoint.Value, this.endPoint.Value);
                // drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb(50, SystemColors.HighlightBrush.Color.R, SystemColors.HighlightBrush.Color.G, SystemColors.HighlightBrush.Color.B)), pen, rect); // Cтандартный цвет выделяемого прямоугольника в Windows
                drawingContext.DrawRectangle(fillBrush, pen, rect);
            }
        }
    }
}
