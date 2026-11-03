using System.Windows;
using System.Windows.Controls;

namespace KID.Services.CodeExecution.Contexts;

/// <summary>Maps size commands for the IDE's own Canvas to its splitter-controlled panels.</summary>
internal sealed class CanvasLayoutAdapter(Canvas canvas)
{
    internal bool TrySetSize(Canvas target, CanvasSizeProperty property, double value)
    {
        if (!ReferenceEquals(target, canvas)) return false;
        var window = Window.GetWindow(target);
        if (window == null) return false;
        var output = window.FindName("OutputGrid") as Grid;
        if (property is CanvasSizeProperty.Width or CanvasSizeProperty.MinWidth)
        {
            var workspace = window.FindName("WorkspaceGrid") as Grid;
            if (workspace == null || workspace.ColumnDefinitions.Count < 3) return false;
            var column = workspace.ColumnDefinitions[2];
            var size = value + WidthDelta(target, output);
            if (property == CanvasSizeProperty.MinWidth) column.MinWidth = size;
            else
            {
                if (column.MinWidth > size) column.MinWidth = size;
                column.Width = new GridLength(size, GridUnitType.Pixel);
            }
        }
        else
        {
            if (output == null || output.RowDefinitions.Count < 3) return false;
            var row = output.RowDefinitions[2];
            var view = window.FindName("GraphicsOutputView") as FrameworkElement;
            var size = value + HeightDelta(target, view);
            if (property == CanvasSizeProperty.MinHeight) row.MinHeight = size;
            else
            {
                if (row.MinHeight > size) row.MinHeight = size;
                row.Height = new GridLength(size, GridUnitType.Pixel);
            }
        }
        return true;
    }

    private static double WidthDelta(Canvas target, Grid? output)
    {
        if (output != null && output.ActualWidth > 0 && target.ActualWidth > 0)
        {
            var delta = output.ActualWidth - target.ActualWidth;
            if (double.IsFinite(delta) && delta >= 0) return delta;
        }
        return (output?.Margin.Left ?? 0) + (output?.Margin.Right ?? 0);
    }

    private static double HeightDelta(Canvas target, FrameworkElement? view)
    {
        if (view != null && view.ActualHeight > 0 && target.ActualHeight > 0)
        {
            var delta = view.ActualHeight - target.ActualHeight;
            if (double.IsFinite(delta) && delta >= 0) return delta;
        }
        return 0;
    }
}
