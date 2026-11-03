using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace KID;

/// <summary>
/// Shared library lifecycle. CleanupAsync releases library resources; CompleteAsync additionally
/// finalizes host cleanup and admission. The IDE can retain the reservation while unloading its ALC.
/// </summary>
internal sealed class RuntimeSessionOwner
{
    private readonly Lock gate = new();
    private readonly Lock stopGate = new();
    private readonly Lock ingressGate = new();
    private readonly CancellationTokenSource stopSource;
    private readonly ExecutionEnvironmentManager.Reservation reservation;
    private readonly ConcurrentQueue<Exception> failures = new();
    private readonly TaskCompletionSource cleaned = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration stopRegistration;
    private TextWriter? originalOut;
    private TextReader? originalIn;
    private TextWriter? originalError;
    private bool cleanupStarted;
    private bool completionStarted;
    private bool finalized;
    private bool ingressClosed;

    private RuntimeSessionOwner(
        CancellationTokenSource stopSource,
        ExecutionEnvironmentManager.Reservation reservation)
    {
        this.stopSource = stopSource;
        this.reservation = reservation;
        CancellationToken = stopSource.Token;
    }

    internal CancellationToken CancellationToken { get; }
    internal ExecutionEnvironment Environment => reservation.Environment;

    internal static async ValueTask<RuntimeSessionOwner> OpenAsync(
        KIDRuntimeOptions options,
        CancellationToken cancellationToken,
        Action<TextWriter, TextReader, TextWriter>? redirectStreams = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = ValidateOptions(options);
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        RuntimeSessionOwner owner;
        try
        {
            Action<ExecutionDiagnostic>? diagnostics = options.DiagnosticSink is { } sink
                ? diagnostic => sink(new KIDRuntimeDiagnostic(
                    diagnostic.ExecutionId, diagnostic.Component, diagnostic.Operation, diagnostic.Exception))
                : null;
            owner = new RuntimeSessionOwner(source,
                ExecutionEnvironmentManager.ReserveExecution(source.Token, diagnostics));
        }
        catch
        {
            source.Dispose();
            throw;
        }

        try
        {
            owner.reservation.Publish();
            owner.stopRegistration = source.Token.Register(owner.CloseIngress);
            owner.Initialize(options, dispatcher, redirectStreams);
            source.Token.ThrowIfCancellationRequested();
            return owner;
        }
        catch (Exception openingFailure)
        {
            owner.Environment.ReportDiagnostic("Runtime", "Open", openingFailure);
            try { await owner.CompleteAsync().ConfigureAwait(false); }
            catch (Exception rollbackFailure)
            {
                throw new AggregateException("Runtime opening and rollback failed.", openingFailure, rollbackFailure);
            }
            throw;
        }
    }

    private static Dispatcher? ValidateOptions(KIDRuntimeOptions options)
    {
        if (options.RedirectSystemConsole && options.ConsoleTextBox == null)
            throw new ArgumentException("Console redirection requires ConsoleTextBox.", nameof(options));

        Dispatcher? dispatcher = null;
        DispatcherObject?[] controls = [options.Canvas, options.MouseCanvas, options.KeyboardWindow, options.ConsoleTextBox];
        foreach (var control in controls)
        {
            if (control == null) continue;
            var candidate = control.Dispatcher;
            if (dispatcher != null && !ReferenceEquals(dispatcher, candidate))
                throw new ArgumentException("All runtime controls must use the same Dispatcher.", nameof(options));
            dispatcher = candidate;
        }
        if (dispatcher != null)
        {
            dispatcher.VerifyAccess();
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                throw new InvalidOperationException("The UI Dispatcher is shutting down.");
        }
        return dispatcher;
    }

    private void Initialize(
        KIDRuntimeOptions options,
        Dispatcher? dispatcher,
        Action<TextWriter, TextReader, TextWriter>? redirectStreams)
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (dispatcher != null)
        {
            var scope = DispatcherManager.AttachDispatcher(Environment.ExecutionId, dispatcher);
            if (options.Canvas != null) Graphics.Init(options.Canvas, scope, options.CanvasSizeHandler);
        }
        if (options.MouseCanvas != null) Mouse.Init(options.MouseCanvas, Environment);
        if (options.KeyboardWindow != null) Keyboard.Init(options.KeyboardWindow, Environment);
        if (options.EnableMusic) Music.Init(Environment);
        if (options.ConsoleTextBox != null)
        {
            var console = TextBoxConsole.Init(options.ConsoleTextBox, Environment);
            if (options.RedirectSystemConsole)
            {
                // Save all three streams before the first mutation, including partial redirection.
                originalOut = System.Console.Out;
                originalIn = System.Console.In;
                originalError = System.Console.Error;
                (redirectStreams ?? RedirectStreams)(
                    TextBoxConsole.GetOut(console), TextBoxConsole.GetIn(console), TextBoxConsole.GetError(console));
            }
        }
    }

    private static void RedirectStreams(TextWriter output, TextReader input, TextWriter error)
    {
        System.Console.SetOut(output);
        System.Console.SetIn(input);
        System.Console.SetError(error);
    }

    internal void RequestStop()
    {
        lock (stopGate)
        {
            if (finalized || stopSource.IsCancellationRequested) return;
            // Set the token before waking console readers, so STOP is observed as cancellation.
            Capture("Runtime", "RequestStop", stopSource.Cancel);
            CloseIngress();
        }
    }

    private void CloseIngress()
    {
        lock (ingressGate)
        {
            if (ingressClosed) return;
            ingressClosed = true;
            Environment.BeginCleanup();
            Capture("Console", "Close", () => TextBoxConsole.BeginCleanup(Environment));
            Capture("Dispatcher", "Close", () => Environment.AttachedDispatcher?.Close());
        }
    }

    internal ValueTask CleanupAsync()
    {
        CloseIngress();
        bool start;
        lock (gate)
        {
            start = !cleanupStarted;
            cleanupStarted = true;
        }
        if (start) _ = CleanupCoreAsync();
        return new ValueTask(cleaned.Task);
    }

    // hostFailure is supplied by the IDE if running-instance/ALC cleanup fails after CleanupAsync.
    internal ValueTask CompleteAsync(Exception? hostFailure = null)
    {
        var cleanup = CleanupAsync();
        bool start;
        lock (gate)
        {
            start = !completionStarted;
            completionStarted = true;
            if (start && hostFailure != null) RecordFailure("Host", "Cleanup", hostFailure);
        }
        if (start) _ = CompleteCoreAsync(cleanup);
        return new ValueTask(completed.Task);
    }

    private async Task CleanupCoreAsync()
    {
        var dispatcher = Environment.AttachedDispatcher;
        // Start independent shutdowns before awaiting any worker, so all inputs are released.
        await Task.WhenAll(
            CaptureAsync("Keyboard", () => Keyboard.ShutdownAsync(Environment)),
            CaptureAsync("Mouse", () => Mouse.ShutdownAsync(Environment)),
            CaptureAsync("Music", () => Music.ShutdownAsync(Environment)),
            CaptureAsync("Console", () => TextBoxConsole.ShutdownAsync(Environment))).ConfigureAwait(false);

        if (originalOut != null) Capture("Console", "RestoreOut", () => System.Console.SetOut(originalOut));
        if (originalIn != null) Capture("Console", "RestoreIn", () => System.Console.SetIn(originalIn));
        if (originalError != null) Capture("Console", "RestoreError", () => System.Console.SetError(originalError));
        originalOut = null;
        originalIn = null;
        originalError = null;

        if (dispatcher != null)
        {
            await CaptureAsync("Dispatcher", () => dispatcher.ShutdownAsync(() => Graphics.Release(dispatcher)))
                .ConfigureAwait(false);
            // Managed references can also be released after an unexpected Dispatcher shutdown.
            Capture("Graphics", "Release", () => Graphics.Release(dispatcher));
        }

        SetResult(cleaned, CreateFailure());
    }

    private async Task CompleteCoreAsync(ValueTask cleanup)
    {
        try { await cleanup.ConfigureAwait(false); }
        catch (Exception) { /* Captured per component; do not duplicate failures. */ }

        await CaptureAsync("Runtime", () => stopRegistration.DisposeAsync()).ConfigureAwait(false);
        lock (stopGate)
        {
            finalized = true;
            Capture("Runtime", "DisposeToken", stopSource.Dispose);
        }
        var failure = CreateFailure();
        reservation.Complete(failure);
        Environment.ClearDiagnosticSink();
        SetResult(completed, failure);
    }

    private void Capture(string component, string operation, Action action)
    {
        try { action(); }
        catch (Exception exception) { RecordFailure(component, operation, exception); }
    }

    private async Task CaptureAsync(string component, Func<ValueTask> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception exception) { RecordFailure(component, "Cleanup", exception); }
    }

    private void RecordFailure(string component, string operation, Exception exception)
    {
        failures.Enqueue(exception);
        Environment.ReportDiagnostic(component, operation, exception);
    }

    private Exception? CreateFailure()
    {
        var errors = failures.ToArray();
        return errors.Length switch
        {
            0 => null,
            1 => errors[0],
            _ => new AggregateException("Runtime cleanup failed.", errors)
        };
    }

    private static void SetResult(TaskCompletionSource completion, Exception? failure)
    {
        if (failure == null) completion.TrySetResult();
        else completion.TrySetException(failure);
    }
}
