using System;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;

namespace Styleplorer
{
    public class AdaptiveColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем тип значения и адаптируем цвет
            if (value is SolidColorBrush brush)
            {
                Color color = brush.Color;
                double brightness = CalculateBrightness(color);
                int adjustment = (int)(parameter as double?).GetValueOrDefault(30);

                // Если цвет светлый, затемняем его
                if (brightness > 0.5)
                {
                    adjustment = -adjustment;
                }

                // Корректируем цвет
                byte r = (byte)Math.Clamp(color.R + adjustment, 0, 255);
                byte g = (byte)Math.Clamp(color.G + adjustment, 0, 255);
                byte b = (byte)Math.Clamp(color.B + adjustment, 0, 255);

                return new SolidColorBrush(Color.FromArgb(color.A, r, g, b));
            }
            return value; // Возвращаем исходное значение, если тип не совпадает
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private double CalculateBrightness(Color color)
        {
            // Вычисление яркости цвета
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
        }
    }
}