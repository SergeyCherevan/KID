using System;
using System.Collections.Concurrent;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Потокобезопасно накапливает ошибки одного execution lifecycle в порядке регистрации.
    /// </summary>
    /// <remarks>
    /// Коллектор предоставляет только общий механизм хранения и агрегации. Решение о том,
    /// какая ошибка является primary, когда переносить observer failures и разрешать ли новый
    /// Run, остаётся у execution coordinator и владельца соответствующего ресурса.
    /// </remarks>
    internal sealed class ExecutionFailureCollector
    {
        private readonly ConcurrentQueue<Exception> failures = new();

        /// <summary>
        /// Добавляет исключение в конец упорядоченной последовательности ошибок.
        /// </summary>
        /// <param name="exception">Сохранённая ошибка execution lifecycle.</param>
        public void Add(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            failures.Enqueue(exception);
        }

        /// <summary>
        /// Выполняет действие, сохраняя выброшенное им исключение вместо прерывания
        /// последующих независимых lifecycle-шагов.
        /// </summary>
        /// <param name="action">Действие, которому необходимо предоставить попытку выполнения.</param>
        /// <returns><see langword="true"/>, если действие завершилось без исключения.</returns>
        public bool Capture(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                action();
                return true;
            }
            catch (Exception exception)
            {
                Add(exception);
                return false;
            }
        }

        /// <summary>
        /// Переносит накопленные ошибки в другой коллектор, сохраняя их порядок.
        /// </summary>
        /// <remarks>
        /// Операция опустошает текущий коллектор. Она позволяет хранить observer failures
        /// отдельно во время выполнения, а после cleanup добавить их вслед за primary и
        /// resource-cleanup errors.
        /// </remarks>
        /// <param name="destination">Коллектор, в конец которого переносятся ошибки.</param>
        public void DrainTo(ExecutionFailureCollector destination)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (ReferenceEquals(this, destination))
            {
                throw new ArgumentException(
                    "Failure collector cannot drain into itself.",
                    nameof(destination));
            }

            while (failures.TryDequeue(out var failure))
                destination.Add(failure);
        }

        /// <summary>
        /// Создаёт итоговое исключение, не изменяя содержимое коллектора.
        /// </summary>
        /// <param name="aggregateMessage">Сообщение для случая нескольких ошибок.</param>
        /// <returns>
        /// <see langword="null"/> при отсутствии ошибок, исходное исключение при одной ошибке
        /// или <see cref="AggregateException"/> с сохранённым порядком при нескольких ошибках.
        /// </returns>
        public Exception? CreateException(string aggregateMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(aggregateMessage);

            var snapshot = failures.ToArray();
            return snapshot.Length switch
            {
                0 => null,
                1 => snapshot[0],
                _ => new AggregateException(aggregateMessage, snapshot)
            };
        }
    }
}
