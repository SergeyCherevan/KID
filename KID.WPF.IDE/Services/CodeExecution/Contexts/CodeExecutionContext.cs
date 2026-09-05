using KID.Services.CodeExecution.Contexts.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using KID;

namespace KID.Services.CodeExecution.Contexts
{
    /// <summary>
    /// Объединяет UI-контексты и состояние одного запуска пользовательской программы.
    /// </summary>
    public class CodeExecutionContext : ICodeExecutionContext
    {
        private readonly TaskCompletionSource disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int disposeStarted;
        private bool initialized;

        /// <summary>Идентификатор сессии, передаваемый её консольному контексту.</summary>
        public long ExecutionId { get; set; }

        /// <summary>
        /// Подготавливает Canvas и связанные графические, мышиные, клавиатурные и музыкальные API.
        /// Обязателен при создании контекста и инициализируется методом <see cref="Init"/>.
        /// </summary>
        public required IGraphicsContext GraphicsContext { get; set; }

        /// <summary>
        /// Перенаправляет стандартные потоки консоли в UI и восстанавливает их после выполнения.
        /// Обязателен при создании контекста и инициализируется методом <see cref="Init"/>.
        /// </summary>
        public required IConsoleContext ConsoleContext { get; set; }

        /// <summary>
        /// Передаёт запрос Stop компилятору и выполняемой пользовательской программе.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = default;

        /// <summary>
        /// Диспетчер UI-потока, через который библиотека выполняет операции с WPF-контролами.
        /// Обязателен при создании контекста и регистрируется методом <see cref="Init"/>.
        /// </summary>
        public required Dispatcher Dispatcher { get; set; }

        public void Init()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposeStarted) != 0, this);
            if (initialized) throw new InvalidOperationException("Execution context is already initialized.");
            initialized = true;
            DispatcherManager.Init(Dispatcher);
            GraphicsContext?.Init();
            ConsoleContext?.Init(ExecutionId, CancellationToken);
        }

        /// <summary>
        /// Ожидает консольную очистку, даже если освобождение графики завершилось ошибкой.
        /// Coordinator вызывает метод после Completion и до выгрузки ALC и session CTS.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposeStarted, 1) == 0)
                _ = DisposeCoreAsync();
            return new ValueTask(disposeCompletion.Task);
        }

        private async Task DisposeCoreAsync()
        {
            var failures = new ExecutionFailureCollector();
            failures.Capture(() => GraphicsContext?.Dispose());
            try
            {
                if (ConsoleContext != null)
                    await ConsoleContext.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            var failure = failures.CreateException("Execution context cleanup failed.");
            if (failure == null) disposeCompletion.TrySetResult();
            else disposeCompletion.TrySetException(failure);
        }
    }
}
