using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace KID
{
    /// <summary>
    /// Статический частичный класс для работы с музыкой и звуком.
    /// Предоставляет API для воспроизведения тонов, полифонии и аудиофайлов.
    /// </summary>
    public static partial class Music
    {
        private static Dispatcher? _dispatcher;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Инициализация Music API (вызывается автоматически при первом использовании).
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_dispatcher == null)
            {
                _dispatcher = Application.Current?.Dispatcher;
            }
        }

        /// <summary>
        /// Выполняет действие в UI потоке.
        /// </summary>
        /// <param name="action">Действие для выполнения.</param>
        internal static void InvokeOnUI(Action action)
        {
            if (action == null)
                return;

            EnsureInitialized();

            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action, DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Выполняет функцию в UI потоке и возвращает результат.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="func">Функция для выполнения.</param>
        /// <returns>Результат выполнения функции.</returns>
        internal static T InvokeOnUI<T>(Func<T> func)
        {
            if (func == null)
                return default(T)!;

            EnsureInitialized();

            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                return func();
            }
            else
            {
                T result = default(T)!;
                _dispatcher.Invoke(() => { result = func(); }, DispatcherPriority.Background);
                return result;
            }
        }

        /// <summary>
        /// Проверяет, была ли нажата кнопка остановки, и выбрасывает исключение если да.
        /// </summary>
        internal static void CheckStopRequested()
        {
            StopManager.StopIfButtonPressed();
        }
    }
}

