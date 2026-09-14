using System.Windows.Threading;

namespace KID;

/// <summary>Направляет пользовательские WPF-операции в scope одного запуска.</summary>
public static class DispatcherManager
{
    [ThreadStatic] private static ExecutionDispatcherScope? executing;

    internal static ExecutionDispatcherScope AttachDispatcher(long executionId, Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return ExecutionEnvironmentManager.AttachDispatcher(executionId, dispatcher);
    }

    internal static bool IsExecuting(ExecutionDispatcherScope scope) => ReferenceEquals(executing, scope);

    internal static void Release(ExecutionDispatcherScope scope) =>
        scope.Environment.ReleaseDispatcher(scope);

    internal static ExecutionDispatcherScope GetScope()
    {
        var scope = executing ?? ExecutionEnvironmentManager.Current?.GetDispatcher()
            ?? throw new InvalidOperationException("No graphics execution is active.");
        scope.CheckAccess(ReferenceEquals(executing, scope));
        return scope;
    }

    internal static T Execute<T>(ExecutionDispatcherScope scope, Func<T> action)
    {
        var previous = executing;
        executing = scope;
        try { return action(); }
        finally { executing = previous; }
    }

    /// <summary>Фоновый caller не ожидает UI. Ошибка принятого действия доставляется через cleanup.</summary>
    public static void InvokeOnUI(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var scope = GetScope();
        if (scope.Dispatcher.CheckAccess())
        {
            _ = InvokeOnUI(() => { action(); return true; });
            return;
        }
        scope.Post(() => { action(); return true; }, reportFailure: true);
    }

    /// <summary>Ожидает результат на worker; Stop освобождает ожидание даже при занятом UI.</summary>
    public static T InvokeOnUI<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        var scope = GetScope();
        return InvokeOnUI(scope, func);
    }

    internal static T InvokeOnUI<T>(ExecutionDispatcherScope scope, Func<T> func)
    {
        scope.CheckAccess(IsExecuting(scope));
        if (scope.Dispatcher.CheckAccess()) return scope.RunInline(func);
        var work = scope.Post(func, reportFailure: false);
        return work.Result.Task.WaitAsync(scope.Environment.CancellationToken).GetAwaiter().GetResult();
    }
}
