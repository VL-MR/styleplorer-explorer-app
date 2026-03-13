using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace Styleplorer
{
    public class FileFolderTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FileTemplate { get; set; }
        public DataTemplate FolderTemplate { get; set; }
        public DataTemplate DriveTemplate { get; set; }
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is FileObject)
                return FileTemplate;
            else if (item is FolderObject)
                return FolderTemplate;
            else if (item is DriveObject)
                return DriveTemplate;
            else
                return base.SelectTemplate(item, container);
        }
    }

}
