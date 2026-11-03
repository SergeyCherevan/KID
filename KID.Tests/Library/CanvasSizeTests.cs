using System.Windows;
using System.Windows.Controls;
using KID.Services.CodeExecution.Contexts;
using KID.Tests.Execution;
using KID.Tests.Infrastructure;

namespace KID.Tests.Library;

[Collection(ExecutionLifecycleCollection.Name)]
public sealed class CanvasSizeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Standalone_SizesCanvasDirectlyIncludingMinimums(bool hosted)
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            var (window, workspace, output) = CreateLayout(a, b);
            if (!hosted) window.Content = null;
            await using var session = await KIDRuntimeSession.OpenAsync(
                new KIDRuntimeOptions { Canvas = a }, TestContext.Current.CancellationToken);
            Assert.Equal((0d, 0d), Graphics.GetCanvasSize());
            Graphics.SetCanvasMinSize(10, 20);
            Graphics.SetCanvasSize(123, 234);
            Graphics.SetCanvas(b);
            Graphics.SetCanvasMinWidth(30);
            Graphics.SetCanvasMinHeight(40);
            Graphics.SetCanvasWidth(345);
            Graphics.SetCanvasHeight(456);
            Assert.Equal((123d, 234d, 10d, 20d), (a.Width, a.Height, a.MinWidth, a.MinHeight));
            Assert.Equal((345d, 456d, 30d, 40d), (b.Width, b.Height, b.MinWidth, b.MinHeight));
            Assert.Equal(GridLength.Auto, workspace.ColumnDefinitions[2].Width);
            Assert.Equal(GridLength.Auto, output.RowDefinitions[2].Height);
            Assert.Equal(0, workspace.ColumnDefinitions[2].MinWidth);
            Assert.Equal(0, output.RowDefinitions[2].MinHeight);
            b.Measure(new Size(500, 500));
            b.Arrange(new Rect(0, 0, b.DesiredSize.Width, b.DesiredSize.Height));
            Assert.Equal((345d, 456d), Graphics.GetCanvasSize());
            Graphics.SetCanvasMinSize(0, 0);
            Assert.Equal(0, b.MinWidth);
            Assert.Equal(0, b.MinHeight);
        });
    }

    [Theory]
    [InlineData("width", 0d)]
    [InlineData("height", -1d)]
    [InlineData("width", double.NaN)]
    [InlineData("height", double.PositiveInfinity)]
    [InlineData("minWidth", -1d)]
    [InlineData("minHeight", double.NaN)]
    [InlineData("minWidth", double.NegativeInfinity)]
    public async Task Sizing_PreservesNumericValidation(string dimension, double value)
    {
        await StaTest.RunAsync(async () =>
        {
            var canvas = new Canvas();
            await using var session = await KIDRuntimeSession.OpenAsync(
                new KIDRuntimeOptions { Canvas = canvas }, TestContext.Current.CancellationToken);
            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                switch (dimension)
                {
                    case "width": Graphics.SetCanvasWidth(value); break;
                    case "height": Graphics.SetCanvasHeight(value); break;
                    case "minWidth": Graphics.SetCanvasMinWidth(value); break;
                    case "minHeight": Graphics.SetCanvasMinHeight(value); break;
                }
            });
            Assert.Equal(dimension, error.ParamName);
            Assert.True(double.IsNaN(canvas.Width));
            Assert.True(double.IsNaN(canvas.Height));
            Assert.Equal(0, canvas.MinWidth);
            Assert.Equal(0, canvas.MinHeight);
        });
    }

    [Fact]
    public async Task IdeAdapter_PreservesMeasuredCanvasToPanelDeltas()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas { Width = 100, Height = 200 };
            var (window, workspace, output) = CreateLayout(a, new Canvas());
            var view = Assert.IsType<Grid>(Assert.Single(output.Children.Cast<UIElement>()));
            view.Width = 120;
            view.Height = 230;
            output.Width = 140;
            output.Height = 270;
            workspace.Measure(new Size(400, 400));
            workspace.Arrange(new Rect(0, 0, 400, 400));
            Assert.Equal((100d, 200d), (a.ActualWidth, a.ActualHeight));
            await using var session = await KIDRuntimeSession.OpenAsync(new KIDRuntimeOptions
            {
                Canvas = a, CanvasSizeHandler = new CanvasLayoutAdapter(a).TrySetSize
            }, TestContext.Current.CancellationToken);
            Graphics.SetCanvasMinSize(150, 250);
            Assert.Equal(190, workspace.ColumnDefinitions[2].MinWidth);
            Assert.Equal(280, output.RowDefinitions[2].MinHeight);
            Graphics.SetCanvasSize(150, 250);
            Assert.Equal(new GridLength(190), workspace.ColumnDefinitions[2].Width);
            Assert.Equal(new GridLength(280), output.RowDefinitions[2].Height);
            Assert.Equal((100d, 200d), (a.Width, a.Height));
            GC.KeepAlive(window);
        });
    }

    [Fact]
    public async Task IdeContext_AdaptsOnlyItsOwnCanvasAndReleasesAdapterBeforeNextSession()
    {
        await StaTest.RunAsync(async () =>
        {
            var a = new Canvas();
            var b = new Canvas();
            var (window, workspace, output) = CreateLayout(a, b);
            output.Margin = new Thickness(7, 0, 11, 0);
            using (var lease = ExecutionEnvironmentManager.BeginExecution(401, TestContext.Current.CancellationToken))
            {
                await using var context = new CanvasGraphicsContext(a);
                context.Init(401, a.Dispatcher);
                Graphics.SetCanvasMinSize(123, 234);
                Assert.Equal(141, workspace.ColumnDefinitions[2].MinWidth);
                Assert.Equal(234, output.RowDefinitions[2].MinHeight);
                Graphics.SetCanvasSize(50, 60);
                Assert.Equal(new GridLength(68), workspace.ColumnDefinitions[2].Width);
                Assert.Equal(new GridLength(60), output.RowDefinitions[2].Height);
                Assert.Equal(68, workspace.ColumnDefinitions[2].MinWidth);
                Assert.Equal(60, output.RowDefinitions[2].MinHeight);
                Assert.True(double.IsNaN(a.Width));
                Assert.True(double.IsNaN(a.Height));
                Graphics.SetCanvas(b);
                Graphics.SetCanvasMinSize(11, 22);
                Graphics.SetCanvasSize(33, 44);
                Assert.Equal((33d, 44d, 11d, 22d), (b.Width, b.Height, b.MinWidth, b.MinHeight));
                Assert.Equal(new GridLength(68), workspace.ColumnDefinitions[2].Width);
                Assert.Equal(new GridLength(60), output.RowDefinitions[2].Height);
            }
            Assert.Null(Graphics.Canvas);
            await using var next = await KIDRuntimeSession.OpenAsync(
                new KIDRuntimeOptions { Canvas = a }, TestContext.Current.CancellationToken);
            Graphics.SetCanvasSize(100, 200);
            Assert.Equal((100d, 200d), (a.Width, a.Height));
            Assert.Equal(new GridLength(68), workspace.ColumnDefinitions[2].Width);
            Assert.Equal(new GridLength(60), output.RowDefinitions[2].Height);
            GC.KeepAlive(window);
        });
    }

    private static (Window Window, Grid Workspace, Grid Output) CreateLayout(Canvas a, Canvas b)
    {
        var workspace = new Grid();
        var output = new Grid();
        for (var i = 0; i < 3; i++)
        {
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            output.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        var view = new Grid();
        view.Children.Add(a);
        view.Children.Add(b);
        output.Children.Add(view);
        workspace.Children.Add(output);
        var window = new Window { Content = workspace };
        NameScope.SetNameScope(window, new NameScope());
        window.RegisterName("WorkspaceGrid", workspace);
        window.RegisterName("OutputGrid", output);
        window.RegisterName("GraphicsOutputView", view);
        return (window, workspace, output);
    }
}
