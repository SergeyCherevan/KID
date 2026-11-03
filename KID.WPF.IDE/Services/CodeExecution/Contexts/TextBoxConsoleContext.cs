using KID.Services.CodeExecution.Contexts.Interfaces;
using System.IO;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts;

/// <summary>Supplies the IDE console to the shared runtime owner, including stream restoration.</summary>
public sealed class TextBoxConsoleContext : IConsoleContext
{
    private readonly Lock gate = new();
    private readonly Action<TextWriter, TextReader, TextWriter>? redirectStreams;
    private RuntimeSessionOwner? owner;
    private TextBox? initializedTarget;
    private bool initializationFailed;
    private bool cleanupStarted;

    public object ConsoleTarget { get; set; }

    public TextBoxConsoleContext(TextBox textBox)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ConsoleTarget = textBox;
    }

    internal TextBoxConsoleContext(TextBox textBox, Action<TextWriter, TextReader, TextWriter> redirectStreams)
        : this(textBox) => this.redirectStreams = redirectStreams ?? throw new ArgumentNullException(nameof(redirectStreams));

    public void Init(long executionId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(cleanupStarted, this);
            if (initializedTarget != null)
            {
                if (owner!.Environment.ExecutionId == executionId && owner.CancellationToken == cancellationToken &&
                    ReferenceEquals(initializedTarget, ConsoleTarget)) return;
                throw new InvalidOperationException("Console context is already initialized for another session or UI target.");
            }
            if (initializationFailed)
                throw new InvalidOperationException("Console context cannot be initialized after a partial initialization failure.");
            if (ConsoleTarget is not TextBox textBox)
                throw new InvalidOperationException("ConsoleTarget must be a TextBox.");
            textBox.VerifyAccess();
            var runtime = RuntimeSessionOwner.GetCurrent(executionId);
            if (runtime.CancellationToken != cancellationToken)
                throw new InvalidOperationException("Console context cancellation token does not match the current execution.");
            var hadConsole = TextBoxConsole.CurrentScope != null;
            try
            {
                runtime.Initialize(new KIDRuntimeOptions { ConsoleTextBox = textBox, RedirectSystemConsole = true }, redirectStreams);
                owner = runtime;
                initializedTarget = textBox;
            }
            catch
            {
                if (!hadConsole && ReferenceEquals(TextBoxConsole.CurrentScope?.Environment, runtime.Environment))
                    owner = runtime;
                initializationFailed = true;
                throw;
            }
        }
    }

    public void BeginCleanup()
    {
        lock (gate)
        {
            if (cleanupStarted) return;
            cleanupStarted = true;
            owner?.BeginCleanup();
        }
    }

    public ValueTask DisposeAsync()
    {
        BeginCleanup();
        return owner?.CleanupAsync() ?? ValueTask.CompletedTask;
    }
}
