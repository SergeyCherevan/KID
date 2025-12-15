using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KID.Services.CodeExecution;

namespace KID
{
    /// <summary>
    /// Статический частичный класс для работы с клавиатурой.
    /// Предоставляет информацию о нажатых клавишах и их символах.
    /// </summary>
    public static partial class Keyboard
    {
        /// <summary>
        /// UIElement, на котором отслеживается фокус клавиатуры (опционально).
        /// </summary>
        public static UIElement? TargetElement { get; private set; }

        private static Window? _mainWindow;

        /// <summary>
        /// Инициализация Keyboard API с опциональным UIElement для отслеживания фокуса.
        /// </summary>
        /// <param name="targetElement">UIElement для отслеживания фокуса. Если null, отслеживание глобальное.</param>
        public static void Init(UIElement? targetElement = null)
        {
            if (Application.Current == null)
                throw new InvalidOperationException("Application.Current is null");

            _mainWindow = Application.Current.MainWindow;
            if (_mainWindow == null)
                throw new InvalidOperationException("Application.Current.MainWindow is null");

            // Отписываемся от предыдущих событий, если были
            if (_mainWindow != null)
            {
                UnsubscribeFromEvents(_mainWindow);
            }

            if (TargetElement != null)
            {
                UnsubscribeFromTargetElementEvents(TargetElement);
            }

            // Подписываемся на события MainWindow
            SubscribeToEvents(_mainWindow);

            // Если targetElement указан, подписаться на его события фокуса
            if (targetElement != null)
            {
                TargetElement = targetElement;
                SubscribeToTargetElementEvents(targetElement);
            }
            else
            {
                TargetElement = null;
            }
        }

        /// <summary>
        /// Устанавливает фокус клавиатуры на TargetElement (Canvas).
        /// </summary>
        public static void Focus()
        {
            DispatcherManager.InvokeOnUI(() =>
            {
                if (TargetElement == null)
                    return;

                // Делаем элемент focusable, если это необходимо
                if (TargetElement is Canvas canvas)
                {
                    canvas.Focusable = true;
                }

                // Метод 1: Стандартный Focus()
                TargetElement.Focus();

                // Метод 2: System.Windows.Input.Keyboard.Focus() - более надежный
                System.Windows.Input.Keyboard.Focus(TargetElement);

                // Метод 3: FocusManager - устанавливает фокус на уровне окна
                if (TargetElement is FrameworkElement frameworkElement && frameworkElement.IsLoaded)
                {
                    FocusManager.SetFocusedElement(
                        FocusManager.GetFocusScope(TargetElement), 
                        TargetElement
                    );
                }

                // Делаем элемент видимым
                if (TargetElement is FrameworkElement element)
                {
                    element.BringIntoView();
                }
            });
        }

        /// <summary>
        /// Подписывается на события клавиатуры MainWindow.
        /// </summary>
        private static void SubscribeToEvents(Window window)
        {
            window.KeyDown += Window_KeyDown;
            window.KeyUp += Window_KeyUp;
            window.TextInput += Window_TextInput;
        }

        /// <summary>
        /// Отписывается от событий клавиатуры MainWindow.
        /// </summary>
        private static void UnsubscribeFromEvents(Window window)
        {
            window.KeyDown -= Window_KeyDown;
            window.KeyUp -= Window_KeyUp;
            window.TextInput -= Window_TextInput;
        }

        /// <summary>
        /// Подписывается на события фокуса targetElement.
        /// </summary>
        private static void SubscribeToTargetElementEvents(UIElement element)
        {
            element.GotKeyboardFocus += TargetElement_GotKeyboardFocus;
            element.LostKeyboardFocus += TargetElement_LostKeyboardFocus;
        }

        /// <summary>
        /// Отписывается от событий фокуса targetElement.
        /// </summary>
        private static void UnsubscribeFromTargetElementEvents(UIElement element)
        {
            element.GotKeyboardFocus -= TargetElement_GotKeyboardFocus;
            element.LostKeyboardFocus -= TargetElement_LostKeyboardFocus;
        }

        // Обработчики событий - вызывают partial методы, реализованные в других файлах
        private static void Window_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e);
        }

        private static void Window_KeyUp(object sender, KeyEventArgs e)
        {
            OnKeyUp(e);
        }

        private static void Window_TextInput(object sender, TextCompositionEventArgs e)
        {
            OnTextInput(e);
        }

        private static void TargetElement_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Можно использовать для обновления состояния фокуса
        }

        private static void TargetElement_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Можно использовать для обновления состояния фокуса
        }

        // Partial методы для реализации в других файлах
        static partial void OnKeyDown(KeyEventArgs e);
        static partial void OnKeyUp(KeyEventArgs e);
        static partial void OnTextInput(TextCompositionEventArgs e);
    }
}

