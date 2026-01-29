using System;
using System.Windows.Threading;

namespace KID
{
    /// <summary>
    /// Статический класс для централизованного управления Dispatcher и выполнения операций в UI потоке.
    /// </summary>
    public static class DispatcherManager
    {
        private static Dispatcher? _dispatcher;

        /// <summary>
        /// Инициализирует DispatcherManager с указанным Dispatcher.
        /// </summary>
        /// <param name="dispatcher">Dispatcher для использования при выполнении операций в UI потоке.</param>
        public static void Init(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>
        /// Выполняет действие в UI потоке.
        /// Если текущий поток уже является UI потоком, действие выполняется синхронно.
        /// В противном случае действие выполняется асинхронно через BeginInvoke.
        /// </summary>
        /// <param name="action">Действие для выполнения.</param>
        public static void InvokeOnUI(Action action)
        {
            if (action == null)
                return;

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
        /// Выполняет функцию в UI потоке с возвратом значения.
        /// Если текущий поток уже является UI потоком, функция выполняется синхронно.
        /// В противном случае функция выполняется синхронно через Invoke.
        /// </summary>
        /// <typeparam name="T">Тип возвращаемого значения.</typeparam>
        /// <param name="func">Функция для выполнения.</param>
        /// <returns>Результат выполнения функции.</returns>
        public static T InvokeOnUI<T>(Func<T> func)
        {
            if (func == null)
                return default!;

            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                return func();
            }
            else
            {
                T result = default!;
                _dispatcher.Invoke(() => { result = func(); }, DispatcherPriority.Background);
                return result;
            }
        }
    }
}
