namespace KID;

public static partial class KIDConsole
{
    /// <summary>Публикует один UTF-16 символ в консоли текущей execution-сессии.</summary>
    public static void Write(char value) => Write(value.ToString());

    /// <summary>Публикует строковый фрагмент в консоли текущей execution-сессии.</summary>
    public static void Write(string? value)
    {
        var scope = GetActiveScope();
        if (scope != null) Write(scope, value);
    }

    /// <summary>Очищает консоль текущей execution-сессии.</summary>
    public static void Clear()
    {
        var scope = GetActiveScope();
        if (scope != null) Clear(scope);
    }

    private static void Write(ConsoleExecutionScope scope, string? value)
    {
        if (value == null) return;
        EnqueueOutput(
            scope,
            () =>
            {
                scope.TextBox.AppendText(value);
                scope.TextBox.ScrollToEnd();
            },
            value);
    }

    private static void Clear(ConsoleExecutionScope scope) =>
        EnqueueOutput(scope, scope.TextBox.Clear, notification: null);

    private static void EnqueueOutput(
        ConsoleExecutionScope scope,
        Action action,
        string? notification = null)
    {
        lock (stateLock)
        {
            if (!IsActive(scope)) return;
            output.Enqueue(new OutputWorkItem(scope, action, notification));
            if (outputScheduled) return;
            outputScheduled = true;
        }

        PostUi(scope, () => DrainOutput(scope, duringCleanup: false));
    }

    private static void DrainOutput(ConsoleExecutionScope scope, bool duringCleanup)
    {
        while (true)
        {
            OutputWorkItem item;
            lock (stateLock)
            {
                if (!Owns(scope) || (closing && !duringCleanup))
                {
                    if (!Owns(scope)) output.Clear();
                    outputScheduled = false;
                    return;
                }

                if (!output.TryDequeue(out item!))
                {
                    outputScheduled = false;
                    return;
                }
            }

            if (!ReferenceEquals(item.Scope, scope) || !Owns(scope)) continue;
            try
            {
                item.Action();
                if (!duringCleanup && item.Notification != null)
                    EnqueueOutputReceived(scope, item.Notification);
            }
            catch (Exception exception)
            {
                uiFailures.Enqueue(exception);
                BeginCleanup(scope.Environment);
            }
        }
    }

    private sealed record OutputWorkItem(
        ConsoleExecutionScope Scope,
        Action Action,
        string? Notification);
}
