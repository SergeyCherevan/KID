using System.Windows;
using System.Windows.Controls;

namespace KID.StandaloneProbe;

internal static class Scenario
{
    internal const string Description = "actual parents, sprite collisions and standalone Canvas sizing";

    internal static async Task RunAsync()
    {
        var a = new Canvas();
        var b = new Canvas();
        await using var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions
        {
            Canvas = a, MouseCanvas = a, KeyboardWindow = new Window()
        });
        Graphics.SetCanvasMinSize(10, 20);
        Graphics.SetCanvasSize(200, 200);
        Probe.Require(a.Width == 200 && a.Height == 200 && a.MinWidth == 10 && a.MinHeight == 20,
            "Standalone sizing did not change the supplied Canvas.");
        var firstElement = Graphics.Rectangle(0, 0, 20, 20);
        var first = new Sprite(0, 0, firstElement);
        var second = new Sprite(0, 0, Graphics.Rectangle(5, 5, 20, 20));
        Graphics.SetCanvas(b);
        Graphics.SetCanvasSize(200, 200);
        var otherCanvas = new Sprite(0, 0, Graphics.Rectangle(0, 0, 20, 20));
        Layout(a, b);
        Probe.Require(first.DetectCollisions([second, otherCanvas]).Count == 1,
            "Collision coordinates followed the selected Canvas.");
        first.Move(100, 0).Hide().Show();
        Layout(a, b);
        Probe.Require(first.DetectCollisions([second]).Count == 0, "Saved sprite movement failed.");
        first.Move(-100, 0);
        firstElement.SetLeftTopXY(0, 0).RemoveFromCanvas();
        Probe.Require(!a.Children.Contains(firstElement), "Removal ignored the actual parent Canvas.");
        Probe.Require(first.DetectCollisions([second]).Count == 0, "Detached elements collided.");
        firstElement.AddToCanvas();
        Probe.Expect<InvalidOperationException>(() => firstElement.AddToCanvas(),
            "Adding an attached element must require explicit removal.");
        Layout(a, b);
        Probe.Require(first.DetectCollisions([otherCanvas]).Count == 1,
            "A transferred sprite did not use its new actual Canvas.");
        Probe.Require(first.DetectCollisions([second]).Count == 0, "Sprites on different Canvases collided.");
    }

    private static void Layout(params Canvas[] canvases)
    {
        foreach (var canvas in canvases)
        {
            canvas.Measure(new Size(200, 200));
            canvas.Arrange(new Rect(0, 0, 200, 200));
            canvas.UpdateLayout();
        }
    }
}
