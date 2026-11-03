using System.Windows;
using System.Windows.Controls;

namespace KID
{
    public static partial class Graphics
    {
        /// <summary>
        /// Возвращает текущую ширину области рисования (Canvas) в DIP после раскладки WPF.
        /// </summary>
        public static double GetCanvasWidth()
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                return targetCanvas.ActualWidth;
            });
        }

        /// <summary>
        /// Возвращает текущую высоту области рисования (Canvas) в DIP после раскладки WPF.
        /// </summary>
        public static double GetCanvasHeight()
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                return targetCanvas.ActualHeight;
            });
        }

        /// <summary>
        /// Возвращает текущий реальный размер области рисования (Canvas) в DIP.
        /// </summary>
        /// <remarks>
        /// Это именно <see cref="FrameworkElement.ActualWidth"/>/<see cref="FrameworkElement.ActualHeight"/>,
        /// т.е. размер, вычисленный WPF-разметкой (и зависящий от положения <see cref="GridSplitter"/>).
        /// </remarks>
        /// <returns>Кортеж (width, height) в DIP.</returns>
        public static (double width, double height) GetCanvasSize()
        {
            return InvokeOnCanvas(targetCanvas => (targetCanvas.ActualWidth, targetCanvas.ActualHeight));
        }

        private static Window? TryGetWindow(Canvas targetCanvas)
        {
            return Window.GetWindow(targetCanvas);
        }

        private static Grid? TryGetWorkspaceGrid(Window window) => window.FindName("WorkspaceGrid") as Grid;

        private static Grid? TryGetOutputGrid(Window window) => window.FindName("OutputGrid") as Grid;

        private static FrameworkElement? TryGetGraphicsOutputView(Window window) => window.FindName("GraphicsOutputView") as FrameworkElement;

        private static double ComputeCanvasToOutputColumnDeltaWidth(Canvas targetCanvas, Grid? outputGrid)
        {
            // Best-effort: if layout is measured, use actual delta; otherwise fallback to margins.
            if (outputGrid != null && outputGrid.ActualWidth > 0 && targetCanvas.ActualWidth > 0)
            {
                var delta = outputGrid.ActualWidth - targetCanvas.ActualWidth;
                if (!double.IsNaN(delta) && !double.IsInfinity(delta) && delta >= 0)
                    return delta;
            }

            return (outputGrid?.Margin.Left ?? 0) + (outputGrid?.Margin.Right ?? 0);
        }

        private static double ComputeCanvasToGraphicsRowDeltaHeight(Canvas targetCanvas, FrameworkElement? graphicsOutputView)
        {
            if (graphicsOutputView != null && graphicsOutputView.ActualHeight > 0 && targetCanvas.ActualHeight > 0)
            {
                var delta = graphicsOutputView.ActualHeight - targetCanvas.ActualHeight;
                if (!double.IsNaN(delta) && !double.IsInfinity(delta) && delta >= 0)
                    return delta;
            }

            // In current layout Canvas is direct child of GraphicsOutputView with no known padding/margins.
            return 0;
        }

        /// <summary>
        /// Устанавливает минимальную ширину Canvas (в DIP) через min-ограничение разметки (Grid.ColumnDefinition.MinWidth).
        /// </summary>
        public static void SetCanvasMinWidth(double minWidth) =>
            InvokeOnCanvas(targetCanvas => SetCanvasMinWidth(targetCanvas, minWidth));

        private static void SetCanvasMinWidth(Canvas targetCanvas, double minWidth)
        {
            if (double.IsNaN(minWidth) || double.IsInfinity(minWidth) || minWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(minWidth));

            var window = TryGetWindow(targetCanvas);
            if (window == null)
                return;

            var workspaceGrid = TryGetWorkspaceGrid(window);
            var outputGrid = TryGetOutputGrid(window);
            if (workspaceGrid?.ColumnDefinitions == null || workspaceGrid.ColumnDefinitions.Count < 3)
                return;

            var outputCol = workspaceGrid.ColumnDefinitions[2];
            var deltaW = ComputeCanvasToOutputColumnDeltaWidth(targetCanvas, outputGrid);
            outputCol.MinWidth = minWidth + deltaW;
        }

        /// <summary>
        /// Устанавливает минимальную высоту Canvas (в DIP) через min-ограничение разметки (Grid.RowDefinition.MinHeight).
        /// </summary>
        public static void SetCanvasMinHeight(double minHeight) =>
            InvokeOnCanvas(targetCanvas => SetCanvasMinHeight(targetCanvas, minHeight));

        private static void SetCanvasMinHeight(Canvas targetCanvas, double minHeight)
        {
            if (double.IsNaN(minHeight) || double.IsInfinity(minHeight) || minHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(minHeight));

            var window = TryGetWindow(targetCanvas);
            if (window == null)
                return;

            var outputGrid = TryGetOutputGrid(window);
            if (outputGrid?.RowDefinitions == null || outputGrid.RowDefinitions.Count < 3)
                return;

            var graphicsRow = outputGrid.RowDefinitions[2];
            var graphicsOutputView = TryGetGraphicsOutputView(window);
            var deltaH = ComputeCanvasToGraphicsRowDeltaHeight(targetCanvas, graphicsOutputView);
            graphicsRow.MinHeight = minHeight + deltaH;
        }

        /// <summary>
        /// Устанавливает минимальный размер Canvas (в DIP) через min-ограничения разметки.
        /// </summary>
        public static void SetCanvasMinSize(double minWidth, double minHeight)
        {
            InvokeOnCanvas(targetCanvas =>
            {
                SetCanvasMinWidth(targetCanvas, minWidth);
                SetCanvasMinHeight(targetCanvas, minHeight);
            });
        }

        /// <summary>
        /// Устанавливает желаемую ширину Canvas (в DIP), изменяя раскладку UI (эквивалентно перемещению GridSplitter).
        /// </summary>
        public static void SetCanvasWidth(double width) =>
            InvokeOnCanvas(targetCanvas => SetCanvasWidth(targetCanvas, width));

        private static void SetCanvasWidth(Canvas targetCanvas, double width)
        {
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));

            var window = TryGetWindow(targetCanvas);
            if (window == null)
            {
                targetCanvas.Width = width;
                return;
            }

            var workspaceGrid = TryGetWorkspaceGrid(window);
            var outputGrid = TryGetOutputGrid(window);
            if (workspaceGrid?.ColumnDefinitions == null || workspaceGrid.ColumnDefinitions.Count < 3)
            {
                targetCanvas.Width = width;
                return;
            }

            var outputCol = workspaceGrid.ColumnDefinitions[2];
            var deltaW = ComputeCanvasToOutputColumnDeltaWidth(targetCanvas, outputGrid);
            var targetColWidth = width + deltaW;

            // Allow width < current MinWidth by lowering MinWidth when needed.
            if (outputCol.MinWidth > targetColWidth)
                outputCol.MinWidth = targetColWidth;

            outputCol.Width = new GridLength(targetColWidth, GridUnitType.Pixel);
        }

        /// <summary>
        /// Устанавливает желаемую высоту Canvas (в DIP), изменяя раскладку UI (эквивалентно перемещению GridSplitter).
        /// </summary>
        public static void SetCanvasHeight(double height) =>
            InvokeOnCanvas(targetCanvas => SetCanvasHeight(targetCanvas, height));

        private static void SetCanvasHeight(Canvas targetCanvas, double height)
        {
            if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            var window = TryGetWindow(targetCanvas);
            if (window == null)
            {
                targetCanvas.Height = height;
                return;
            }

            var outputGrid = TryGetOutputGrid(window);
            if (outputGrid?.RowDefinitions == null || outputGrid.RowDefinitions.Count < 3)
            {
                targetCanvas.Height = height;
                return;
            }

            var graphicsRow = outputGrid.RowDefinitions[2];
            var graphicsOutputView = TryGetGraphicsOutputView(window);
            var deltaH = ComputeCanvasToGraphicsRowDeltaHeight(targetCanvas, graphicsOutputView);
            var targetRowHeight = height + deltaH;

            // Allow height < current MinHeight by lowering MinHeight when needed.
            if (graphicsRow.MinHeight > targetRowHeight)
                graphicsRow.MinHeight = targetRowHeight;

            graphicsRow.Height = new GridLength(targetRowHeight, GridUnitType.Pixel);
        }

        /// <summary>
        /// Устанавливает желаемый размер Canvas (в DIP), изменяя раскладку UI
        /// (эквивалентно программному перемещению <see cref="GridSplitter"/>).
        /// </summary>
        public static void SetCanvasSize(double width, double height)
        {
            InvokeOnCanvas(targetCanvas =>
            {
                SetCanvasWidth(targetCanvas, width);
                SetCanvasHeight(targetCanvas, height);
            });
        }
    }
}

