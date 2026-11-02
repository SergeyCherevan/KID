using System.Windows;
using System.Windows.Controls;

namespace KID.StandaloneProbe;

// Runs in a separate process: failed cleanup deliberately blocks admission for its lifetime.
internal static class Scenario
{
    internal const string Description = "cleanup failure restores resources and permanently blocks admission";

    internal static async Task RunAsync()
    {
        var output = Console.Out;
        var input = Console.In;
        var error = Console.Error;
        var canvas = new Canvas();
        var diagnostics = new List<KIDRuntimeDiagnostic>();
        var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions
        {
            Canvas = canvas,
            MouseCanvas = canvas,
            KeyboardWindow = new Window(),
            EnableMusic = true,
            ConsoleTextBox = new TextBox(),
            RedirectSystemConsole = true,
            DiagnosticSink = diagnostics.Add
        });
        var commandFailure = new InvalidOperationException("accepted void command failed");
        Task.Run(() => DispatcherManager.InvokeOnUI(() => throw commandFailure))
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        var first = session.DisposeAsync().AsTask();
        var second = session.DisposeAsync().AsTask();
        Probe.Require(ReferenceEquals(first, second), "Concurrent disposal did not share its completion.");
        try
        {
            await first;
            throw new InvalidOperationException("Cleanup incorrectly succeeded.");
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, commandFailure)) { }

        Probe.Require(ReferenceEquals(Console.Out, output), "Console.Out was not restored after failure.");
        Probe.Require(ReferenceEquals(Console.In, input), "Console.In was not restored after failure.");
        Probe.Require(ReferenceEquals(Console.Error, error), "Console.Error was not restored after failure.");
        Probe.Require(Graphics.Canvas == null, "Graphics ownership was not released.");
        Probe.Require(StopManager.CurrentToken == default, "Failed session is still published.");
        Probe.Require(diagnostics.Any(diagnostic => ReferenceEquals(diagnostic.Exception, commandFailure)),
            "Cleanup failure was not diagnosed.");
        session.RequestStop();
        try
        {
            await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions());
            throw new InvalidOperationException("A session opened after failed cleanup.");
        }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception.InnerException, commandFailure)) { }
        try { await session.DisposeAsync(); }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, commandFailure)) { return; }
        throw new InvalidOperationException("Repeated disposal lost the cleanup failure.");
    }
}
