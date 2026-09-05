using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KID.Services.Interfaces;
using AsyncManualResetEvent = Microsoft.VisualStudio.Threading.AsyncManualResetEvent;

namespace KID.Services.CodeExecution;

/// <summary>
/// Консоль одного запуска. Ввод ожидает символ, Stop или Dispose; UI-команды
/// проверяют владельца перед выполнением на Dispatcher своего контрола.
/// </summary>
public sealed class TextBoxConsole : IConsole, IDisposable, IAsyncDisposable
{
    private readonly TextBox textBox;
    private readonly CancellationToken cancellationToken;
    private readonly object stateLock = new();
    private readonly object readLock = new();
    private readonly Queue<char> input = new();
    private readonly Queue<Action> output = new();
    private readonly ExecutionFailureCollector uiFailures = new();
    private readonly AutoResetEvent inputAvailable;
    private readonly ManualResetEvent stopRequested;
    private readonly ManualResetEvent disposeRequested;
    private readonly WaitHandle[] readSignals;
    private readonly CancellationTokenRegistration stopRegistration;
    private readonly AsyncManualResetEvent readersExited = new();
    private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TextWriter textWriter;
    private TextReader textReader;
    private bool isDisposed;
    private int readerCount;
    private ReadRequest? activeRead;
    private ReadRequest? uiRead;
    private bool outputScheduled;

    /// <summary>Неизменяемый id execution-сессии, переданный host.</summary>
    public long ExecutionId { get; }

    /// <summary>Текст, уже опубликованный на UI-потоке.</summary>
    public event EventHandler<string>? OutputReceived;

    /// <summary>Создаёт адаптер на UI-потоке с token и id текущей сессии.</summary>
    public TextBoxConsole(TextBox textBox, long executionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(executionId);
        textBox.Dispatcher.VerifyAccess();
        inputAvailable = new AutoResetEvent(false);
        stopRequested = new ManualResetEvent(false);
        disposeRequested = new ManualResetEvent(false);
        this.textBox = textBox;
        ExecutionId = executionId;
        this.cancellationToken = cancellationToken;
        textWriter = new TextBoxTextWriter(this);
        textReader = new TextBoxTextReader(this);
        readSignals = [stopRequested, disposeRequested, inputAvailable];

        /* Callback не обращается к WPF и не ожидает reader. Собственный сигнал позволяет
         * освободить регистрацию до wait handles, не закрывая WaitHandle чужого CTS.
         */
        stopRegistration = cancellationToken.Register(() => stopRequested.Set());
        textBox.PreviewKeyDown += OnPreviewKeyDown;
        textBox.PreviewTextInput += OnPreviewTextInput;
        StaticConsole.Init(this);
    }

    public TextWriter Out { get => textWriter; set => textWriter = value ?? throw new ArgumentNullException(nameof(value)); }
    public TextReader In { get => textReader; set => textReader = value ?? throw new ArgumentNullException(nameof(value)); }
    public TextWriter Error { get => textWriter; set => textWriter = value ?? throw new ArgumentNullException(nameof(value)); }

    public void Write(char value) => Write(value.ToString());

    public void Write(string? value)
    {
        if (value == null) return;
        EnqueueOutput(() =>
        {
            textBox.AppendText(value);
            textBox.ScrollToEnd();
            OutputReceived?.Invoke(this, value);
        });
    }

    public void Clear() => EnqueueOutput(textBox.Clear);

    /// <summary>Читает UTF-16 символ; Stop выбрасывает отмену с исходным session token.</summary>
    public int Read() => ReadCore(readLine: false)[0];

    /// <summary>Читает строку до Enter, поддерживая Backspace и многосимвольный ввод.</summary>
    public string ReadLine() => ReadCore(readLine: true);

    private string ReadCore(bool readLine)
    {
        if (textBox.Dispatcher.CheckAccess())
            throw new InvalidOperationException("Console input must run outside the UI thread.");

        lock (stateLock)
        {
            ThrowIfStopped();
            readerCount++;
        }

        try
        {
            /* Reader, ожидающий readLock, тоже учтён в readerCount: Dispose не закрывает
             * сигналы до выхода как активного чтения, так и его конкурентов.
             */
            lock (readLock)
            {
                var request = new ReadRequest();
                try
                {
                    lock (stateLock)
                    {
                        ThrowIfStopped();
                        activeRead = request;
                    }
                    PostUi(() => BeginReadUi(request));
                    var result = new StringBuilder();
                    do
                    {
                        var symbol = ReadCharacter();
                        if (readLine && symbol == '\b')
                        {
                            if (result.Length > 0)
                            {
                                result.Length--;
                                EnqueueOutput(() =>
                                {
                                    if (textBox.Text.Length > 0)
                                        textBox.Text = textBox.Text[..^1];
                                    textBox.CaretIndex = textBox.Text.Length;
                                });
                            }
                            continue;
                        }
                        Write(symbol);
                        if (readLine && symbol == '\n') break;
                        result.Append(symbol);
                    }
                    while (readLine);
                    return result.ToString();
                }
                finally
                {
                    lock (stateLock)
                    {
                        activeRead = null;
                        // Непрочитанные символы обычного ввода нужны следующему Read.
                        // При отмене/Dispose они уже не принадлежат будущему запросу.
                        if (isDisposed || cancellationToken.IsCancellationRequested) input.Clear();
                    }
                    /* Cleanup не использует отменённый token. Запоздалый callback
                     * проверит владельца и конкретный запрос чтения.
                     */
                    PostUi(() => RestoreReadUi(request));
                }
            }
        }
        finally
        {
            lock (stateLock)
            {
                if (--readerCount == 0 && isDisposed)
                    readersExited.Set();
            }
        }
    }

    private char ReadCharacter()
    {
        while (true)
        {
            lock (stateLock)
            {
                ThrowIfStopped();
                if (input.TryDequeue(out var symbol)) return symbol;
            }
            WaitHandle.WaitAny(readSignals);
        }
    }

    // Под stateLock; в гонке Stop и Dispose приоритет имеет session cancellation.
    private void ThrowIfStopped()
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(isDisposed || !StaticConsole.IsCurrent(this), this);
    }

    private bool ReceiveInput(string text)
    {
        lock (stateLock)
        {
            if (isDisposed || cancellationToken.IsCancellationRequested ||
                activeRead == null || !StaticConsole.IsCurrent(this) || text.Length == 0)
                return false;
            foreach (var symbol in text) input.Enqueue(symbol);
            inputAvailable.Set();
            return true;
        }
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (ReceiveInput(e.Text)) e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var text = e.Key switch { Key.Enter => "\n", Key.Back => "\b", Key.Space => " ", _ => "" };
        if (ReceiveInput(text)) e.Handled = true;
    }

    private void BeginReadUi(ReadRequest request)
    {
        lock (stateLock)
        {
            if (isDisposed || cancellationToken.IsCancellationRequested ||
                activeRead != request || !StaticConsole.IsCurrent(this)) return;
            request.WasReadOnly = textBox.IsReadOnly;
            request.KeyboardFocus = global::System.Windows.Input.Keyboard.FocusedElement;
            request.FocusScope = FocusManager.GetFocusScope(textBox);
            request.LogicalFocus = FocusManager.GetFocusedElement(request.FocusScope);
            uiRead = request;
            textBox.IsReadOnly = false;
            textBox.Focus();
            global::System.Windows.Input.Keyboard.Focus(textBox);
            FocusManager.SetFocusedElement(request.FocusScope, textBox);
            textBox.CaretIndex = textBox.Text.Length;
            textBox.ScrollToEnd();
        }
    }

    private void RestoreReadUi(ReadRequest request)
    {
        if (uiRead != request) return;
        uiRead = null;
        if (!StaticConsole.IsCurrent(this)) return;
        textBox.IsReadOnly = request.WasReadOnly;
        if (request.FocusScope != null && FocusManager.GetFocusedElement(request.FocusScope) == textBox)
            FocusManager.SetFocusedElement(request.FocusScope, request.LogicalFocus);
        if (global::System.Windows.Input.Keyboard.FocusedElement == textBox)
            global::System.Windows.Input.Keyboard.Focus(request.KeyboardFocus);
    }

    private void PostUi(Action action)
    {
        void InvokeSafely()
        {
            if (!uiFailures.Capture(action))
            {
                // Ошибка WPF/callback должна разбудить reader и дойти до coordinator,
                // а не остаться необработанным исключением Dispatcher.
                Dispose();
            }
        }
        if (textBox.Dispatcher.CheckAccess()) InvokeSafely();
        else _ = textBox.Dispatcher.InvokeAsync(InvokeSafely, DispatcherPriority.Normal);
    }

    private void EnqueueOutput(Action action)
    {
        lock (stateLock)
        {
            if (isDisposed || !StaticConsole.IsCurrent(this)) return;
            output.Enqueue(action);
            if (outputScheduled) return;
            outputScheduled = true;
        }
        PostUi(() => DrainOutput(duringCleanup: false));
    }

    private void DrainOutput(bool duringCleanup)
    {
        while (true)
        {
            Action action;
            lock (stateLock)
            {
                if (!StaticConsole.IsCurrent(this) || (isDisposed && !duringCleanup))
                {
                    // DisposeAsync допечатает принятый вывод до передачи UI новой сессии.
                    if (!StaticConsole.IsCurrent(this)) output.Clear();
                    outputScheduled = false;
                    return;
                }
                if (!output.TryDequeue(out action!))
                {
                    outputScheduled = false;
                    return;
                }
            }
            action();
        }
    }

    /// <summary>
    /// Запрещает новые операции и пробуждает reader. Host ожидает полную очистку через
    /// DisposeAsync; синхронный вызов не блокирует UI, нужный для завершения чтения.
    /// </summary>
    public void Dispose()
    {
        lock (stateLock)
        {
            if (isDisposed) return;
            isDisposed = true;
            disposeRequested.Set();
            if (readerCount == 0) readersExited.Set();
        }
        _ = CompleteDisposeAsync();
    }

    /// <summary>Ожидает одну общую очистку, в том числе при конкурентных вызовах.</summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return new ValueTask(disposed.Task);
    }

    private async Task CompleteDisposeAsync()
    {
        var failures = new ExecutionFailureCollector();
        uiFailures.DrainTo(failures);
        await failures.CaptureAsync(
            async () =>
            {
                void CleanupUi()
                {
                    textBox.PreviewKeyDown -= OnPreviewKeyDown;
                    textBox.PreviewTextInput -= OnPreviewTextInput;
                    failures.Capture(() => DrainOutput(duringCleanup: true));
                    if (uiRead != null) failures.Capture(() => RestoreReadUi(uiRead));
                    OutputReceived = null;
                    StaticConsole.Release(this);
                    lock (stateLock) output.Clear();
                }

                if (textBox.Dispatcher.CheckAccess()) CleanupUi();
                else await textBox.Dispatcher.InvokeAsync(CleanupUi).Task.ConfigureAwait(false);
            },
            catchAction: () =>
            {
                OutputReceived = null;
                StaticConsole.Release(this);
                lock (stateLock) output.Clear();
            });

        await failures.CaptureAsync(async () =>
        {
            await readersExited.WaitAsync().ConfigureAwait(false);
            // После отписки UI и выхода readers остаётся лишь возможный callback отмены.
            await stopRegistration.DisposeAsync().ConfigureAwait(false);
        });
        failures.Capture(inputAvailable.Dispose);
        failures.Capture(stopRequested.Dispose);
        failures.Capture(disposeRequested.Dispose);
        uiFailures.DrainTo(failures);
        var exceptionToReport = failures.CreateException("Console cleanup failed.");
        if (exceptionToReport == null) disposed.TrySetResult();
        else disposed.TrySetException(exceptionToReport);
    }

    private sealed class ReadRequest
    {
        public bool WasReadOnly { get; set; }
        public IInputElement? KeyboardFocus { get; set; }
        public DependencyObject? FocusScope { get; set; }
        public IInputElement? LogicalFocus { get; set; }
    }

    private sealed class TextBoxTextWriter(TextBoxConsole console) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) => console.Write(value);
        public override void Write(string? value) => console.Write(value);
    }

    private sealed class TextBoxTextReader(TextBoxConsole console) : TextReader
    {
        public override int Read() => console.Read();
        public override string ReadLine() => console.ReadLine();
    }

    /// <summary>Мост для Console.Clear; сменой владельца управляет host.</summary>
    public static class StaticConsole
    {
        private static TextBoxConsole? current;
        internal static void Init(TextBoxConsole console) => Volatile.Write(ref current, console);
        internal static bool IsCurrent(TextBoxConsole console) => ReferenceEquals(Volatile.Read(ref current), console);
        internal static void Release(TextBoxConsole console)
        {
            var owner = Volatile.Read(ref current);
            if (owner?.ExecutionId == console.ExecutionId)
                Interlocked.CompareExchange(ref current, null, console);
        }
        public static void Clear() => Volatile.Read(ref current)?.Clear();
    }
}

