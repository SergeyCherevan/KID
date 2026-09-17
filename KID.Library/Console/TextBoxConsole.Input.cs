using System.Text;
using System.Windows;
using System.Windows.Input;

namespace KID;

public static partial class TextBoxConsole
{
    /// <summary>Синхронно ожидает один UTF-16 code unit текущей execution-сессии.</summary>
    public static int Read()
    {
        var scope = GetActiveScope() ??
            throw new InvalidOperationException("No console execution is active.");
        return Read(scope);
    }

    /// <summary>Синхронно читает строку до Enter в текущей execution-сессии.</summary>
    public static string ReadLine()
    {
        var scope = GetActiveScope() ??
            throw new InvalidOperationException("No console execution is active.");
        return ReadLine(scope);
    }

    private static int Read(ConsoleExecutionScope scope) => ReadCore(scope, readLine: false)[0];
    private static string ReadLine(ConsoleExecutionScope scope) => ReadCore(scope, readLine: true);

    private static string ReadCore(ConsoleExecutionScope scope, bool readLine)
    {
        if (scope.TextBox.Dispatcher.CheckAccess())
            throw new InvalidOperationException("Console input must run outside the UI thread.");

        lock (stateLock)
        {
            ThrowIfStopped(scope);
            readerCount++;
        }

        try
        {
            lock (readLock)
            {
                var request = new ReadRequest(scope);
                try
                {
                    lock (stateLock)
                    {
                        ThrowIfStopped(scope);
                        activeRead = request;
                    }

                    PostUi(scope, () => BeginReadUi(request));
                    var result = new StringBuilder();
                    do
                    {
                        var symbol = ReadCharacter(scope);
                        if (readLine && symbol == '\b')
                        {
                            if (result.Length > 0)
                            {
                                result.Length--;
                                EnqueueOutput(scope, () =>
                                {
                                    if (scope.TextBox.Text.Length > 0)
                                        scope.TextBox.Text = scope.TextBox.Text[..^1];
                                    scope.TextBox.CaretIndex = scope.TextBox.Text.Length;
                                });
                            }
                            continue;
                        }

                        Write(scope, symbol.ToString());
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
                        if (ReferenceEquals(activeRead, request)) activeRead = null;
                        if (!IsActive(scope) || scope.Environment.CancellationToken.IsCancellationRequested)
                            input.Clear();
                    }

                    PostUi(scope, () => RestoreReadUi(request));
                }
            }
        }
        finally
        {
            lock (stateLock)
            {
                readerCount--;
                if (readerCount == 0 && closing) readersExited?.TrySetResult();
            }
        }
    }

    private static char ReadCharacter(ConsoleExecutionScope scope)
    {
        while (true)
        {
            WaitHandle[] signals;
            lock (stateLock)
            {
                ThrowIfStopped(scope);
                if (input.TryDequeue(out var symbol)) return symbol;
                signals = readSignals ?? throw new ObjectDisposedException(nameof(TextBoxConsole));
            }

            WaitHandle.WaitAny(signals);
        }
    }

    private static void ThrowIfStopped(ConsoleExecutionScope scope)
    {
        scope.Environment.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(!IsActive(scope), nameof(TextBoxConsole));
    }

    private static bool ReceiveInput(ConsoleExecutionScope scope, object sender, string text)
    {
        if (!ReferenceEquals(sender, scope.TextBox)) return false;
        lock (stateLock)
        {
            if (!IsActive(scope) || activeRead?.Scope != scope || text.Length == 0)
                return false;

            foreach (var symbol in text) input.Enqueue(symbol);
            inputAvailable?.Set();
            return true;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var scope = GetActiveScope();
        if (scope != null && ReceiveInput(scope, sender, e.Text)) e.Handled = true;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var scope = GetActiveScope();
        if (scope == null) return;
        var text = e.Key switch { Key.Enter => "\n", Key.Back => "\b", Key.Space => " ", _ => "" };
        if (ReceiveInput(scope, sender, text)) e.Handled = true;
    }

    private static void BeginReadUi(ReadRequest request)
    {
        var scope = request.Scope;
        lock (stateLock)
        {
            if (!IsActive(scope) || !ReferenceEquals(activeRead, request)) return;

            request.WasReadOnly = scope.TextBox.IsReadOnly;
            request.KeyboardFocus = global::System.Windows.Input.Keyboard.FocusedElement;
            request.FocusScope = FocusManager.GetFocusScope(scope.TextBox);
            request.LogicalFocus = FocusManager.GetFocusedElement(request.FocusScope);
            uiRead = request;
            scope.TextBox.IsReadOnly = false;
            scope.TextBox.Focus();
            global::System.Windows.Input.Keyboard.Focus(scope.TextBox);
            FocusManager.SetFocusedElement(request.FocusScope, scope.TextBox);
            scope.TextBox.CaretIndex = scope.TextBox.Text.Length;
            scope.TextBox.ScrollToEnd();
        }
    }

    private static void RestoreReadUi(ReadRequest request)
    {
        var scope = request.Scope;
        if (!ReferenceEquals(uiRead, request)) return;
        uiRead = null;
        if (!Owns(scope)) return;

        scope.TextBox.IsReadOnly = request.WasReadOnly;
        if (request.FocusScope != null &&
            FocusManager.GetFocusedElement(request.FocusScope) == scope.TextBox)
        {
            FocusManager.SetFocusedElement(request.FocusScope, request.LogicalFocus);
        }
        if (global::System.Windows.Input.Keyboard.FocusedElement == scope.TextBox)
            global::System.Windows.Input.Keyboard.Focus(request.KeyboardFocus);
    }

    private sealed class ReadRequest(ConsoleExecutionScope scope)
    {
        internal ConsoleExecutionScope Scope { get; } = scope;
        internal bool WasReadOnly { get; set; }
        internal IInputElement? KeyboardFocus { get; set; }
        internal DependencyObject? FocusScope { get; set; }
        internal IInputElement? LogicalFocus { get; set; }
    }
}
