using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;
using Path = System.IO.Path;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class CanvasObjectTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Elements_OperateOnOriginalObjectAndTransferExplicitly()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            await using var session = await OpenAsync(a);
            var shape = Graphics.Rectangle(1, 2, 30, 40);
            var text = Graphics.Text(1, 2, "old");
            Graphics.SetCanvas(b);
            await Task.Run(() =>
            {
                shape.SetSize(50, 60).SetLeftTopXY(10, 20).MoveToRightBottom(3, 4);
                shape.SetColor("Red");
                text.SetText("new").SetFont("Consolas", 28);
                shape.RemoveFromCanvas();
                shape.AddToCanvas();
            }, TestContext.Current.CancellationToken);
            Assert.Equal(new Point(13, 24), shape.GetLeftTopXY());
            Assert.Equal(50, shape.Width);
            Assert.Equal(60, shape.Height);
            Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(shape.Fill).Color);
            Assert.Same(b, VisualTreeHelper.GetParent(shape));
            Assert.Same(a, VisualTreeHelper.GetParent(text));
            Assert.Equal("new", text.Text);
            Assert.Equal(28, text.FontSize);
            Assert.Equal("Consolas", text.FontFamily.Source);
            Assert.Throws<InvalidOperationException>(() => shape.AddToCanvas());
            Graphics.SetCanvas(a);
            Assert.Throws<InvalidOperationException>(() => shape.AddToCanvas());
            Assert.Same(b, VisualTreeHelper.GetParent(shape));
            shape.RemoveFromCanvas();
            Assert.Same(shape, shape.RemoveFromCanvas()); // detached is a no-op
            shape.AddToCanvas();
            Assert.Same(a, VisualTreeHelper.GetParent(shape));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removal_RejectsVisualAndLogicalNonCanvasParents(bool logicalOnly)
    {
        await StaTest.RunAsync(async () =>
        {
            var element = new Rectangle();
            DependencyObject parent;
            if (logicalOnly) parent = new ContentControl { Content = element };
            else
            {
                var grid = new Grid();
                grid.Children.Add(element);
                parent = grid;
            }
            await using var session = await OpenAsync(new Canvas());
            Assert.Throws<InvalidOperationException>(() => element.RemoveFromCanvas());
            Assert.Throws<InvalidOperationException>(() => element.AddToCanvas());
            Assert.Same(parent, GraphicsElementAccess.GetParent(element));
        });
    }

    [Theory]
    [InlineData("position")]
    [InlineData("center")]
    [InlineData("size")]
    [InlineData("color")]
    [InlineData("text")]
    [InlineData("font")]
    [InlineData("image")]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task ElementOperations_RejectForeignDispatcherBeforePosting(string operation)
    {
        await StaTest.RunAsync(async () =>
        {
            await using var session = await OpenAsync(new Canvas());
            await StaTest.RunAsync(() =>
            {
                var shape = new Rectangle { Width = 10 };
                var text = new TextBlock { Text = "old" };
                var image = new Image();
                // The session UI is blocked waiting for this STA: validation must not post to it.
                Assert.Throws<ArgumentException>(() =>
                {
                    switch (operation)
                    {
                        case "position": shape.SetLeftTopXY(1, 2); break;
                        case "center": shape.GetCenterXY(); break;
                        case "size": shape.SetSize(20, 30); break;
                        case "color": shape.SetFillColor("Red"); break;
                        case "text": text.SetText("new"); break;
                        case "font": text.SetFont("Consolas", 20); break;
                        case "image": image.SetSource("missing.png"); break;
                        case "add": shape.AddToCanvas(); break;
                        case "remove": shape.RemoveFromCanvas(); break;
                    }
                });
                Assert.Equal(10, shape.Width);
                Assert.Equal("old", text.Text);
                Assert.Null(image.Source);
            });
        });
    }

    [Fact]
    public async Task ObjectOperations_DoNotRequireGraphicsDestination()
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            var element = new Rectangle();
            canvas.Children.Add(element);
            await using var session = await KIDRuntimeSession.OpenAsync(
                new KIDRuntimeOptions { MouseCanvas = canvas }, TestContext.Current.CancellationToken);
            element.SetSize(10, 20).SetLeftTopXY(3, 4);
            var sprite = new Sprite(0, 0, element);
            sprite.Move(5, 6).Hide().Show();
            Assert.Equal(new Point(5, 6), sprite.Position);
            Assert.Equal(new Point(3, 4), element.GetLeftTopXY());
            element.RemoveFromCanvas();
            Assert.Empty(canvas.Children);
            Assert.Throws<InvalidOperationException>(() => element.AddToCanvas());
        });
    }

    [Fact]
    public async Task ImageSource_UsesObjectDispatcherWithoutGraphicsAndReleasesFile()
    {
        await StaTest.RunAsync(async () =>
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4)));
                using (var file = File.Create(path)) encoder.Save(file);
                var canvas = new Canvas();
                var image = new Image();
                canvas.Children.Add(image);
                await using var session = await KIDRuntimeSession.OpenAsync(
                    new KIDRuntimeOptions { MouseCanvas = canvas }, TestContext.Current.CancellationToken);
                Assert.Same(image, await Task.Run(() => image.SetSource(path, new Point(30, 40)),
                    TestContext.Current.CancellationToken));
                Assert.NotNull(image.Source);
                Assert.Equal((30d, 40d), (image.Width, image.Height));
                Assert.Same(canvas, VisualTreeHelper.GetParent(image));
                File.Delete(path); // BitmapCacheOption.OnLoad must not keep the file open.
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }

    [Fact]
    public async Task AcceptedRemoval_UsesActualParentAtExecutionAndDrainsNormally()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            var element = new Rectangle();
            a.Children.Add(element);
            var session = await OpenAsync(a);
            using var posted = new ManualResetEventSlim();
            var dispatcher = a.Dispatcher;
            DispatcherHookEventHandler handler = (_, args) =>
            {
                if (!dispatcher.CheckAccess() && args.Operation.Priority == DispatcherPriority.Background)
                    posted.Set();
            };
            dispatcher.Hooks.OperationPosted += handler;
            Task<UIElement> pending;
            Task disposal;
            try
            {
                using var blocked = dispatcher.DisableProcessing();
                pending = Task.Run(() => element.RemoveFromCanvas(), TestContext.Current.CancellationToken);
                Assert.True(posted.Wait(Timeout));
                a.Children.Remove(element);
                b.Children.Add(element);
                Graphics.SetCanvas(b);
                disposal = session.DisposeAsync().AsTask();
            }
            finally { dispatcher.Hooks.OperationPosted -= handler; }
            Assert.Same(element, await pending.WaitAsync(Timeout, TestContext.Current.CancellationToken));
            await disposal.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.Empty(a.Children);
            Assert.Empty(b.Children);
            await session.DisposeAsync();
        });
    }

    private static ValueTask<KIDRuntimeSession> OpenAsync(Canvas canvas) => KIDRuntimeSession.OpenAsync(
        new KIDRuntimeOptions { Canvas = canvas }, TestContext.Current.CancellationToken);
}
