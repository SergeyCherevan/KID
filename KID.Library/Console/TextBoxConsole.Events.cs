namespace KID;

public static partial class TextBoxConsole
{
    /// <summary>
    /// Последовательно сообщает в фоне о фрагментах, уже опубликованных в консольном TextBox.
    /// </summary>
    public static event Action<string>? OutputReceived;

    private static void EnqueueOutputReceived(ConsoleExecutionScope scope, string value)
    {
        var handlers = OutputReceived?.GetInvocationList();
        if (handlers == null) return;

        foreach (var handler in handlers.Cast<Action<string>>())
        {
            _ = scope.EventWorker.TryEnqueue(() =>
            {
                if (!IsActive(scope)) return;
                handler(value);
            });
        }
    }

    private static void ClearUserEvents() => OutputReceived = null;
}
