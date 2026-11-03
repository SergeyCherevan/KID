using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KID.StandaloneDemo;

internal sealed class MainWindow : Window
{
    private readonly Canvas canvasA = new() { Background = Brushes.WhiteSmoke, Focusable = true };
    private readonly Canvas canvasB = new() { Background = Brushes.AliceBlue };
    private readonly TextBox console = new() { IsReadOnly = true, AcceptsReturn = true, Height = 110 };
    private readonly TextBlock status = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock input = new() { Margin = new Thickness(8), Text = "Mouse: A only. Keyboard: this window." };
    private readonly StackPanel buttons = new() { Orientation = Orientation.Horizontal };
    private readonly bool withConsole;
    private readonly bool verify;
    private readonly TextWriter originalOut = Console.Out;
    private readonly TextReader originalIn = Console.In;
    private readonly TextWriter originalError = Console.Error;
    private KIDRuntimeSession? session;
    private Task initialization = Task.CompletedTask;
    private Task work = Task.CompletedTask;
    private bool closing;
    private bool cleanupCompleted;
    private int additionalDrawCount;

    internal int ExitCode { get; private set; }

    internal MainWindow(bool withConsole, bool verify)
    {
        this.withConsole = withConsole;
        this.verify = verify;
        Title = "KID.Library — standalone A → B → A";
        Width = 900;
        Height = withConsole ? 600 : 460;
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;
        AddDocked(root, buttons, Dock.Top);
        AddDocked(root, status, Dock.Bottom);
        AddDocked(root, input, Dock.Bottom);
        if (withConsole) AddDocked(root, console, Dock.Bottom);
        var fields = new Grid();
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        fields.Children.Add(new GroupBox { Header = "A · mouse source", Content = canvasA, Margin = new Thickness(4) });
        var fieldB = new GroupBox { Header = "B · graphics only", Content = canvasB, Margin = new Thickness(4) };
        Grid.SetColumn(fieldB, 1);
        fields.Children.Add(fieldB);
        root.Children.Add(fields);

        AddButton("Draw A → B → A", () => DrawSequence(80 * Interlocked.Increment(ref additionalDrawCount)));
        AddButton("Tone 440 Hz", () => Music.Sound(440, 500));
        if (withConsole) AddButton("Read line", () =>
        {
            Console.Write("Your name: ");
            Console.WriteLine("Hello, " + Console.ReadLine());
        });
        var stop = new Button { Content = "STOP", Margin = new Thickness(4), Padding = new Thickness(8) };
        stop.Click += (_, _) =>
        {
            session?.RequestStop();
            buttons.IsEnabled = false;
            status.Text = "STOP requested. Close the window to await work and release the session.";
        };
        buttons.Children.Add(stop);
        buttons.IsEnabled = false;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            if (verify && ExitCode == 0 && cleanupCompleted)
                originalOut.WriteLine($"PASS: visible WPF lifecycle (console={withConsole})");
        };
    }

    private static void AddDocked(DockPanel root, UIElement child, Dock dock)
    {
        DockPanel.SetDock(child, dock);
        root.Children.Add(child);
    }

    private void AddButton(string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(4), Padding = new Thickness(8) };
        button.Click += async (_, _) =>
        {
            // Serialize callers: SetCanvas + drawing is not a transaction across different Tasks.
            if (closing || !work.IsCompleted || session == null || session.CancellationToken.IsCancellationRequested) return;
            work = Task.Run(action, session.CancellationToken);
            await ObserveWorkAsync();
        };
        buttons.Children.Add(button);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (closing) return;
        initialization = InitializeAsync();
        try
        {
            await initialization;
            if (verify && !closing)
            {
                Require(IsLoaded && IsVisible, "The Window did not reach its visible Loaded state.");
                Require(canvasA.Children.Count == 2 && canvasB.Children.Count == 1, "The initial A → B → A pass lost its targets.");
                Require(ReferenceEquals(Graphics.Canvas, canvasA), "The selected Canvas is not A.");
                Require(ReferenceEquals(Console.Out, originalOut) != withConsole, "Unexpected console redirection.");
                Require(session!.CancellationToken == StopManager.CurrentToken, "Tokens differ.");
                DrawSequence(80);
                Require(canvasA.Children.Count == 4 && canvasB.Children.Count == 2, "The repeated draw was not visible on both fields.");
                Require(ReferenceEquals(Graphics.Canvas, canvasA), "The repeated draw did not finish on A.");
                // Exercise Closing while host work is still active, without relying on a delay.
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var token = session.CancellationToken;
                work = Task.Run(async () =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                });
                await started.Task;
            }
        }
        catch (OperationCanceledException) when (session?.CancellationToken.IsCancellationRequested == true) { }
        catch (Exception exception) { ReportFailure(exception); }
        finally
        {
            if (verify && !closing)
            {
                Close();
                if (!cleanupCompleted) Close(); // Reentrant requests share the pending cleanup.
            }
        }
    }

    private async Task InitializeAsync()
    {
        // Open on the controls' UI thread. No IDE, compiler, internals or hand-made execution id.
        session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions
        {
            Canvas = canvasA,
            MouseCanvas = canvasA, // Fixed for the session, even while Graphics selects B.
            KeyboardWindow = this,
            EnableMusic = true,
            ConsoleTextBox = withConsole ? console : null,
            RedirectSystemConsole = withConsole,
            DiagnosticSink = diagnostic => originalError.WriteLine(
                $"{diagnostic.ExecutionId} {diagnostic.Component}/{diagnostic.Operation}: {diagnostic.Exception.Message}")
        });
        if (closing) return;
        // Input handlers run on workers; only the host's own UI update needs Dispatcher.
        Mouse.MouseMoveEvent += cursor => PostInput($"Mouse on A: {cursor.Position}; buttons: {cursor.PressedButton}");
        Keyboard.KeyDownEvent += key => PostInput($"Key in this window: {key.Key}; modifiers: {key.Modifiers}");
        work = Task.Run(() => DrawSequence(), session.CancellationToken);
        await work;
        if (closing) return;
        buttons.IsEnabled = true;
        status.Text = "A has two circles; B has one rectangle. Tone plays on demand. Close awaits cleanup.";
    }

    private void DrawSequence(int offset = 0)
    {
        session!.CancellationToken.ThrowIfCancellationRequested();
        Graphics.SetCanvas(canvasA);
        Graphics.Circle(60 + offset, 70 + offset, 25);
        Graphics.SetCanvas(canvasB);
        Graphics.Rectangle(40 + offset, 45 + offset, 90, 60);
        Graphics.SetCanvas(canvasA);
        Graphics.Circle(140 + offset, 110 + offset, 35);
        if (withConsole) Console.WriteLine("Drawn A → B → A; both fields retain their content.");
        Dispatcher.BeginInvoke(() =>
        {
            if (!closing) status.Text = $"A has {canvasA.Children.Count} circles; B has {canvasB.Children.Count} rectangles. Last draw offset: {offset}.";
        });
    }

    private void PostInput(string text) => Dispatcher.BeginInvoke(() =>
    {
        if (!closing) input.Text = text;
    });

    private async Task ObserveWorkAsync()
    {
        try { await work; }
        catch (OperationCanceledException) when (session?.CancellationToken.IsCancellationRequested == true) { }
        catch (Exception exception) { ReportFailure(exception); }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (cleanupCompleted) return;
        e.Cancel = true; // Keep the window and Dispatcher alive across all awaits.
        if (closing) return;
        closing = true;
        buttons.IsEnabled = false;
        status.Text = "Stopping work and awaiting DisposeAsync…";
        try
        {
            session?.RequestStop();
            try { await initialization; }
            catch (OperationCanceledException) when (session?.CancellationToken.IsCancellationRequested == true) { }
            catch (Exception exception) { ReportFailure(exception); }
            session?.RequestStop(); // Also covers a close during OpenAsync.
            await ObserveWorkAsync(); // The host owns its Tasks; DisposeAsync does not await them.
            if (session != null) await session.DisposeAsync();
            if (verify)
            {
                try
                {
                    Require(work.IsCompleted, "Host work was not awaited.");
                    Require(!Dispatcher.HasShutdownStarted, "Dispatcher stopped before cleanup.");
                    Require(ReferenceEquals(Console.Out, originalOut) && ReferenceEquals(Console.In, originalIn) &&
                        ReferenceEquals(Console.Error, originalError), "Console streams were not restored.");
                    Require(Graphics.Canvas == null && StopManager.CurrentToken == default, "Runtime is still published.");
                }
                catch (Exception exception) { ReportFailure(exception); }
                // Keep the Dispatcher alive if cleanup of this verification session fails too.
                await using (var reopened = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions { Canvas = canvasB }))
                    Graphics.Plot(10, 10);
            }
            cleanupCompleted = true;
            // Cleanup can finish synchronously inside Closing; Close must run after that event unwinds.
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Close));
        }
        catch (Exception exception)
        {
            // Do not hide failed cleanup behind shutdown or attempt a new runtime session.
            ReportFailure(exception);
            status.Text = "Cleanup failed; runtime admission remains blocked. See the terminal. Dispatcher is still alive.";
        }
    }

    private void ReportFailure(Exception exception)
    {
        ExitCode = 1;
        originalError.WriteLine(exception);
        status.Text = exception.Message;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
