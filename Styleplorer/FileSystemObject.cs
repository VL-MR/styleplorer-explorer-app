using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Styleplorer
{
    public class FileSystemObject : INotifyPropertyChanged
    {
        public string? Name { get; set; } // Имя объекта файловой системы
        public string? Path { get; set; } // Путь к объекту файловой системы

        private long? _size; // Размер объекта файловой системы
        public long? Size
        {
            get { return _size; }
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnPropertyChanged("Size"); // Уведомление об изменении свойства Size
                    OnPropertyChanged("FormattedSize"); // Уведомление об изменении свойства FormattedSize
                }
            }
        }

        public string? Type { get; set; } // Тип объекта файловой системы (например, файл или папка)
        public DateTime? DateChanged { get; set; } // Дата последнего изменения объекта

        private ImageSource? _icon; // Иконка объекта
        public ImageSource? Icon
        {
            get { return _icon; }
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged("Icon"); // Уведомление об изменении свойства Icon
                }
            }
        }

        public bool IsFavorite { get; set; } // Флаг, указывающий, является ли объект избранным
        public string? Group { get; set; } // Группа, к которой относится объект

        private ImageSource? _groupImage; // Иконка группы объекта
        public ImageSource? GroupImage
        {
            get => _groupImage;
            set
            {
                if (_groupImage != value)
                {
                    _groupImage = value;
                    OnPropertyChanged(nameof(GroupImage)); // Уведомление об изменении свойства GroupImage
                }
            }
        }

        // Свойство для получения отформатированного размера объекта
        public string? FormattedSize
        {
            get
            {
                return FormatFileSize(Size);
            }
        }

        // Метод для форматирования размера файла в удобочитаемом виде
        private static string? FormatFileSize(long? sizeInBytes)
        {
            if (!sizeInBytes.HasValue)
                return null;

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = sizeInBytes.Value;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        // Событие, вызываемое при изменении свойства
        public event PropertyChangedEventHandler? PropertyChanged;

        // Метод для уведомления об изменении свойства
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

