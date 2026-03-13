using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Styleplorer
{
    public class NullToVisibilityConverter : IValueConverter
    {
        // Метод Convert преобразует значение из одного типа в другой.
        // Если значение равно null, возвращает Visibility.Collapsed; иначе возвращает Visibility.Visible.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        // Метод ConvertBack выполняет обратное преобразование. В данном случае он не реализован и выбрасывает исключение.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
