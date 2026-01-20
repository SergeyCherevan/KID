using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using KID;

namespace KID
{
    public static partial class Graphics
    {
        public static Canvas Canvas { get; private set; } = null!;

        private static Brush fillBrush = Brushes.Black;
        private static Brush strokeBrush = Brushes.Black;
        private static Typeface currentFont = new Typeface("Arial");
        private static double currentFontSize = 20;

        public static void Init(Canvas targetCanvas)
        {
            if (targetCanvas == null)
                throw new ArgumentNullException(nameof(targetCanvas));
            
            Canvas = targetCanvas;
        }

        public static void Clear()
        {
            DispatcherManager.InvokeOnUI(() =>
            {
                Canvas.Children.Clear();
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
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) throw new ArgumentNullException("Canvas is null");
                return (Canvas.ActualWidth, Canvas.ActualHeight);
            });
        }

        /// <summary>
        /// Устанавливает желаемый размер области рисования (Canvas), изменяя раскладку UI
        /// (эквивалентно программному перемещению <see cref="GridSplitter"/>).
        /// </summary>
        /// <param name="width">Желаемая ширина Canvas в DIP.</param>
        /// <param name="height">Желаемая высота Canvas в DIP.</param>
        /// <remarks>
        /// Метод меняет ширину правой панели (через <see cref="ColumnDefinition.Width"/> внешнего грида)
        /// и высоту строки графики (через <see cref="RowDefinition.Height"/> внутреннего грида).
        /// Если нужные элементы разметки не найдены, применяется fallback на <see cref="FrameworkElement.Width"/>/<see cref="FrameworkElement.Height"/> Canvas.
        /// </remarks>
        public static void SetCanvasSize(double width, double height)
        {
            DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) throw new ArgumentNullException("Canvas is null");
                if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                    throw new ArgumentOutOfRangeException(nameof(width));
                if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
                    throw new ArgumentOutOfRangeException(nameof(height));

                // Находим окно и именованные гриды из MainWindow.xaml
                var window = Window.GetWindow(Canvas);
                if (window == null)
                {
                    // На всякий случай — без окна не можем трогать GridDefinitions
                    Canvas.Width = width;
                    Canvas.Height = height;
                    return;
                }

                var workspaceGrid = window.FindName("WorkspaceGrid") as Grid;
                var outputGrid = window.FindName("OutputGrid") as Grid;

                // Требуемая ширина Canvas = ширина OutputGrid (который с Margin="5") → колонка должна быть шире на margin слева/справа.
                var outputGridHorizontalMargin = outputGrid?.Margin.Left + outputGrid?.Margin.Right ?? 0.0;
                var targetOutputColumnWidth = width + outputGridHorizontalMargin;

                // В MainWindow.xaml: WorkspaceGrid.ColumnDefinitions = [code(*), splitter(5), output(310 min 310)]
                if (workspaceGrid?.ColumnDefinitions != null && workspaceGrid.ColumnDefinitions.Count >= 3)
                {
                    var outputCol = workspaceGrid.ColumnDefinitions[2];
                    // Учитываем MinWidth (и не даём уйти в отрицательное)
                    var clamped = Math.Max(outputCol.MinWidth, targetOutputColumnWidth);
                    outputCol.Width = new GridLength(clamped, GridUnitType.Pixel);
                }
                else
                {
                    // Без грида — fallback на фиксированный размер самого Canvas (не меняет раскладку, но хоть как-то работает)
                    Canvas.Width = width;
                }

                // В MainWindow.xaml: OutputGrid.RowDefinitions = [console(*), splitter(5), graphics(min 300)]
                if (outputGrid?.RowDefinitions != null && outputGrid.RowDefinitions.Count >= 3)
                {
                    var graphicsRow = outputGrid.RowDefinitions[2];
                    var clamped = Math.Max(graphicsRow.MinHeight, height);
                    graphicsRow.Height = new GridLength(clamped, GridUnitType.Pixel);
                }
                else
                {
                    Canvas.Height = height;
                }
            });
        }
    }
}
