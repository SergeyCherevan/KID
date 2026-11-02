using System.Windows;
using System.Windows.Controls;

namespace KID.StandaloneProbe;

// Compiled only with -p:StandaloneContractStage=Session. No substitute runtime types.
internal static class Scenario
{
    internal const string Description = "public session, optional components, STOP, cleanup and reopen";

    internal static async Task RunAsync()
    {
        var canvas = new Canvas();
        var originalOut = Console.Out;
        var originalIn = Console.In;
        var originalError = Console.Error;

        await using (var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions
        {
            Canvas = canvas,
            MouseCanvas = canvas,
            KeyboardWindow = new Window(), // Explicit; the Canvas need not belong to a Window yet.
            EnableMusic = true,
            ConsoleTextBox = new TextBox(),
            RedirectSystemConsole = true,
            DiagnosticSink = (KIDRuntimeDiagnostic diagnostic) =>
            {
                // Bind the host-neutral contract without injecting callback failures.
                _ = diagnostic.ExecutionId;
                _ = diagnostic.Component;
                _ = diagnostic.Operation;
                _ = diagnostic.Exception;
            }
        }))
        {
            Probe.Require(session.CancellationToken == StopManager.CurrentToken, "Session tokens differ.");
            var point = await Task.Run(() => Graphics.Plot(10, 10));
            Probe.Require(canvas.Children.Contains(point), "The host Canvas did not receive the point.");
            Console.WriteLine("standalone console output");
        }

        Probe.Require(ReferenceEquals(Console.Out, originalOut), "Console.Out was not restored.");
        Probe.Require(ReferenceEquals(Console.In, originalIn), "Console.In was not restored.");
        Probe.Require(ReferenceEquals(Console.Error, originalError), "Console.Error was not restored.");
        Probe.Require(StopManager.CurrentToken == default, "Completed session is still published.");

        // No Canvas or TextBox is required for music. No device playback is requested here.
        await using (var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions { EnableMusic = true }))
        {
            session.RequestStop();
            session.RequestStop();
            Probe.Require(session.CancellationToken.IsCancellationRequested, "STOP did not cancel the session.");
            await Task.WhenAll(session.DisposeAsync().AsTask(), session.DisposeAsync().AsTask());
        }

        await using var reopened = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions { Canvas = canvas });
        _ = Graphics.Plot(20, 20);
    }
}
