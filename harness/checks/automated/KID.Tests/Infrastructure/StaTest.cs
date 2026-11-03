using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;

namespace KID.Tests.Infrastructure;

/// <summary>
/// Runs WPF-focused test code on a private STA thread with a real Dispatcher pump.
/// No visible window is created.
/// </summary>
internal static class StaTest
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static Task RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return RunAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    public static async Task RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource<Exception?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() => RunDispatcher(action, completion))
        {
            IsBackground = true,
            Name = "KID.Tests.STA"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var exception = await completion.Task.WaitAsync(DefaultTimeout).ConfigureAwait(false);
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private static void RunDispatcher(
        Func<Task> action,
        TaskCompletionSource<Exception?> completion)
    {
        Exception? capturedException = null;

        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(dispatcher));

            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                }
                catch (Exception exception)
                {
                    capturedException = exception;
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                }
            });

            Dispatcher.Run();
            completion.TrySetResult(capturedException);
        }
        catch (Exception exception)
        {
            completion.TrySetResult(exception);
        }
    }
}
