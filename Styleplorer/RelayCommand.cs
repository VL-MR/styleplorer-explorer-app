using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Styleplorer
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute; // Действие, которое будет выполнено
        private readonly Func<bool> _canExecute; // Функция, определяющая возможность выполнения действия

        // Конструктор класса RelayCommand
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute)); // Проверка на null и присвоение действия
            _canExecute = canExecute; // Присвоение функции возможности выполнения
        }

        // Метод, определяющий возможность выполнения команды
        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true; // Если функция _canExecute задана, возвращает её результат; иначе возвращает true

        // Метод, выполняющий команду
        public void Execute(object parameter) => _execute(); // Выполнение действия

        // Событие, вызываемое при изменении состояния выполнения команды
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; } // Добавление обработчика события
            remove { CommandManager.RequerySuggested -= value; } // Удаление обработчика события
        }
    }
}
