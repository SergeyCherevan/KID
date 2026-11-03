using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using KID.Services.CodeExecution.Contexts;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class CanvasSwitchingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Selection_ValidatesWithoutChangingDestinationOrAttachingGraphics()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            Assert.Throws<ArgumentNullException>(() => Graphics.SetCanvas(null!));
            Assert.Throws<InvalidOperationException>(() => Graphics.SetCanvas(a));
            await using (var noGraphics = await OpenAsync(new KIDRuntimeOptions { MouseCanvas = a }))
            {
                Assert.Throws<InvalidOperationException>(() => Graphics.SetCanvas(a));
                Assert.Null(Graphics.Canvas);
            }

            await using (var session = await OpenAsync(new KIDRuntimeOptions { Canvas = a }))
            {
                Assert.Throws<ArgumentNullException>(() => Graphics.SetCanvas(null!));
                await StaTest.RunAsync(() =>
                {
                    var foreign = new Canvas();
                    Assert.Throws<ArgumentException>(() => Graphics.SetCanvas(foreign));
                });
                Assert.Same(a, Graphics.Canvas);
                Assert.True(a.Children.Contains(Graphics.Plot(1, 2)));
            }
            Assert.Throws<InvalidOperationException>(() => Graphics.SetCanvas(a));
        });
    }

    [Fact]
    public async Task WorkerSelection_ABA_PreservesContentsStyleScopeAndOtherComponents()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            var box = new TextBox();
            await using var session = await OpenAsync(new KIDRuntimeOptions
            {
                Canvas = a, MouseCanvas = a, KeyboardWindow = new Window(),
                ConsoleTextBox = box, EnableMusic = true
            });
            var scope = DispatcherManager.GetScope();
            var mouse = Mouse.CurrentScope;
            var keyboard = Keyboard.CurrentScope;
            var music = Music.CurrentScope;
            var console = TextBoxConsole.CurrentScope;
            Graphics.Color = "Red";
            Graphics.SetFont("Consolas", 31);
            var first = Graphics.Text(1, 2, "A");
            RunWithoutPumping(() => Graphics.SetCanvas(b));
            var second = Graphics.Text(3, 4, "B");
            Graphics.SetCanvas(a);
            Graphics.SetCanvas(a);
            var third = Graphics.Text(5, 6, "A again");
            Assert.Equal(new UIElement[] { first, third }, a.Children.Cast<UIElement>());
            Assert.Same(second, Assert.Single(b.Children.Cast<UIElement>()));
            Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(second.Foreground).Color);
            Assert.Equal(31, second.FontSize);
            Assert.Equal("Consolas", second.FontFamily.Source);
            Assert.Same(scope, DispatcherManager.GetScope());
            Assert.Same(mouse, Mouse.CurrentScope);
            Assert.Same(keyboard, Keyboard.CurrentScope);
            Assert.Same(music, Music.CurrentScope);
            Assert.Same(console, TextBoxConsole.CurrentScope);
            Assert.Equal(session.CancellationToken, StopManager.CurrentToken);
            TextBoxConsole.Write("still connected");
            await session.DisposeAsync();
            Assert.Equal("still connected", box.Text);
            Assert.Null(Graphics.Canvas);
            Assert.Null(Mouse.CurrentScope);
            Assert.Null(Keyboard.CurrentScope);
            Assert.Null(Music.CurrentScope);
            Assert.Null(TextBoxConsole.CurrentScope);
        });
    }

    [Theory]
    [InlineData("Plot")]
    [InlineData("PlotPoint")]
    [InlineData("Circle")]
    [InlineData("CirclePoint")]
    [InlineData("Ellipse")]
    [InlineData("EllipsePoint")]
    [InlineData("Rectangle")]
    [InlineData("RectanglePoint")]
    [InlineData("RectangleSize")]
    [InlineData("Line")]
    [InlineData("LinePoints")]
    [InlineData("QuadraticBezier")]
    [InlineData("CubicBezier")]
    [InlineData("Polygon")]
    [InlineData("PolygonTuples")]
    [InlineData("Text")]
    [InlineData("TextPoint")]
    [InlineData("Image")]
    [InlineData("ImagePoint")]
    [InlineData("ImageSize")]
    [InlineData("AddToCanvas")]
    public async Task AcceptedCreation_UsesOriginalCanvas(string operation)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            var detached = new Rectangle();
            var imagePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".png");
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4)));
                using (var file = File.Create(imagePath)) encoder.Save(file);
                await using var session = await OpenAsync(new KIDRuntimeOptions { Canvas = a });
                Point[] points = [new(1, 2), new(3, 4), new(5, 6), new(7, 8)];
                var pending = QueueThenSelectAsync<UIElement>(b, () => operation switch
                {
                    "Plot" => Graphics.Plot(1, 2),
                    "PlotPoint" => Graphics.Plot(points[0]),
                    "Circle" => Graphics.Circle(1, 2, 3),
                    "CirclePoint" => Graphics.Circle(points[0], 3),
                    "Ellipse" => Graphics.Ellipse(1, 2, 3, 4),
                    "EllipsePoint" => Graphics.Ellipse(points[0], 3, 4),
                    "Rectangle" => Graphics.Rectangle(1, 2, 3, 4),
                    "RectanglePoint" => Graphics.Rectangle(points[0], 3, 4),
                    "RectangleSize" => Graphics.Rectangle(points[0], points[1]),
                    "Line" => Graphics.Line(1, 2, 3, 4),
                    "LinePoints" => Graphics.Line(points[0], points[1]),
                    "QuadraticBezier" => Graphics.QuadraticBezier(points),
                    "CubicBezier" => Graphics.CubicBezier(points),
                    "Polygon" => Graphics.Polygon(points),
                    "PolygonTuples" => Graphics.Polygon([(1d, 2d), (3d, 4d)]),
                    "Text" => Graphics.Text(1, 2, "A"),
                    "TextPoint" => Graphics.Text(points[0], "A"),
                    "Image" => Graphics.Image(1, 2, imagePath),
                    "ImagePoint" => Graphics.Image(points[0], imagePath, 3, 4),
                    "ImageSize" => Graphics.Image(points[0], imagePath, points[1]),
                    "AddToCanvas" => detached.AddToCanvas(),
                    _ => throw new ArgumentException(operation)
                });
                var element = await pending.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                Assert.Same(element, Assert.Single(a.Children.Cast<UIElement>()));
                Assert.Empty(b.Children);
            }
            finally { File.Delete(imagePath); }
        });
    }

    [Fact]
    public async Task QueuedStyleSetters_KeepCallerOrderAcrossSelection()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            await using var session = await OpenAsync(new KIDRuntimeOptions { Canvas = a });
            RunWithoutPumping(() =>
            {
                Graphics.Color = "Red";
                Graphics.FillColor = "Green";
                Graphics.StrokeColor = "Blue";
                Graphics.SetFont("Consolas", 31);
            });
            var pending = QueueThenSelectAsync(b, () => Graphics.Text(1, 2, "queued"));
            var text = await pending.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.True(a.Children.Contains(text));
            Assert.Equal(Colors.Green, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
            Assert.Equal(31, text.FontSize);
            Assert.Equal("Consolas", text.FontFamily.Source);
            var shape = Graphics.Circle(1, 2, 3);
            Assert.True(b.Children.Contains(shape));
            Assert.Equal(Colors.Green, Assert.IsType<SolidColorBrush>(shape.Fill).Color);
            Assert.Equal(Colors.Blue, Assert.IsType<SolidColorBrush>(shape.Stroke).Color);
        });
    }

    [Theory]
    [InlineData("Clear")]
    [InlineData("RemoveFromCanvas")]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("Size")]
    [InlineData("GetWidth")]
    [InlineData("GetHeight")]
    [InlineData("GetSize")]
    public async Task AcceptedCanvasOperation_UsesOriginalCanvas(string operation)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas { Width = 100, Height = 200 };
            var b = new Canvas { Width = 300, Height = 400 };
            a.Measure(new Size(100, 200));
            a.Arrange(new Rect(0, 0, 100, 200));
            b.Measure(new Size(300, 400));
            b.Arrange(new Rect(0, 0, 300, 400));
            var first = new Rectangle();
            var second = new Rectangle();
            a.Children.Add(first);
            b.Children.Add(second);
            await using var session = await OpenAsync(new KIDRuntimeOptions { Canvas = a });
            object? result = null;
            var pending = QueueThenSelectAsync(b, () =>
            {
                switch (operation)
                {
                    case "Clear": Graphics.Clear(); break;
                    case "RemoveFromCanvas": result = first.RemoveFromCanvas(); break;
                    case "Width": Graphics.SetCanvasWidth(123); break;
                    case "Height": Graphics.SetCanvasHeight(234); break;
                    case "Size": Graphics.SetCanvasSize(123, 234); break;
                    case "GetWidth": result = Graphics.GetCanvasWidth(); break;
                    case "GetHeight": result = Graphics.GetCanvasHeight(); break;
                    case "GetSize": result = Graphics.GetCanvasSize(); break;
                }
                return true;
            });
            await pending.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await PumpAsync(); // void methods return once admitted, before UI execution.
            Assert.Same(second, Assert.Single(b.Children.Cast<UIElement>()));
            Assert.Equal(300, b.Width);
            Assert.Equal(400, b.Height);
            if (operation is "Clear" or "RemoveFromCanvas") Assert.Empty(a.Children);
            if (operation is "Width" or "Size") Assert.Equal(123, a.Width);
            if (operation is "Height" or "Size") Assert.Equal(234, a.Height);
            if (operation == "GetWidth") Assert.Equal(100d, result);
            if (operation == "GetHeight") Assert.Equal(200d, result);
            if (operation == "GetSize") Assert.Equal((100d, 200d), result);
        });
    }

    [Fact]
    public async Task CompositeSize_PassesCapturedCanvasEvenWhenFirstChangeSelectsAnother()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            await using var session = await OpenAsync(new KIDRuntimeOptions { Canvas = a });
            var descriptor = DependencyPropertyDescriptor.FromProperty(FrameworkElement.WidthProperty, typeof(Canvas))!;
            EventHandler onWidth = (_, _) => Graphics.SetCanvas(b);
            descriptor.AddValueChanged(a, onWidth);
            try { Graphics.SetCanvasSize(123, 234); }
            finally { descriptor.RemoveValueChanged(a, onWidth); }
            Assert.Same(b, Graphics.Canvas);
            Assert.Equal(123, a.Width);
            Assert.Equal(234, a.Height);
            Assert.True(double.IsNaN(b.Width));
            Assert.True(double.IsNaN(b.Height));
        });
    }

    [Theory]
    [InlineData("MinWidth")]
    [InlineData("MinHeight")]
    [InlineData("MinSize")]
    public async Task AcceptedMinimumSize_UsesOriginalLayoutAndPassesTargetToNestedHelpers(string operation)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            var workspace = new Grid();
            for (var i = 0; i < 3; i++) workspace.ColumnDefinitions.Add(new ColumnDefinition());
            var output = new Grid();
            for (var i = 0; i < 3; i++) output.RowDefinitions.Add(new RowDefinition());
            var view = new Grid();
            view.Children.Add(a);
            output.Children.Add(view);
            workspace.Children.Add(output);
            var window = new Window { Content = workspace };
            NameScope.SetNameScope(window, new NameScope());
            window.RegisterName("WorkspaceGrid", workspace);
            window.RegisterName("OutputGrid", output);
            window.RegisterName("GraphicsOutputView", view);
            await using var session = await OpenAsync(new KIDRuntimeOptions
            {
                Canvas = a, CanvasSizeHandler = new CanvasLayoutAdapter(a).TrySetSize
            });
            // Changing the first dimension also changes the global destination during execution.
            var descriptor = DependencyPropertyDescriptor.FromProperty(
                ColumnDefinition.MinWidthProperty, typeof(ColumnDefinition))!;
            EventHandler onWidth = (_, _) => Graphics.SetCanvas(b);
            descriptor.AddValueChanged(workspace.ColumnDefinitions[2], onWidth);
            try
            {
                var pending = QueueThenSelectAsync(b, () =>
                {
                    switch (operation)
                    {
                        case "MinWidth": Graphics.SetCanvasMinWidth(123); break;
                        case "MinHeight": Graphics.SetCanvasMinHeight(234); break;
                        case "MinSize": Graphics.SetCanvasMinSize(123, 234); break;
                    }
                    return true;
                });
                await pending.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                await PumpAsync();
                if (operation is "MinWidth" or "MinSize") Assert.Equal(123, workspace.ColumnDefinitions[2].MinWidth);
                if (operation is "MinHeight" or "MinSize") Assert.Equal(234, output.RowDefinitions[2].MinHeight);
                Assert.Equal(0, b.MinWidth);
                Assert.Equal(0, b.MinHeight);
            }
            finally { descriptor.RemoveValueChanged(workspace.ColumnDefinitions[2], onWidth); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_KeepsAcceptedDestinationAndRejectsSelection(bool stop)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            var session = await OpenAsync(new KIDRuntimeOptions { Canvas = a });
            var oldScope = DispatcherManager.GetScope();
            var pending = QueueThenSelectAsync(b, () => Graphics.Plot(1, 2));
            if (stop)
            {
                session.RequestStop();
                Assert.Throws<ObjectDisposedException>(() => Graphics.SetCanvas(a));
            }
            var disposal = session.DisposeAsync().AsTask();
            if (!stop) Assert.Throws<ObjectDisposedException>(() => Graphics.SetCanvas(a));
            await disposal;
            if (stop)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.WaitAsync(Timeout, TestContext.Current.CancellationToken));
                Assert.Empty(a.Children);
            }
            else Assert.True(a.Children.Contains(await pending));
            Assert.Empty(b.Children);
            await using var next = await OpenAsync(new KIDRuntimeOptions { Canvas = b });
            Graphics.Release(oldScope);
            var voidFailure = Record.Exception(() => DispatcherManager.InvokeOnUI(oldScope, () => a.Children.Clear()));
            var resultFailure = Record.Exception(() => DispatcherManager.InvokeOnUI(oldScope, () => a.Children.Count));
            if (stop)
            {
                Assert.Equal(session.CancellationToken, Assert.IsAssignableFrom<OperationCanceledException>(voidFailure).CancellationToken);
                Assert.Equal(session.CancellationToken, Assert.IsAssignableFrom<OperationCanceledException>(resultFailure).CancellationToken);
            }
            else
            {
                Assert.IsType<ObjectDisposedException>(voidFailure);
                Assert.IsType<ObjectDisposedException>(resultFailure);
            }
            await session.DisposeAsync();
            Assert.Same(b, Graphics.Canvas);
            Assert.True(b.Children.Contains(Graphics.Plot(3, 4)));
        });
    }

    [Fact]
    public async Task SelectionRacingCleanup_DoesNotLeakAnOldCanvasIntoNextSession()
    {
        await StaTest.RunAsync(async () =>
        {
            for (var run = 0; run < 20; run++)
            {
                var a = new Canvas();
                var b = new Canvas();
                var session = await OpenAsync(new KIDRuntimeOptions { Canvas = a });
                using var started = new ManualResetEventSlim();
                var producer = Task.Run(() =>
                {
                    Graphics.SetCanvas(b);
                    started.Set();
                    for (var i = 0; i < 100; i++)
                    {
                        try { Graphics.SetCanvas(i % 2 == 0 ? a : b); }
                        catch (ObjectDisposedException) { return; }
                        catch (InvalidOperationException) { return; }
                    }
                }, TestContext.Current.CancellationToken);
                Assert.True(started.Wait(Timeout));
                await session.DisposeAsync();
                await producer.WaitAsync(Timeout, TestContext.Current.CancellationToken);
                Assert.Null(Graphics.Canvas);
                var nextCanvas = new Canvas();
                await using var next = await OpenAsync(new KIDRuntimeOptions { Canvas = nextCanvas });
                Graphics.SetCanvas(nextCanvas);
                Assert.True(nextCanvas.Children.Contains(Graphics.Plot(1, 2)));
                Assert.Empty(a.Children);
                Assert.Empty(b.Children);
            }
        });
    }

    private static ValueTask<KIDRuntimeSession> OpenAsync(KIDRuntimeOptions options) =>
        KIDRuntimeSession.OpenAsync(options, TestContext.Current.CancellationToken);

    private static Task<T> QueueThenSelectAsync<T>(Canvas next, Func<T> action)
    {
        var dispatcher = next.Dispatcher;
        using var posted = new ManualResetEventSlim();
        DispatcherHookEventHandler onPosted = (_, args) =>
        {
            if (!dispatcher.CheckAccess() && args.Operation.Priority == DispatcherPriority.Background) posted.Set();
        };
        dispatcher.Hooks.OperationPosted += onPosted;
        try
        {
            using var blocked = dispatcher.DisableProcessing();
            var pending = Task.Run(action, TestContext.Current.CancellationToken);
            Assert.True(posted.Wait(Timeout), "The graphics operation was not posted.");
            Graphics.SetCanvas(next);
            return pending;
        }
        finally { dispatcher.Hooks.OperationPosted -= onPosted; }
    }

    private static void RunWithoutPumping(Action action)
    {
        using var blocked = Dispatcher.CurrentDispatcher.DisableProcessing();
        var pending = Task.Run(action, TestContext.Current.CancellationToken);
        Assert.True(SpinWait.SpinUntil(() => pending.IsCompleted, Timeout));
        Assert.True(pending.IsCompletedSuccessfully, pending.Exception?.ToString());
    }

    private static async Task PumpAsync() =>
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
}
