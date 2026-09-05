using KID.Services.CodeExecution.Contexts.Interfaces;
using System.IO;
using System.Windows.Controls;

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
    private readonly TaskCompletionSource disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposeStarted;

    public object ConsoleTarget { get; set; }

    public TextBoxConsoleContext(TextBox textBox) : this(textBox, console =>
    {
        Console.SetOut(console.Out);
        Console.SetIn(console.In);
        Console.SetError(console.Error);
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

    /// <summary>Однократный Init; исходные потоки доступны cleanup даже при частичной ошибке.</summary>
    public void Init(long executionId, CancellationToken cancellationToken)
    {
        lock (lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(disposeStarted, this);
            if (initialized) throw new InvalidOperationException("Console context is already initialized.");
            if (ConsoleTarget is not TextBox textBox)
                throw new InvalidOperationException("ConsoleTarget must be a TextBox.");
            textBox.Dispatcher.VerifyAccess();
            initialized = true;
            originalConsoleOut = Console.Out;
            originalConsoleIn = Console.In;
            originalConsoleError = Console.Error;
            textBoxConsole = new TextBoxConsole(textBox, executionId, cancellationToken);
            redirectStreams(textBoxConsole);
        }
    }

    /// <summary>
    /// Ожидает одну общую очистку. Отменённый session token не отменяет восстановление
    /// потоков, а ошибка адаптера не пропускает попытку восстановить каждый из них.
    /// </summary>
    public ValueTask DisposeAsync()
    {
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
        try
        {
            if (textBoxConsole != null) await textBoxConsole.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            if (originalConsoleOut != null) failures.Capture(() => Console.SetOut(originalConsoleOut));
            if (originalConsoleIn != null) failures.Capture(() => Console.SetIn(originalConsoleIn));
            if (originalConsoleError != null) failures.Capture(() => Console.SetError(originalConsoleError));
            textBoxConsole = null;
            originalConsoleOut = null;
            originalConsoleIn = null;
            originalConsoleError = null;
        }
        var failure = failures.CreateException("Console streams cleanup failed.");
        if (failure == null) disposeCompletion.TrySetResult();
        else disposeCompletion.TrySetException(failure);
    }
}
