using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KID.StandaloneProbe;

// Compiled only with -p:StandaloneContractStage=CanvasSwitching (stage 3).
internal static class Scenario
{
    internal const string Description = "A -> B -> A, shared style and accepted command destinations";

    internal static async Task RunAsync()
    {
        var a = new Canvas();
        var b = new Canvas();
        await using var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions { Canvas = a });

        Graphics.Color = "Red";
        var first = Graphics.Plot(10, 10);
        Graphics.SetCanvas(b);
        var second = Graphics.Plot(20, 20);
        Graphics.SetCanvas(a);
        Graphics.SetCanvas(a); // Repeated selection must be harmless.
        var third = Graphics.Plot(30, 30);
        Probe.Require(a.Children.Contains(first) && a.Children.Contains(third) && a.Children.Count == 2,
            "A -> B -> A lost or moved the contents of A.");
        Probe.Require(b.Children.Contains(second) && b.Children.Count == 1, "B received the wrong contents.");
        Probe.Require(second.Fill is SolidColorBrush brush && brush.Color == Colors.Red,
            "Switching reset the shared style.");

        // Keep the UI busy until the worker has actually posted the result-returning operation.
        // Dispatcher.Hooks is public WPF API; no sleeps, reflection or library test hooks.
        var dispatcher = a.Dispatcher;
        using var posted = new ManualResetEventSlim();
        DispatcherHookEventHandler onPosted = (_, _) =>
        {
            if (!dispatcher.CheckAccess())
                posted.Set();
        };
        Task<System.Windows.Shapes.Rectangle> pending;
        dispatcher.Hooks.OperationPosted += onPosted;
        try
        {
            pending = Task.Run(() => Graphics.Plot(40, 40));
            Probe.Require(posted.Wait(TimeSpan.FromSeconds(5)), "The worker did not post its operation.");
            Graphics.SetCanvas(b);
        }
        finally
        {
            dispatcher.Hooks.OperationPosted -= onPosted;
        }

        var delayed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Probe.Require(a.Children.Contains(delayed) && !b.Children.Contains(delayed),
            "An accepted figure was redirected from A to B.");

        Graphics.SetCanvas(a);
        var clearAccepted = Task.Run(() => Graphics.Clear());
        Probe.Require(clearAccepted.Wait(TimeSpan.FromSeconds(5)), "Clear did not return after admission.");
        Graphics.SetCanvas(b);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Probe.Require(a.Children.Count == 0 && b.Children.Contains(second),
            "An accepted void operation was redirected from A to B.");
    }
}
