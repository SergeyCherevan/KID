using System.Windows;
using System.Windows.Controls;

namespace KID.StandaloneProbe;

internal static class Scenario
{
    internal const string Description = "current public commands cannot bootstrap a standalone runtime";

    internal static Task RunAsync()
    {
        var canvas = new Canvas();
        var window = new Window();

        Probe.Expect<InvalidOperationException>(() => Graphics.Plot(10, 10),
            "Drawing unexpectedly succeeded without a session.");
        Probe.Expect<InvalidOperationException>(() => Mouse.Init(canvas),
            "Mouse.Init unexpectedly opened an execution environment.");
        Probe.Expect<InvalidOperationException>(() => Keyboard.Init(window),
            "Keyboard.Init unexpectedly opened an execution environment.");
        Probe.Expect<InvalidOperationException>(Music.Init,
            "Music.Init unexpectedly opened an execution environment.");
        Probe.Require(canvas.Children.Count == 0, "A rejected call changed the host Canvas.");
        Probe.Require(StopManager.CurrentToken == default, "A rejected call leaked an execution token.");

        return Task.CompletedTask;
    }
}
