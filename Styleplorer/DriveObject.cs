using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Styleplorer
{
    public class DriveObject : FileSystemObject
    {
        private long _freeSpace; // Свободное место на диске
        public long FreeSpace
        {
            get => _freeSpace;
            internal set
            {
                if (_freeSpace != value)
                {
                    _freeSpace = value;
                    OnPropertyChanged(nameof(FreeSpace)); // Уведомление об изменении свойства FreeSpace
                    UpdateUsedSpacePercentage(); // Обновление процента использованного пространства
                }
            }
        }

        public string FileSystem { get; internal set; } // Файловая система диска
        public string VolumeLabel { get; internal set; } // Метка тома диска
        public string Model { get; internal set; } // Модель диска

        private double _usedSpacePercentage; // Процент использованного пространства
        public double UsedSpacePercentage
        {
            get => _usedSpacePercentage;
            private set
            {
                if (_usedSpacePercentage != value)
                {
                    _usedSpacePercentage = value;
                    OnPropertyChanged(nameof(UsedSpacePercentage)); // Уведомление об изменении свойства UsedSpacePercentage
                }
            }
        }

        // Свойство для получения формата свободного места
        public string FreeSpaceFormatted => $"{FormatBytes(FreeSpace)} свободно из {FormatBytes(Size ?? 0)}";

        // Событие, вызываемое при изменении свойства
        public event PropertyChangedEventHandler PropertyChanged;

        // Метод для уведомления об изменении свойства
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Метод для обновления процента использованного пространства
        public void UpdateUsedSpacePercentage()
        {
            UsedSpacePercentage = Size.HasValue && Size.Value > 0
                ? 100 - FreeSpace * 100.0 / Size.Value
                : 0;
        }

        // Метод для форматирования байтов в удобочитаемом виде
        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:N1} {suffixes[suffixIndex]}";
        }
    }
}
