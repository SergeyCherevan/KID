using KID.Services.Errors;
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
        private readonly object lifecycleLock = new();
        private readonly TaskCompletionSource disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool disposeStarted;
        private bool cleanupStarted;
        private bool initialized;
        private bool initializationFailed;
        private readonly ExecutionFailureCollector beginCleanupFailures = new();
        private long initializedExecutionId;
        private CancellationToken initializedCancellationToken;
        private Dispatcher? initializedDispatcher;
        private IGraphicsContext? initializedGraphicsContext;
        private IConsoleContext? initializedConsoleContext;
        private object? initializedGraphicsTarget;
        private object? initializedConsoleTarget;

        /// <summary>Идентификатор сессии, передаваемый графическому и консольному контекстам.</summary>
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
            lock (lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(disposeStarted || cleanupStarted, this);

                if (initialized)
                {
                    if (HasSameInitializationIdentity())
                        return;

                    throw new InvalidOperationException(
                        "Execution context is already initialized for another session or UI target.");
                }

                if (initializationFailed)
                {
                    throw new InvalidOperationException(
                        "Execution context cannot be initialized after a partial initialization failure.");
                }

                initializedExecutionId = ExecutionId;
                initializedCancellationToken = CancellationToken;
                initializedDispatcher = Dispatcher;
                initializedGraphicsContext = GraphicsContext;
                initializedConsoleContext = ConsoleContext;
                initializedGraphicsTarget = GraphicsContext?.GraphicsTarget;
                initializedConsoleTarget = ConsoleContext?.ConsoleTarget;

                try
                {
                    GraphicsContext?.Init(ExecutionId, Dispatcher);
                    ConsoleContext?.Init(ExecutionId, CancellationToken);
                    initialized = true;
                }
                catch
                {
                    initializationFailed = true;
                    throw;
                }
            }
        }

        private bool HasSameInitializationIdentity() =>
            initializedExecutionId == ExecutionId &&
            initializedCancellationToken == CancellationToken &&
            ReferenceEquals(initializedDispatcher, Dispatcher) &&
            ReferenceEquals(initializedGraphicsContext, GraphicsContext) &&
            ReferenceEquals(initializedConsoleContext, ConsoleContext) &&
            ReferenceEquals(initializedGraphicsTarget, GraphicsContext?.GraphicsTarget) &&
            ReferenceEquals(initializedConsoleTarget, ConsoleContext?.ConsoleTarget);

        /// <summary>
        /// Ожидает графическую, затем консольную очистку, не пропуская консоль после ошибки графики.
        /// Coordinator вызывает метод после Completion и до выгрузки ALC и session CTS.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            bool startDispose;
            lock (lifecycleLock)
            {
                startDispose = !disposeStarted;
                disposeStarted = true;
            }

            if (startDispose)
                _ = DisposeCoreAsync();
            return new ValueTask(disposeCompletion.Task);
        }

        private async Task DisposeCoreAsync()
        {
            var failures = new ExecutionFailureCollector();
            IGraphicsContext? ownedGraphicsContext;
            IConsoleContext? ownedConsoleContext;

            lock (lifecycleLock)
            {
                /* После первой попытки Init ownership привязан к тем же экземплярам,
                 * даже если вызывающая сторона позже ошибочно изменила public properties.
                 * До Init Dispose освобождает переданные на текущий момент контексты.
                 */
                ownedGraphicsContext = initializedGraphicsContext ?? GraphicsContext;
                ownedConsoleContext = initializedConsoleContext ?? ConsoleContext;
            }

            /* До первого await закрываем приём во всех дочерних контекстах. Console при этом
             * ещё не восстанавливает глобальные streams: они нужны до завершения library workers.
             */
            failures.Capture(BeginCleanup);
            beginCleanupFailures.DrainTo(failures);
            await failures.CaptureAsync(() => ownedGraphicsContext?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            await failures.CaptureAsync(() => ownedConsoleContext?.DisposeAsync().AsTask() ?? Task.CompletedTask);
            var failure = failures.CreateException("Execution context cleanup failed.");
            if (failure == null) disposeCompletion.TrySetResult();
            else disposeCompletion.TrySetException(failure);
        }

        public void BeginCleanup()
        {
            IGraphicsContext? ownedGraphicsContext;
            IConsoleContext? ownedConsoleContext;

            lock (lifecycleLock)
            {
                if (cleanupStarted)
                    return;

                cleanupStarted = true;
                ownedGraphicsContext = initializedGraphicsContext ?? GraphicsContext;
                ownedConsoleContext = initializedConsoleContext ?? ConsoleContext;

                /* Захват failures остаётся под lifecycle lock, чтобы конкурентный
                 * DisposeAsync не успел опустошить collector до окончания этой фазы.
                 */
                beginCleanupFailures.Capture(() => ownedConsoleContext?.BeginCleanup());
                beginCleanupFailures.Capture(() => ownedGraphicsContext?.BeginCleanup());
            }
        }
    }
}
