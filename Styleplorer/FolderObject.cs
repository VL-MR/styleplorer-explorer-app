using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Styleplorer
{
    public class FolderObject : FileSystemObject
    {
        private ImageSource _fileIcon1; // Иконка файла 1
        public ImageSource FileIcon1
        {
            get => _fileIcon1;
            set
            {
                // Если значение изменилось, обновляем и уведомляем об изменении свойства
                if (_fileIcon1 != value)
                {
                    _fileIcon1 = value;
                    OnPropertyChanged(nameof(FileIcon1));
                }
            }
        }

        private ImageSource _fileIcon2; // Иконка файла 2
        public ImageSource FileIcon2
        {
            get => _fileIcon2;
            set
            {
                // Если значение изменилось, обновляем и уведомляем об изменении свойства
                if (_fileIcon2 != value)
                {
                    _fileIcon2 = value;
                    OnPropertyChanged(nameof(FileIcon2));
                }
            }
        }

        private ImageSource _standartFolderIcon1; // Стандартная иконка папки (верхняя часть)
        public ImageSource StandartFolderIcon1
        {
            get => _standartFolderIcon1;
            set
            {
                // Если значение изменилось, обновляем и уведомляем об изменении свойства
                if (_standartFolderIcon1 != value)
                {
                    _standartFolderIcon1 = value;
                    OnPropertyChanged(nameof(StandartFolderIcon1));
                }
            }
        }

        private ImageSource _standartFolderIcon2; // Стандартная иконка папки (нижняя часть)
        public ImageSource StandartFolderIcon2
        {
            get => _standartFolderIcon2;
            set
            {
                // Если значение изменилось, обновляем и уведомляем об изменении свойства
                if (_standartFolderIcon2 != value)
                {
                    _standartFolderIcon2 = value;
                    OnPropertyChanged(nameof(StandartFolderIcon2));
                }
            }
        }
    }
}
