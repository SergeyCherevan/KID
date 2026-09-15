using KID.Services.Errors;
using System.IO;
using System.Windows.Controls;
using KID.Services.CodeExecution.Console;
using KID.Services.CodeExecution.Contexts.Interfaces;

namespace KID.Services.CodeExecution.Contexts;

/// <summary>
/// Владеет перенаправлением System.Console одной сессии. Cleanup ожидает readers,
/// отписки и публикацию принятого вывода, затем восстанавливает исходные потоки.
/// </summary>
public sealed class TextBoxConsoleContext : IConsoleContext
{
    private readonly object lifecycleLock = new();
    private readonly Action<TextBoxConsole> redirectStreams;
    private TextWriter? originalConsoleOut;
    private TextReader? originalConsoleIn;
    private TextWriter? originalConsoleError;
    private TextBoxConsole? textBoxConsole;
    private bool initialized;
    private bool initializationFailed;
    private bool cleanupStarted;
    private readonly ExecutionFailureCollector beginCleanupFailures = new();
    private readonly TaskCompletionSource disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposeStarted;
    private long initializedExecutionId;
    private CancellationToken initializedCancellationToken;
    private object? initializedTarget;

    public object ConsoleTarget { get; set; }

    public TextBoxConsoleContext(TextBox textBox) : this(textBox, console =>
    {
        System.Console.SetOut(console.Out);
        System.Console.SetIn(console.In);
        System.Console.SetError(console.Error);
    })
    {
    }

    // Тестовая точка ошибки посередине перенаправления без замены глобального Console API.
    internal TextBoxConsoleContext(TextBox textBox, Action<TextBoxConsole> redirectStreams)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(redirectStreams);
        ConsoleTarget = textBox;
        this.redirectStreams = redirectStreams;
    }

    /// <summary>
    /// Идемпотентный для той же session identity Init; исходные потоки доступны cleanup
    /// даже при частичной ошибке.
    /// </summary>
    public void Init(long executionId, CancellationToken cancellationToken)
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposeStarted || cleanupStarted, this);
            if (initialized)
            {
                if (initializedExecutionId == executionId &&
                    initializedCancellationToken == cancellationToken &&
                    ReferenceEquals(initializedTarget, ConsoleTarget))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Console context is already initialized for another session or UI target.");
            }
            if (initializationFailed)
            {
                throw new InvalidOperationException(
                    "Console context cannot be initialized after a partial initialization failure.");
            }
            if (ConsoleTarget is not TextBox textBox)
                throw new InvalidOperationException("ConsoleTarget must be a TextBox.");
            textBox.Dispatcher.VerifyAccess();
            initializedExecutionId = executionId;
            initializedCancellationToken = cancellationToken;
            initializedTarget = textBox;
            originalConsoleOut = System.Console.Out;
            originalConsoleIn = System.Console.In;
            originalConsoleError = System.Console.Error;
            textBoxConsole = new TextBoxConsole(textBox, executionId, cancellationToken);
            try
            {
                redirectStreams(textBoxConsole);
                initialized = true;
            }
            catch
            {
                initializationFailed = true;
                throw;
            }
        }
    }

    public void BeginCleanup()
    {
        lock (lifecycleLock)
        {
            if (cleanupStarted)
                return;

            cleanupStarted = true;
            beginCleanupFailures.Capture(() => textBoxConsole?.BeginCleanup());
        }
    }

    /// <summary>
    /// Ожидает одну общую очистку. Отменённый session token не отменяет восстановление
    /// потоков, а ошибка адаптера не пропускает попытку восстановить каждый из них.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        BeginCleanup();
        lock (lifecycleLock)
        {
            if (disposeStarted) return new ValueTask(disposeCompletion.Task);
            disposeStarted = true;
        }
        // Completion опубликована до вызова callback: повторный Dispose видит ту же task.
        _ = DisposeCoreAsync();
        return new ValueTask(disposeCompletion.Task);
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new ExecutionFailureCollector();
        beginCleanupFailures.DrainTo(failures);
        await failures.CaptureAsync(
            () => textBoxConsole?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            finallyAction: () =>
            {
                if (originalConsoleOut != null) failures.Capture(() => System.Console.SetOut(originalConsoleOut));
                if (originalConsoleIn != null) failures.Capture(() => System.Console.SetIn(originalConsoleIn));
                if (originalConsoleError != null) failures.Capture(() => System.Console.SetError(originalConsoleError));
                textBoxConsole = null;
                originalConsoleOut = null;
                originalConsoleIn = null;
                originalConsoleError = null;
            });
        var failure = failures.CreateException("Console streams cleanup failed.");
        if (failure == null) disposeCompletion.TrySetResult();
        else disposeCompletion.TrySetException(failure);
    }
}
