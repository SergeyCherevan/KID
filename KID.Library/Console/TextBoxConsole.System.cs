using System.Collections.Concurrent;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KID;

/// <summary>
/// Предоставляет консольный API текущей execution-сессии поверх принадлежащего ей WPF TextBox.
/// </summary>
/// <remarks>
/// В процессе может существовать только одна активная console-сессия. <see cref="Init"/>
/// публикует полностью подготовленный <see cref="ConsoleExecutionScope"/>, а повторная
/// инициализация разрешается только после завершения <see cref="ShutdownAsync"/> предыдущего
/// запуска. Сохраняемые reader/writer adapters захватывают точный scope своего запуска и не
/// разрешают текущее статическое состояние повторно, поэтому stale stream не может обратиться
/// к ресурсам следующей execution-сессии.
/// </remarks>
public static partial class TextBoxConsole
{
    private static readonly object initLock = new();
    private static readonly object stateLock = new();
    private static readonly object readLock = new();
    private static readonly Queue<char> input = new();
    private static readonly Queue<OutputWorkItem> output = new();
    private static readonly ConcurrentQueue<Exception> uiFailures = new();

    private static ConsoleExecutionScope? executionScope;
    private static AutoResetEvent? inputAvailable;
    private static ManualResetEvent? stopRequested;
    private static ManualResetEvent? disposeRequested;
    private static WaitHandle[]? readSignals;
    private static CancellationTokenRegistration? stopRegistration;
    private static TaskCompletionSource? readersExited;
    private static TaskCompletionSource? shutdownCompletion;
    private static TextWriter? textWriter;
    private static TextReader? textReader;
    private static ReadRequest? activeRead;
    private static ReadRequest? uiRead;
    private static bool closing;
    private static bool outputScheduled;
    private static bool shutdownStarted;
    private static int readerCount;

    /// <summary>Подключает консоль к явно захваченному environment текущего запуска.</summary>
    internal static ConsoleExecutionScope Init(TextBox textBox, ExecutionEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(environment);
        textBox.Dispatcher.VerifyAccess();

        ConsoleExecutionScope? scope = null;
        AutoResetEvent? newInputAvailable = null;
        ManualResetEvent? newStopRequested = null;
        ManualResetEvent? newDisposeRequested = null;
        CancellationTokenRegistration? newStopRegistration = null;

        lock (initLock)
        {
            if (Volatile.Read(ref executionScope) != null)
                throw new InvalidOperationException("TextBoxConsole is already initialized.");
            if (!ExecutionEnvironmentManager.IsCurrentAndAccepting(environment))
                throw new InvalidOperationException("Execution does not own the current environment.");

            environment.ThrowIfCancellationRequested();

            try
            {
                scope = new ConsoleExecutionScope(environment, textBox);
                newInputAvailable = new AutoResetEvent(false);
                newStopRequested = new ManualResetEvent(false);
                newDisposeRequested = new ManualResetEvent(false);
                var capturedStop = newStopRequested;
                newStopRegistration = environment.CancellationToken.Register(
                    static state => ((ManualResetEvent)state!).Set(),
                    capturedStop);

                lock (stateLock)
                {
                    input.Clear();
                    output.Clear();
                    DrainUiFailures();
                    ClearUserEvents();
                    inputAvailable = newInputAvailable;
                    stopRequested = newStopRequested;
                    disposeRequested = newDisposeRequested;
                    readSignals = [newStopRequested, newDisposeRequested, newInputAvailable];
                    stopRegistration = newStopRegistration;
                    readersExited = NewCompletionSource();
                    shutdownCompletion = NewCompletionSource();
                    textWriter = new TextBoxTextWriter(scope);
                    textReader = new TextBoxTextReader(scope);
                    activeRead = null;
                    uiRead = null;
                    closing = false;
                    outputScheduled = false;
                    shutdownStarted = false;
                    readerCount = 0;
                }

                Volatile.Write(ref executionScope, scope);
                Subscribe(scope);
                environment.ThrowIfCancellationRequested();
                return scope;
            }
            catch
            {
                if (scope != null && ReferenceEquals(Volatile.Read(ref executionScope), scope))
                {
                    BeginCleanup(environment);
                    // Инициализация всё ещё удерживает initLock. Асинхронный cleanup стартует
                    // после выхода из текущего стека и потому не может синхронно войти в Release.
                    _ = Task.Run(() => ShutdownAsync(environment).AsTask());
                }
                else
                {
                    newStopRegistration?.Dispose();
                    newInputAvailable?.Dispose();
                    newStopRequested?.Dispose();
                    newDisposeRequested?.Dispose();
                    if (scope != null)
                        scope.EventWorker.ShutdownAsync().AsTask().GetAwaiter().GetResult();
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Синхронно закрывает приём новой работы для указанного environment, закрывает event worker
    /// и пробуждает все заблокированные Console reads. Метод идемпотентен и не освобождает handles.
    /// </summary>
    internal static void BeginCleanup(ExecutionEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var scope = Volatile.Read(ref executionScope);
        if (scope == null || !ReferenceEquals(scope.Environment, environment)) return;

        lock (stateLock)
        {
            if (!ReferenceEquals(executionScope, scope) || closing) return;
            closing = true;
            scope.EventWorker.Close();
            disposeRequested?.Set();
            if (readerCount == 0) readersExited?.TrySetResult();
        }
    }

    /// <summary>
    /// Ожидает UI teardown, выполняющийся пользовательский callback и всех readers, затем
    /// освобождает wait handles, token registration, streams и static ownership указанного
    /// environment. Повторные вызовы наблюдают одну completion task; stale environment является no-op.
    /// </summary>
    internal static ValueTask ShutdownAsync(ExecutionEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var scope = Volatile.Read(ref executionScope);
        if (scope == null || !ReferenceEquals(scope.Environment, environment))
            return ValueTask.CompletedTask;

        BeginCleanup(environment);
        Task completion;
        lock (stateLock)
        {
            if (!ReferenceEquals(executionScope, scope)) return ValueTask.CompletedTask;
            completion = shutdownCompletion?.Task ?? Task.CompletedTask;
            if (!shutdownStarted)
            {
                shutdownStarted = true;
                _ = ShutdownCoreAsync(scope);
            }
        }

        return new ValueTask(completion);
    }

    internal static ConsoleExecutionScope? CurrentScope => Volatile.Read(ref executionScope);

    internal static TextWriter GetOut(ConsoleExecutionScope scope) => GetWriter(scope);
    internal static TextReader GetIn(ConsoleExecutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(!Owns(scope) || textReader == null, nameof(TextBoxConsole));
            return textReader;
        }
    }

    internal static TextWriter GetError(ConsoleExecutionScope scope) => GetWriter(scope);

    private static TextWriter GetWriter(ConsoleExecutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(!Owns(scope) || textWriter == null, nameof(TextBoxConsole));
            return textWriter;
        }
    }

    private static ConsoleExecutionScope? GetActiveScope()
    {
        var scope = Volatile.Read(ref executionScope);
        return scope != null && IsActive(scope) ? scope : null;
    }

    private static bool Owns(ConsoleExecutionScope scope) =>
        ReferenceEquals(Volatile.Read(ref executionScope), scope) &&
        ExecutionEnvironmentManager.IsCurrent(scope.Environment);

    private static bool IsActive(ConsoleExecutionScope scope) =>
        Owns(scope) &&
        !closing &&
        scope.EventWorker.IsAccepting &&
        ExecutionEnvironmentManager.IsCurrentAndAccepting(scope.Environment);

    private static void Subscribe(ConsoleExecutionScope scope)
    {
        scope.TextBox.PreviewKeyDown += OnPreviewKeyDown;
        scope.TextBox.PreviewTextInput += OnPreviewTextInput;
    }

    private static void Unsubscribe(ConsoleExecutionScope scope)
    {
        scope.TextBox.PreviewKeyDown -= OnPreviewKeyDown;
        scope.TextBox.PreviewTextInput -= OnPreviewTextInput;
    }

    private static void PostUi(ConsoleExecutionScope scope, Action action)
    {
        void InvokeSafely()
        {
            if (!Owns(scope)) return;
            try
            {
                action();
            }
            catch (Exception exception)
            {
                uiFailures.Enqueue(exception);
                BeginCleanup(scope.Environment);
            }
        }

        try
        {
            if (scope.TextBox.Dispatcher.CheckAccess()) InvokeSafely();
            else _ = scope.TextBox.Dispatcher.InvokeAsync(InvokeSafely, DispatcherPriority.Normal);
        }
        catch (Exception exception)
        {
            uiFailures.Enqueue(exception);
            BeginCleanup(scope.Environment);
        }
    }

    private static async Task ShutdownCoreAsync(ConsoleExecutionScope scope)
    {
        var failures = new List<Exception>();
        var completion = shutdownCompletion;
        var registration = stopRegistration;
        var ownedInputAvailable = inputAvailable;
        var ownedStopRequested = stopRequested;
        var ownedDisposeRequested = disposeRequested;
        var ownedReadersExited = readersExited;

        try
        {
            try
            {
                void CleanupUi()
                {
                    TryCleanup(() => Unsubscribe(scope), failures);
                    DrainOutput(scope, duringCleanup: true);
                    var request = uiRead;
                    if (request != null) TryCleanup(() => RestoreReadUi(request), failures);
                    lock (stateLock)
                    {
                        output.Clear();
                        outputScheduled = false;
                    }
                }

                if (scope.TextBox.Dispatcher.CheckAccess()) CleanupUi();
                else await scope.TextBox.Dispatcher.InvokeAsync(CleanupUi).Task.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
                lock (stateLock)
                {
                    output.Clear();
                    outputScheduled = false;
                }
            }

            try { await scope.EventWorker.ShutdownAsync().ConfigureAwait(false); }
            catch (Exception exception) { failures.Add(exception); }

            if (ownedReadersExited != null)
            {
                try { await ownedReadersExited.Task.ConfigureAwait(false); }
                catch (Exception exception) { failures.Add(exception); }
            }

            if (registration.HasValue)
            {
                try { await registration.Value.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception) { failures.Add(exception); }
            }

            TryCleanup(() => ownedInputAvailable?.Dispose(), failures);
            TryCleanup(() => ownedStopRequested?.Dispose(), failures);
            TryCleanup(() => ownedDisposeRequested?.Dispose(), failures);
            while (uiFailures.TryDequeue(out var uiFailure)) failures.Add(uiFailure);
        }
        finally
        {
            Release(scope);
        }

        if (completion != null)
        {
            if (failures.Count == 0) completion.TrySetResult();
            else if (failures.Count == 1) completion.TrySetException(failures[0]);
            else completion.TrySetException(new AggregateException("Console cleanup failed.", failures));
        }
    }

    private static void Release(ConsoleExecutionScope scope)
    {
        lock (initLock)
        {
            if (!ReferenceEquals(executionScope, scope)) return;
            lock (stateLock)
            {
                ClearUserEvents();
                input.Clear();
                output.Clear();
                textWriter = null;
                textReader = null;
                inputAvailable = null;
                stopRequested = null;
                disposeRequested = null;
                readSignals = null;
                stopRegistration = null;
                readersExited = null;
                activeRead = null;
                uiRead = null;
                outputScheduled = false;
                readerCount = 0;
            }
            Volatile.Write(ref executionScope, null);
        }
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void DrainUiFailures()
    {
        while (uiFailures.TryDequeue(out _)) { }
    }

    private static void TryCleanup(Action action, List<Exception> failures)
    {
        try { action(); }
        catch (Exception exception) { failures.Add(exception); }
    }
}
