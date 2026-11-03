namespace KID;

public static partial class Graphics
{
    /// <summary>Возвращает фактическую ширину Canvas в DIP после раскладки WPF.</summary>
    public static double GetCanvasWidth() => InvokeOnCanvas(canvas => canvas.ActualWidth);

    /// <summary>Возвращает фактическую высоту Canvas в DIP после раскладки WPF.</summary>
    public static double GetCanvasHeight() => InvokeOnCanvas(canvas => canvas.ActualHeight);

    /// <summary>Возвращает фактический размер Canvas в DIP; до раскладки он может быть нулевым.</summary>
    public static (double width, double height) GetCanvasSize() =>
        InvokeOnCanvas(canvas => (canvas.ActualWidth, canvas.ActualHeight));

    /// <summary>Устанавливает Canvas.MinWidth в DIP или ограничение разметки, заданное хостом IDE.</summary>
    public static void SetCanvasMinWidth(double minWidth) =>
        SetCanvasDimension(CanvasSizeProperty.MinWidth, minWidth);

    /// <summary>Устанавливает Canvas.MinHeight в DIP или ограничение разметки, заданное хостом IDE.</summary>
    public static void SetCanvasMinHeight(double minHeight) =>
        SetCanvasDimension(CanvasSizeProperty.MinHeight, minHeight);

    /// <summary>Устанавливает минимальные размеры Canvas в DIP.</summary>
    public static void SetCanvasMinSize(double minWidth, double minHeight)
    {
        var target = CaptureTarget();
        DispatcherManager.InvokeOnUI(target.Scope, () =>
        {
            SetCanvasDimension(target, CanvasSizeProperty.MinWidth, minWidth);
            SetCanvasDimension(target, CanvasSizeProperty.MinHeight, minHeight);
        });
    }

    /// <summary>Устанавливает Canvas.Width в DIP или ширину панели, заданную хостом IDE.</summary>
    public static void SetCanvasWidth(double width) => SetCanvasDimension(CanvasSizeProperty.Width, width);

    /// <summary>Устанавливает Canvas.Height в DIP или высоту панели, заданную хостом IDE.</summary>
    public static void SetCanvasHeight(double height) => SetCanvasDimension(CanvasSizeProperty.Height, height);

    /// <summary>Устанавливает желаемые размеры Canvas в DIP.</summary>
    public static void SetCanvasSize(double width, double height)
    {
        var target = CaptureTarget();
        DispatcherManager.InvokeOnUI(target.Scope, () =>
        {
            SetCanvasDimension(target, CanvasSizeProperty.Width, width);
            SetCanvasDimension(target, CanvasSizeProperty.Height, height);
        });
    }

    private static void SetCanvasDimension(CanvasSizeProperty property, double value)
    {
        var target = CaptureTarget();
        DispatcherManager.InvokeOnUI(target.Scope, () => SetCanvasDimension(target, property, value));
    }

    private static void SetCanvasDimension(CanvasTarget target, CanvasSizeProperty property, double value)
    {
        var minimum = property is CanvasSizeProperty.MinWidth or CanvasSizeProperty.MinHeight;
        if (!double.IsFinite(value) || (minimum ? value < 0 : value <= 0))
            throw new ArgumentOutOfRangeException(property switch
            {
                CanvasSizeProperty.Width => "width",
                CanvasSizeProperty.Height => "height",
                CanvasSizeProperty.MinWidth => "minWidth",
                _ => "minHeight"
            });

        if (target.SizeHandler?.Invoke(target.Canvas, property, value) == true) return;
        switch (property)
        {
            case CanvasSizeProperty.Width: target.Canvas.Width = value; break;
            case CanvasSizeProperty.Height: target.Canvas.Height = value; break;
            case CanvasSizeProperty.MinWidth: target.Canvas.MinWidth = value; break;
            case CanvasSizeProperty.MinHeight: target.Canvas.MinHeight = value; break;
        }
    }
}
