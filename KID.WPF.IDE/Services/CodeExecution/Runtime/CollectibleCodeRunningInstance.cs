using KID.Services.CodeExecution.Runtime.Interfaces;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Threading;
using KID.Services.CodeExecution.Errors;

namespace KID.Services.CodeExecution.Runtime
{
    /// <summary>
    /// Загружает и выполняет одну пользовательскую программу в собственном collectible context.
    /// </summary>
    /// <remarks>
    /// Объект хранит сильную ссылку только на сам AssemblyLoadContext, пока coordinator не вызовет
    /// <see cref="Dispose"/>. Assembly, Type и MethodInfo существуют лишь внутри отдельного
    /// non-inlined метода выполнения и не сохраняются в singleton-сервисах.
    /// Вызов <see cref="System.Runtime.Loader.AssemblyLoadContext.Unload"/> только инициирует
    /// кооперативную выгрузку: живой пользовательский поток, delegate, static event или другой
    /// внешний strong reference может удерживать context после завершения основного entry point.
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

        private CompilationArtifact? artifact;
        private UserProgramLoadContext? loadContext;
        private WeakReference? loadContextReference;
        private JoinableTask<ExecutionResult>? completion;
        private int lifecycleStateValue = (int)LifecycleState.Ready;

        /// <summary>
        /// Создаёт экземпляр, который пока владеет только неизменяемым PE/PDB-артефактом.
        /// </summary>
        /// <param name="artifact">Артефакт успешной компиляции.</param>
        public CollectibleCodeRunningInstance(CompilationArtifact artifact)
        {
            this.artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
        }

        /// <summary>
        /// Слабая ссылка на созданный context для регрессионной проверки фактической выгрузки.
        /// </summary>
        /// <remarks>
        /// После <see cref="Dispose"/> живое значение ссылки означает, что runtime ещё не собрал
        /// context. Это диагностический сигнал, а не автоматическое доказательство host-утечки:
        /// причиной может быть продолжающий выполняться пользовательский поток.
        /// </remarks>
        internal WeakReference? LoadContextReference => loadContextReference;

        /// <summary>
        /// Возвращает одну и ту же задачу уже запущенного выполнения без повторного запуска.
        /// </summary>
        /// <remarks>
        /// Runner публикует экземпляр только после Start. Завершение этой задачи позволяет
        /// coordinator начать очистку контекста; сам Dispose и Unload в неё не входят.
        /// </remarks>
        public JoinableTask<ExecutionResult> Completion => completion ??
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
        private async Task<ExecutionResult> ExecuteAsync(CancellationToken cancellationToken)
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
                return await AwaitEntryPointAsync(invocation, cancellationToken)
                    .ConfigureAwait(false);
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
        /// <remarks>
        /// Метод не ожидает и не гарантирует фактическую сборку context. CLR завершит unload
        /// только после исчезновения всех stack frames и strong references пользовательской сборки.
        /// </remarks>
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
                return EntryPointInvocation.FromOutcome(ExecutionResult.NoEntryPoint);

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
                    null => EntryPointInvocation.FromOutcome(ExecutionResult.Completed),
                    int => EntryPointInvocation.FromOutcome(ExecutionResult.Completed),
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
        private static async Task<ExecutionResult> AwaitEntryPointAsync(
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
                return ExecutionResult.Completed;
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
        private static ExecutionResult ClassifyUserException(
            Exception exception,
            CancellationToken cancellationToken) =>
            ExecutionExceptionClassifier.IsExpectedStop(exception, cancellationToken)
                ? ExecutionResult.Stopped
                : ExecutionResult.RuntimeFaulted(exception.Message, exception.StackTrace);

        /// <summary>
        /// Host-owned описание вызова, временно удерживающее только ожидаемый Task пользователя.
        /// </summary>
        private readonly record struct EntryPointInvocation(
            ExecutionResult ImmediateOutcome,
            Task? AsyncCompletion)
        {
            /// <summary>Создаёт уже завершённый результат вызова.</summary>
            public static EntryPointInvocation FromOutcome(ExecutionResult outcome) =>
                new(outcome, null);

            /// <summary>Создаёт результат, завершение которого ещё требуется дождаться.</summary>
            public static EntryPointInvocation FromTask(Task completion) =>
                new(ExecutionResult.NoEntryPoint, completion);
        }

    }
}
