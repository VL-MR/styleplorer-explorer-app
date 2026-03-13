using System.Collections.Generic;

namespace Styleplorer
{
    public class NavigationManager
    {
        // Стек для хранения пути назад
        private Stack<string> backStack = new Stack<string>();
        // Стек для хранения пути вперёд
        private Stack<string> forwardStack = new Stack<string>();

        // Метод для перехода по заданному пути
        public void NavigateTo(string path, CustomListView listBox)
        {
            // Если в стеке назад есть элементы
            if (backStack.Count > 0)
            {
                // Если верхний элемент стека назад не совпадает с текущим путём
                if (backStack.Peek() != path)
                {
                    // Очистка стека вперёд
                    forwardStack.Clear();
                    // Добавление текущего пути в стек назад
                    backStack.Push(path);
                    // Установка текущего пути в listBox
                    listBox.CurrentPath = path;
                    // Обновление текущего пути в главном окне
                    MainWindow.UpdateCurrentPath(listBox);
                }
            }
            else
            {
                // Очистка обоих стеков
                forwardStack.Clear();
                backStack.Clear();
                // Добавление текущего пути в стек назад
                backStack.Push(path);
                // Установка текущего пути в listBox
                listBox.CurrentPath = path;
                // Обновление текущего пути в главном окне
                MainWindow.UpdateCurrentPath(listBox);
            }
        }

        // Метод для перехода назад
        public string? NavigateBack(CustomListView listBox)
        {
            // Если в стеке назад больше одного элемента
            if (backStack.Count > 1)
            {
                // Извлечение текущего пути из стека назад
                string current = backStack.Pop();
                // Добавление текущего пути в стек вперёд
                forwardStack.Push(current);
                // Установка текущего пути в listBox равного новому верхнему элементу стека назад
                listBox.CurrentPath = backStack.Peek();
                // Обновление текущего пути в главном окне
                MainWindow.UpdateCurrentPath(listBox);
                return backStack.Peek();
            }
            // Если в стеке назад один элемент
            else if (backStack.Count == 1)
            {
                // Извлечение текущего пути из стека назад
                string current = backStack.Pop();
                // Добавление текущего пути в стек вперёд
                forwardStack.Push(current);
                // Установка текущего пути в listBox в null
                listBox.CurrentPath = null;
                // Обновление текущего пути в главном окне
                MainWindow.UpdateCurrentPath(listBox);
                return null;
            }
            return null;
        }

        // Метод для перехода вперёд
        public string? NavigateForward(CustomListView listBox)
        {
            // Если в стеке вперёд есть элементы
            if (forwardStack.Count > 0)
            {
                // Извлечение следующего пути из стека вперёд
                string next = forwardStack.Pop();
                // Добавление следующего пути в стек назад
                backStack.Push(next);
                // Установка текущего пути в listBox равного следующему пути
                listBox.CurrentPath = next;
                // Обновление текущего пути в главном окне
                MainWindow.UpdateCurrentPath(listBox);
                return next;
            }

            return null;
        }
    }
}
