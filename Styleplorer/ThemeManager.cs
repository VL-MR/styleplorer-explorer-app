using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Styleplorer
{
    public static class ThemeManager
    {
        private static ResourceDictionary _currentTheme; // Текущая тема оформления

        // Метод для применения новой темы
        public static void ApplyTheme(Uri themeUri)
        {
            // Создание нового словаря ресурсов на основе URI темы
            var newTheme = new ResourceDictionary { Source = themeUri };

            // Если текущая тема уже применена, удаляем её
            if (_currentTheme != null)
                Application.Current.Resources.MergedDictionaries.Remove(_currentTheme);

            // Добавляем новую тему к словарям ресурсов приложения
            Application.Current.Resources.MergedDictionaries.Add(newTheme);
            _currentTheme = newTheme;

            // Обновляем ресурсы для всех окон приложения
            foreach (Window window in Application.Current.Windows)
            {
                UpdateWindowResources(window);
            }
        }

        // Метод для обновления ресурсов окна
        private static void UpdateWindowResources(Window window)
        {
            // Перебор всех ключей в текущем словаре ресурсов темы
            foreach (var key in _currentTheme.Keys)
            {
                // Если ресурсы окна уже содержат ключ
                if (window.Resources.Contains(key))
                {
                    // Обновляем значение ресурса
                    window.Resources[key] = _currentTheme[key];
                }
                else
                {
                    // Добавляем новый ресурс
                    window.Resources.Add(key, _currentTheme[key]);
                }
            }
        }
    }
}
