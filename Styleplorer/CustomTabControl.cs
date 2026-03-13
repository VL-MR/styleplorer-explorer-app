using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Styleplorer
{
    public class CustomTabControl : TabControl
    {
        // Конструктор класса CustomTabControl
        public CustomTabControl()
        {
            // Подписываемся на событие изменения размера элемента управления
            this.SizeChanged += CustomTabControl_SizeChanged;
        }

        // Переопределенный метод, вызываемый при изменении элементов
        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            UpdateTabWidths(); // Обновляем ширину вкладок
        }

        // Обработчик события изменения размера элемента управления
        private void CustomTabControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTabWidths(); // Обновляем ширину вкладок
        }

        // Метод для обновления ширины вкладок
        public void UpdateTabWidths()
        {
            if (Items.Count > 0)
            {
                double maxTabWidth = 300; // Максимальная ширина вкладки
                double availableWidth = ActualWidth; // Доступная ширина
                double totalDesiredWidth = 0; // Общая желаемая ширина всех вкладок

                // Рассчитываем общую желаемую ширину всех вкладок
                foreach (TabItem item in Items)
                {
                    if (item.Header.ToString() != "+")
                    {
                        double desiredWidth = CalculateDesiredWidth(item); // Вычисляем желаемую ширину вкладки
                        TabItemData itemData = item.Tag as TabItemData;
                        if (itemData != null)
                        {
                            itemData.DesiredWidth = desiredWidth; // Сохраняем желаемую ширину в данных элемента
                        }
                        item.Tag = itemData != null ? itemData : desiredWidth; // Сохраняем данные или ширину в теге элемента
                        totalDesiredWidth += desiredWidth; // Увеличиваем общую желаемую ширину
                    }
                }

                // Вычисляем коэффициент масштабирования
                double scaleFactor = (totalDesiredWidth > availableWidth) ? availableWidth / totalDesiredWidth : 1;

                // Применяем масштабирование к ширине вкладок
                foreach (TabItem item in Items)
                {
                    if (item.Header.ToString() != "+")
                    {
                        TabItemData itemData = item.Tag as TabItemData;
                        double desiredWidth = itemData != null ? itemData.DesiredWidth : (double)item.Tag;
                        double adjustedWidth = Math.Min(desiredWidth * scaleFactor, maxTabWidth); // Корректируем ширину вкладки

                        item.MinWidth = 50; // Минимальная ширина вкладки
                        item.MaxWidth = adjustedWidth; // Максимальная ширина вкладки
                        item.Width = adjustedWidth; // Устанавливаем ширину вкладки
                    }
                }
            }
        }

        // Метод для вычисления желаемой ширины вкладки
        private double CalculateDesiredWidth(TabItem item)
        {
            var textBlock = new TextBlock { Text = item.Header.ToString() };
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return textBlock.DesiredSize.Width + 50; // Возвращаем желаемую ширину с учетом отступов
        }
    }
}