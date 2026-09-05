using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.Localization.Interfaces;
using Microsoft.VisualStudio.Threading;

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
    internal sealed class CollectibleCodeRunningInstance : ICodeRunningInstance
    {
        /// <summary>
        /// Состояния одного экземпляра выполнения.
        /// </summary>
        private enum LifecycleState
        {
            Ready = 0,
            Running = 1,
            Completed = 2,
            Disposed = 3
        }

        private readonly ILocalizationService localizationService;
        private CompilationArtifact? artifact;
        private UserProgramLoadContext? loadContext;
        private WeakReference? loadContextReference;
        private JoinableTask? completion;
        private int lifecycleStateValue = (int)LifecycleState.Ready;

        /// <summary>
        /// Создаёт экземпляр, который пока владеет только неизменяемым PE/PDB-артефактом.
        /// </summary>
        /// <param name="artifact">Артефакт успешной компиляции.</param>
        /// <param name="localizationService">Источник сообщений о результате выполнения.</param>
        public CollectibleCodeRunningInstance(
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
        /// Возвращает одну и ту же задачу уже запущенного выполнения без повторного запуска.
        /// </summary>
        /// <remarks>
        /// Runner публикует экземпляр только после Start. Завершение этой задачи позволяет
        /// coordinator начать очистку контекста; сам Dispose и Unload в неё не входят.
        /// </remarks>
        public JoinableTask Completion => completion ??
            throw new InvalidOperationException("Running instance has not been started.");

        /// <summary>
        /// Начинает выполнение ровно один раз и сохраняет его задачу перед возвратом из runner.
        /// </summary>
        /// <param name="joinableTaskFactory">
        /// Фабрика, связывающая заранее запущенную операцию с ожидающим её UI-контекстом.
        /// </param>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        /// <exception cref="InvalidOperationException">
        /// Экземпляр уже запускался либо выполняется сейчас.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Экземпляр уже освобождён.</exception>
        internal void Start(
            JoinableTaskFactory joinableTaskFactory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(joinableTaskFactory);

            /* Единый атомарный переход Ready → Running одновременно запрещает повторный запуск
             * и закрывает гонку между Start и Dispose.
             */
            var previousState = CompareExchangeLifecycleState(
                LifecycleState.Running,
                LifecycleState.Ready);

            if (previousState == LifecycleState.Disposed)
                throw new ObjectDisposedException(nameof(CollectibleCodeRunningInstance));
            if (previousState != LifecycleState.Ready)
                throw new InvalidOperationException("Running instance can only be started once.");

            /* ExecuteAsync сохраняет ошибки и отмену в Task, даже если они возникли до первого
             * незавершённого await. Runner сможет вернуть экземпляр владельцу для cleanup.
             */
            completion = joinableTaskFactory.RunAsync(() => ExecuteAsync(cancellationToken));
        }

        /// <summary>
        /// Загружает и выполняет артефакт вне UI-потока, затем публикует результат выполнения.
        /// </summary>
        /// <param name="cancellationToken">Токен активной execution-сессии.</param>
        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                /* ConfigureAwait(false) не возвращает обработку результата на UI-поток.
                 * Сам load/invoke выполняется внутри отдельного worker delegate, после чего
                 * возможный Task-результат entry point ожидается до публикации исхода.
                 */
                var invocation = await Task.Run(
                        () => LoadAndInvokeEntryPoint(cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
                var outcome = await AwaitEntryPointAsync(invocation, cancellationToken)
                    .ConfigureAwait(false);

                await ReportOutcomeAsync(outcome).ConfigureAwait(false);
            }
            finally
            {
                /* Completed означает, что worker stack уже покинул пользовательскую Assembly,
                 * поэтому coordinator теперь может безопасно инициировать Unload.
                 */
                WriteLifecycleState(LifecycleState.Completed);
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
                var currentState = ReadLifecycleState();
                if (currentState == LifecycleState.Disposed)
                    return;
                if (currentState == LifecycleState.Running)
                {
                    throw new InvalidOperationException(
                        "Running instance cannot be disposed while it is running.");
                }

                if (CompareExchangeLifecycleState(
                    LifecycleState.Disposed,
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
        /// Атомарно читает текущее типизированное состояние из целочисленного backing field.
        /// </summary>
        private LifecycleState ReadLifecycleState() =>
            (LifecycleState)Volatile.Read(ref lifecycleStateValue);

        /// <summary>
        /// Атомарно заменяет ожидаемое состояние и возвращает фактическое предыдущее значение.
        /// </summary>
        /// <param name="newState">Состояние, которое требуется записать.</param>
        /// <param name="expectedState">Состояние, при котором разрешена замена.</param>
        private LifecycleState CompareExchangeLifecycleState(
            LifecycleState newState,
            LifecycleState expectedState) =>
            (LifecycleState)Interlocked.CompareExchange(
                ref lifecycleStateValue,
                (int)newState,
                (int)expectedState);

        /// <summary>
        /// Атомарно публикует новое состояние после завершения операции этого экземпляра.
        /// </summary>
        /// <param name="newState">Состояние, которое требуется опубликовать.</param>
        private void WriteLifecycleState(LifecycleState newState) =>
            Volatile.Write(ref lifecycleStateValue, (int)newState);

        /// <summary>
        /// Создаёт collectible context, загружает PE/PDB и вызывает entry point.
        /// </summary>
        /// <returns>
        /// Немедленный host-only результат либо Task, который необходимо дождаться перед cleanup.
        /// </returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private EntryPointInvocation LoadAndInvokeEntryPoint(
            CancellationToken cancellationToken)
        {
            /* Артефакт забирается только одним разрешённым запуском. После загрузки экземпляр больше
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
                return EntryPointInvocation.FromOutcome(ExecutionOutcome.None);

            var parameters = entryPoint.GetParameters().Length == 0
                ? null
                : new object[] { Array.Empty<string>() };

            try
            {
                /* Для async Main Roslyn создаёт синхронный runtime-wrapper, который сам дожидается
                 * исходного Task и возвращает void/int. Защитная проверка Task ниже также корректно
                 * обрабатывает reflection entry point, вернувший Task.
                 */
                var invocationResult = entryPoint.Invoke(null, parameters);
                return invocationResult switch
                {
                    null => EntryPointInvocation.FromOutcome(ExecutionOutcome.Finished),
                    int => EntryPointInvocation.FromOutcome(ExecutionOutcome.Finished),
                    Task task => EntryPointInvocation.FromTask(task),
                    _ => throw new InvalidOperationException(
                        $"Unsupported entry point result type: {invocationResult.GetType().FullName}.")
                };
            }
            catch (TargetInvocationException exception)
            {
                /* Reflection оборачивает исключение entry point. В host-результат выносятся
                 * данные исходной пользовательской ошибки, а не служебной оболочки Invoke.
                 */
                var userException = exception.InnerException ?? exception;
                return EntryPointInvocation.FromOutcome(
                    ClassifyUserException(userException, cancellationToken));
            }
        }

        /// <summary>
        /// Дожидается прямого Task/Task&lt;int&gt;-результата entry point и преобразует его исход.
        /// </summary>
        /// <param name="invocation">
        /// Результат загрузки и вызова, не содержащий Assembly, Type или MethodInfo.
        /// </param>
        private static async Task<ExecutionOutcome> AwaitEntryPointAsync(
            EntryPointInvocation invocation,
            CancellationToken cancellationToken)
        {
            var asyncCompletion = invocation.AsyncCompletion;
            if (asyncCompletion == null)
                return invocation.ImmediateOutcome;

            try
            {
                /* Task<int> наследуется от Task: обычный await ожидает завершение, а exit code
                 * пока не публикуется наружу, как и результат синхронного int Main.
                 */
                await asyncCompletion.ConfigureAwait(false);
                return ExecutionOutcome.Finished;
            }
            catch (Exception exception)
            {
                return ClassifyUserException(exception, cancellationToken);
            }
            finally
            {
                /* Параметр и локальная переменная являются полями async state machine. Явное
                 * обнуление не оставляет завершённый user Task корнем collectible ALC.
                 */
                asyncCompletion = null;
                invocation = default;
            }
        }

        /// <summary>
        /// Отличает ожидаемый Stop текущей сессии от пользовательской runtime-ошибки.
        /// </summary>
        /// <remarks>
        /// Сам тип OperationCanceledException недостаточен: пользовательская программа может
        /// выбросить его без нажатия Stop. Ожидаемой остановкой он становится только после
        /// фактической отмены token, переданного этому running instance.
        /// </remarks>
        private static ExecutionOutcome ClassifyUserException(
            Exception exception,
            CancellationToken cancellationToken) =>
            ExecutionExceptionClassifier.IsExpectedStop(exception, cancellationToken)
                ? ExecutionOutcome.Stopped
                : ExecutionOutcome.FromError(exception.Message, exception.StackTrace);

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
        /// Host-owned описание вызова, временно удерживающее только ожидаемый Task пользователя.
        /// </summary>
        private readonly record struct EntryPointInvocation(
            ExecutionOutcome ImmediateOutcome,
            Task? AsyncCompletion)
        {
            /// <summary>Создаёт уже завершённый результат вызова.</summary>
            public static EntryPointInvocation FromOutcome(ExecutionOutcome outcome) =>
                new(outcome, null);

            /// <summary>Создаёт результат, завершение которого ещё требуется дождаться.</summary>
            public static EntryPointInvocation FromTask(Task completion) =>
                new(default, completion);
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
            None = 0,
            Finished = 1,
            Stopped = 2,
            Error = 3
        }
    }
}
