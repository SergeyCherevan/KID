using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Localization.Interfaces;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Загружает и выполняет одну пользовательскую программу в собственном collectible context.
    /// </summary>
    /// <remarks>
    /// Объект хранит сильную ссылку только на сам AssemblyLoadContext, пока coordinator не вызовет
    /// <see cref="Dispose"/>. Assembly, Type и MethodInfo существуют лишь внутри отдельного
    /// non-inlined метода выполнения и не сохраняются в singleton-сервисах.
    /// </remarks>
    internal sealed class CollectibleCodeExecutionHandle : ICodeExecutionHandle
    {
        private const int ReadyState = 0;
        private const int RunningState = 1;
        private const int CompletedState = 2;
        private const int DisposedState = 3;

        private readonly ILocalizationService localizationService;
        private CompilationArtifact? artifact;
        private UserProgramLoadContext? loadContext;
        private WeakReference? loadContextReference;
        private int lifecycleState;

        /// <summary>
        /// Создаёт handle, который пока владеет только неизменяемым PE/PDB-артефактом.
        /// </summary>
        /// <param name="artifact">Артефакт успешной компиляции.</param>
        /// <param name="localizationService">Источник сообщений о результате выполнения.</param>
        public CollectibleCodeExecutionHandle(
            CompilationArtifact artifact,
            ILocalizationService localizationService)
        {
            this.artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            this.localizationService = localizationService ??
                throw new ArgumentNullException(nameof(localizationService));
        }

        /// <summary>
        /// Слабая ссылка на созданный context для регрессионной проверки фактической выгрузки.
        /// </summary>
        internal WeakReference? LoadContextReference => loadContextReference;

        /// <summary>
        /// Выполняет артефакт ровно один раз вне UI-потока.
        /// </summary>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        /// <exception cref="InvalidOperationException">
        /// Handle уже запускался либо выполняется сейчас.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Handle уже освобождён.</exception>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            /* Единый атомарный переход Ready → Running одновременно запрещает повторный запуск
             * и закрывает гонку между RunAsync и Dispose.
             */
            var previousState = Interlocked.CompareExchange(
                ref lifecycleState,
                RunningState,
                ReadyState);

            if (previousState == DisposedState)
                throw new ObjectDisposedException(nameof(CollectibleCodeExecutionHandle));
            if (previousState != ReadyState)
                throw new InvalidOperationException("Execution handle can only be run once.");

            try
            {
                /* ConfigureAwait(false) не возвращает обработку результата на UI-поток.
                 * Сам load/invoke выполняется внутри отдельного worker delegate.
                 */
                var outcome = await Task.Run(
                        ExecuteArtifact,
                        cancellationToken)
                    .ConfigureAwait(false);

                await ReportOutcomeAsync(outcome).ConfigureAwait(false);
            }
            finally
            {
                /* Completed означает, что worker stack уже покинул пользовательскую Assembly,
                 * поэтому coordinator теперь может безопасно инициировать Unload.
                 */
                Volatile.Write(ref lifecycleState, CompletedState);
            }
        }

        /// <summary>
        /// Инициирует выгрузку пользовательского AssemblyLoadContext и разрывает сильные ссылки.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Выполнение ещё находится внутри пользовательской сборки.
        /// </exception>
        public void Dispose()
        {
            while (true)
            {
                var currentState = Volatile.Read(ref lifecycleState);
                if (currentState == DisposedState)
                    return;
                if (currentState == RunningState)
                {
                    throw new InvalidOperationException(
                        "Execution handle cannot be disposed while it is running.");
                }

                if (Interlocked.CompareExchange(
                    ref lifecycleState,
                    DisposedState,
                    currentState) == currentState)
                {
                    break;
                }
            }

            /* Interlocked.Exchange сначала удаляет последнюю host-ссылку на context, после чего
             * Unload запускает кооперативную выгрузку. PE/PDB также больше не нужны.
             */
            artifact = null;
            var contextToUnload = Interlocked.Exchange(ref loadContext, null);
            contextToUnload?.Unload();
        }

        /// <summary>
        /// Создаёт collectible context, загружает PE/PDB и синхронно вызывает entry point.
        /// </summary>
        /// <returns>Результат без ссылок на типы и объекты пользовательской сборки.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ExecutionOutcome ExecuteArtifact()
        {
            /* Артефакт забирается только одним разрешённым запуском. После загрузки handle больше
             * не удерживает его буферы, а runtime-объекты остаются локальными этому методу.
             */
            var executionArtifact = Interlocked.Exchange(ref artifact, null) ??
                throw new InvalidOperationException("Compilation artifact is unavailable.");
            var executionLoadContext = new UserProgramLoadContext();
            loadContext = executionLoadContext;
            loadContextReference = new WeakReference(executionLoadContext);

            using var peStream = new MemoryStream(
                executionArtifact.PeImage.ToArray(),
                writable: false);
            using var pdbStream = executionArtifact.PdbImage.IsEmpty
                ? null
                : new MemoryStream(executionArtifact.PdbImage.ToArray(), writable: false);
            var assembly = pdbStream == null
                ? executionLoadContext.LoadFromStream(peStream)
                : executionLoadContext.LoadFromStream(peStream, pdbStream);
            var entryPoint = assembly.EntryPoint;
            if (entryPoint == null)
                return ExecutionOutcome.None;

            var parameters = entryPoint.GetParameters().Length == 0
                ? null
                : new object[] { Array.Empty<string>() };

            try
            {
                /* Возвращаемое значение намеренно пока не ожидается: поддержка Task и Task<int>
                 * является следующим отдельным подэтапом и не смешивается с ownership ALC.
                 */
                _ = entryPoint.Invoke(null, parameters);
                return ExecutionOutcome.Finished;
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException is OperationCanceledException)
                    return ExecutionOutcome.Stopped;

                var innerException = exception.InnerException;
                return ExecutionOutcome.FromError(
                    innerException?.Message ?? exception.Message,
                    innerException?.StackTrace ?? exception.StackTrace);
            }
            catch (OperationCanceledException)
            {
                return ExecutionOutcome.Stopped;
            }
        }

        /// <summary>
        /// Выводит host-only результат после выхода worker stack из пользовательской сборки.
        /// </summary>
        /// <param name="outcome">Результат, содержащий только строки и host enum.</param>
        private async Task ReportOutcomeAsync(ExecutionOutcome outcome)
        {
            switch (outcome.Kind)
            {
                case ExecutionOutcomeKind.None:
                    return;
                case ExecutionOutcomeKind.Finished:
                    Console.WriteLine(localizationService.GetString("Notification_ProgramFinished"));
                    return;
                case ExecutionOutcomeKind.Stopped:
                    Console.WriteLine(localizationService.GetString("Notification_ProgramStopped"));
                    return;
                case ExecutionOutcomeKind.Error:
                    await Console.Error.WriteLineAsync(
                        localizationService.GetString(
                            "Error_Execution",
                            outcome.ErrorMessage ?? string.Empty));
                    if (!string.IsNullOrEmpty(outcome.StackTrace))
                    {
                        await Console.Error.WriteLineAsync(
                            localizationService.GetString("Error_StackTrace", outcome.StackTrace));
                    }
                    return;
                default:
                    throw new InvalidOperationException("Unknown execution outcome.");
            }
        }

        /// <summary>
        /// Host-owned описание результата, не удерживающее runtime-объекты user assembly.
        /// </summary>
        private readonly record struct ExecutionOutcome(
            ExecutionOutcomeKind Kind,
            string? ErrorMessage = null,
            string? StackTrace = null)
        {
            /// <summary>Entry point отсутствует.</summary>
            public static ExecutionOutcome None { get; } = new(ExecutionOutcomeKind.None);

            /// <summary>Синхронный entry point завершился.</summary>
            public static ExecutionOutcome Finished { get; } = new(ExecutionOutcomeKind.Finished);

            /// <summary>Entry point завершился ожидаемой отменой.</summary>
            public static ExecutionOutcome Stopped { get; } = new(ExecutionOutcomeKind.Stopped);

            /// <summary>Создаёт результат пользовательской runtime-ошибки.</summary>
            public static ExecutionOutcome FromError(string message, string? stackTrace) =>
                new(ExecutionOutcomeKind.Error, message, stackTrace);
        }

        /// <summary>
        /// Варианты результата, которые можно безопасно вынести за пределы collectible context.
        /// </summary>
        private enum ExecutionOutcomeKind
        {
            None,
            Finished,
            Stopped,
            Error
        }
    }
}
