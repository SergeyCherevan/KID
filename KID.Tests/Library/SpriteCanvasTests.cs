using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class SpriteCanvasTests
{
    [Fact]
    public async Task Collision_UsesActualCanvasAfterSelectionMovementAndVisibilityChanges()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            await using var session = await OpenAsync(a);
            var first = new Sprite(0, 0, Graphics.Rectangle(0, 0, 20, 20));
            var second = new Sprite(0, 0, Graphics.Rectangle(5, 5, 20, 20));
            Graphics.SetCanvas(b);
            var foreignCanvasSprite = new Sprite(0, 0, Graphics.Rectangle(0, 0, 20, 20));
            Layout(a, b);
            var hit = Assert.Single(first.DetectCollisions([second, foreignCanvasSprite]));
            Assert.Same(first, hit.SpritesPair.First);
            Assert.Same(second, hit.SpritesPair.Second);
            first.Hide();
            Assert.Empty(first.DetectCollisions([second]));
            first.Show().Move(100, 0);
            Layout(a, b);
            Assert.Empty(first.DetectCollisions([second]));
            first.Move(-100, 0);
            Layout(a, b);
            Assert.Single(first.DetectCollisions([second]));
            Assert.Same(b, Graphics.Canvas);
            Assert.All(first.GraphicElements, element => Assert.Same(a, VisualTreeHelper.GetParent(element)));
        });
    }

    [Fact]
    public async Task DetachedAndEmptySprites_ClearCollisionsAndRejoinAfterAttachment()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            await using var session = await OpenAsync(a);
            var firstElement = Graphics.Rectangle(0, 0, 20, 20);
            var secondElement = Graphics.Rectangle(0, 0, 20, 20);
            var first = new Sprite(0, 0, firstElement);
            var second = new Sprite(0, 0, secondElement);
            Layout(a);
            Assert.Single(first.DetectCollisions([second]));
            firstElement.RemoveFromCanvas();
            var empty = first.DetectCollisions([second]);
            Assert.Empty(empty);
            Assert.Same(empty, first.Collisions);
            first.Move(1, 2).Hide().Show(); // detached elements still support object operations
            first.Move(-1, -2);
            firstElement.AddToCanvas();
            Layout(a);
            Assert.Single(first.DetectCollisions([second]));
            // The detached second element must not contribute even if its old bounds overlap.
            secondElement.RemoveFromCanvas();
            Assert.Empty(first.DetectCollisions([second]));
            first.GraphicElements.Clear();
            Assert.Empty(first.DetectCollisions([second]));
            Assert.Empty(new Sprite().DetectCollisions([first]));
        });
    }

    [Fact]
    public async Task PartialTransfer_RejectsMixedSpriteUntilAllElementsReachNewCanvas()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            await using var session = await OpenAsync(a);
            var one = Graphics.Rectangle(0, 0, 20, 20);
            var two = Graphics.Rectangle(30, 0, 20, 20);
            var sprite = new Sprite(0, 0, one, two);
            one.RemoveFromCanvas();
            Graphics.SetCanvas(b);
            one.AddToCanvas();
            var target = new Sprite(0, 0, Graphics.Rectangle(0, 0, 20, 20));
            Assert.Throws<InvalidOperationException>(() => sprite.Move(10, 20));
            Assert.Throws<InvalidOperationException>(() => sprite.Hide());
            Assert.Throws<InvalidOperationException>(() => sprite.Show());
            Assert.Throws<InvalidOperationException>(() => sprite.IsVisible = true);
            Assert.Throws<InvalidOperationException>(() => sprite.Position = sprite.Position);
            Assert.Throws<InvalidOperationException>(() => sprite.DetectCollisions([]));
            Assert.Equal(new Point(0, 0), sprite.Position);
            Assert.Equal(Visibility.Visible, one.Visibility);
            Assert.Same(Transform.Identity, one.RenderTransform);
            two.RemoveFromCanvas().AddToCanvas();
            Layout(a, b);
            Assert.Single(sprite.DetectCollisions([target]));
            Graphics.SetCanvas(a);
            sprite.Move(100, 0);
            Layout(a, b);
            Assert.Empty(sprite.DetectCollisions([target]));
            Assert.Same(b, VisualTreeHelper.GetParent(two));
        });
    }

    [Theory]
    [InlineData("move")]
    [InlineData("show")]
    [InlineData("hide")]
    [InlineData("collision")]
    public async Task MutatedElements_RejectUnsupportedParentsBeforeChanges(string operation)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            await using var session = await OpenAsync(a);
            var first = Graphics.Rectangle(0, 0, 20, 20);
            var invalid = new Rectangle();
            var grid = new Grid();
            grid.Children.Add(invalid);
            var sprite = new Sprite(0, 0, first);
            sprite.GraphicElements.Add(invalid);
            Assert.Throws<InvalidOperationException>(() => Operate(sprite, operation));
            Assert.Equal(new Point(0, 0), sprite.Position);
            Assert.Equal(Visibility.Visible, first.Visibility);
            Assert.Same(Transform.Identity, first.RenderTransform);
        });
    }

    [Theory]
    [InlineData("move")]
    [InlineData("show")]
    [InlineData("hide")]
    [InlineData("collision")]
    public async Task MutatedElements_RejectForeignDispatcherBeforeChanges(string operation)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            await using var session = await OpenAsync(a);
            var first = Graphics.Rectangle(0, 0, 20, 20);
            Rectangle foreign = null!;
            await StaTest.RunAsync(() => { foreign = new Rectangle(); });
            var sprite = new Sprite(0, 0, first);
            sprite.GraphicElements.Add(foreign);
            Assert.Throws<ArgumentException>(() => Operate(sprite, operation));
            Assert.Equal(new Point(0, 0), sprite.Position);
            Assert.Equal(Visibility.Visible, first.Visibility);
            Assert.Same(Transform.Identity, first.RenderTransform);
        });
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("hidden")]
    [InlineData("differentCanvas")]
    public async Task Collision_ValidatesComparedSpritesBeforeShortcuts(string receiver)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            await using var session = await OpenAsync(a);
            var first = receiver == "empty" ? new Sprite() :
                new Sprite(0, 0, Graphics.Rectangle(0, 0, 20, 20));
            if (receiver == "hidden") first.Hide();
            Graphics.SetCanvas(b);
            var other = new Sprite(0, 0, Graphics.Rectangle(0, 0, 20, 20));
            other.Hide();
            Graphics.SetCanvas(a);
            other.GraphicElements.Add(Graphics.Rectangle(0, 0, 20, 20));
            Assert.Throws<InvalidOperationException>(() => first.DetectCollisions([other]));
            Rectangle foreign = null!;
            await StaTest.RunAsync(() => { foreign = new Rectangle(); });
            other.GraphicElements = [foreign];
            Assert.Throws<ArgumentException>(() => first.DetectCollisions([other]));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptySprite_RetainsOriginalScopeAsReceiverAndComparedOperand(bool stop)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var session = await OpenAsync(a);
            var old = new Sprite();
            old.Hide();
            if (stop) session.RequestStop();
            await session.DisposeAsync();
            await using var next = await OpenAsync(new Canvas());
            var current = new Sprite();
            foreach (Action action in new Action[]
            {
                () => old.Move(0, 0), () => old.Show(), () => old.Hide(),
                () => old.DetectCollisions([]), () => current.DetectCollisions([old])
            })
            {
                if (stop)
                    Assert.Equal(session.CancellationToken,
                        Assert.ThrowsAny<OperationCanceledException>(action).CancellationToken);
                else Assert.Throws<ObjectDisposedException>(action);
            }
        });
    }

    private static void Operate(Sprite sprite, string operation)
    {
        switch (operation)
        {
            case "move": sprite.Move(10, 20); break;
            case "show": sprite.Show(); break;
            case "hide": sprite.Hide(); break;
            case "collision": sprite.DetectCollisions([]); break;
        }
    }

    private static void Layout(params Canvas[] canvases)
    {
        foreach (var canvas in canvases)
        {
            canvas.Measure(new Size(400, 400));
            canvas.Arrange(new Rect(0, 0, 400, 400));
            canvas.UpdateLayout();
        }
    }

    private static ValueTask<KIDRuntimeSession> OpenAsync(Canvas canvas) => KIDRuntimeSession.OpenAsync(
        new KIDRuntimeOptions { Canvas = canvas }, TestContext.Current.CancellationToken);
}
