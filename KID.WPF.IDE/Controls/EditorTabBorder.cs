using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KID.Controls;

/// <summary>
/// Рамка вкладки: обычный Border для VS2022, верхние закругления и нижние
/// внешние сопряжения для VS2026. Размеры задаёт активная тема.
/// </summary>
public sealed class EditorTabBorder : Border
{
    public static readonly DependencyProperty UseCurvedShapeProperty =
        DependencyProperty.Register(nameof(UseCurvedShape), typeof(bool), typeof(EditorTabBorder),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool UseCurvedShape
    {
        get => (bool)GetValue(UseCurvedShapeProperty);
        set => SetValue(UseCurvedShapeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!UseCurvedShape)
        {
            base.OnRender(drawingContext);
            return;
        }

        // Рамка располагается внутри доступной области, включая половину stroke.
        var stroke = BorderThickness.Left;
        var width = Math.Max(0, ActualWidth - stroke);
        var height = Math.Max(0, ActualHeight - stroke);
        if (width == 0 || height == 0)
            return;

        var radius = Math.Min(CornerRadius.TopLeft, Math.Min(width / 4, height / 2));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, height), true, true);
            Arc(new Point(radius, height - radius), SweepDirection.Counterclockwise);
            context.LineTo(new Point(radius, radius), true, false);
            Arc(new Point(2 * radius, 0), SweepDirection.Clockwise);
            context.LineTo(new Point(width - 2 * radius, 0), true, false);
            Arc(new Point(width - radius, radius), SweepDirection.Clockwise);
            context.LineTo(new Point(width - radius, height - radius), true, false);
            Arc(new Point(width, height), SweepDirection.Counterclockwise);

            void Arc(Point point, SweepDirection direction) =>
                context.ArcTo(point, new Size(radius, radius), 0, false, direction, true, false);
        }
        geometry.Freeze();
        drawingContext.PushTransform(new TranslateTransform(stroke / 2, stroke / 2));
        drawingContext.DrawGeometry(Background,
            BorderBrush == null || stroke == 0 ? null : new Pen(BorderBrush, stroke), geometry);
        drawingContext.Pop();
    }
}
