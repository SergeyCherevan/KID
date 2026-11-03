using System.Threading;
using System.Windows.Threading;

namespace KID.StandaloneProbe;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        var exitCode = 1;

        _ = dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await Scenario.RunAsync();
                Console.WriteLine("PASS: " + Scenario.Description);
                exitCode = 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
            }
            finally
            {
                // Every scenario must finish its async cleanup before the UI pump stops.
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        });

        Dispatcher.Run();
        return exitCode;
    }
}

internal static class Probe
{
    internal static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    internal static void Expect<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
