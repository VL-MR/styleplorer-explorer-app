using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Styleplorer
{
    public static class FolderColorHelper
    {
        // Свойство зависимости для цвета папки
        public static readonly DependencyProperty FolderColorProperty =
            DependencyProperty.RegisterAttached(
                "FolderColor", // Имя свойства
                typeof(Color), // Тип свойства
                typeof(FolderColorHelper), // Владелец свойства
                new PropertyMetadata(Colors.Transparent, OnFolderColorChanged)); // Метаданные свойства, включая обработчик изменений

        // Метод для установки цвета папки
        public static void SetFolderColor(DependencyObject element, Color value)
        {
            element.SetValue(FolderColorProperty, value);
        }

        // Метод для получения цвета папки
        public static Color GetFolderColor(DependencyObject element)
        {
            return (Color)element.GetValue(FolderColorProperty);
        }

        // Обработчик изменения цвета папки
        private static void OnFolderColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Проверяем, является ли объект элементом интерфейса и имеет ли он контекст данных типа FolderObject
            if (d is FrameworkElement element && element.DataContext is FolderObject folder)
            {
                UpdateFolderIcons(folder); // Обновляем иконки папки
            }
        }

        // Метод для обновления иконок папки в зависимости от цвета
        private static void UpdateFolderIcons(FolderObject folder)
        {
            // Если у папки есть иконка, меняем её цвет
            if (folder.Icon != null)
            {
                folder.Icon = MainWindow.ChangeImageColor(MainWindow.folderInsideFolderIcon);
            }
            else
            {
                // Если иконки нет, меняем цвет стандартных иконок папки (верхней и нижней части)
                folder.StandartFolderIcon1 = MainWindow.ChangeImageColor(MainWindow.standardFolderIcon1);
                folder.StandartFolderIcon2 = MainWindow.ChangeImageColor(MainWindow.standardFolderIcon2);
            }
        }
    }
}
