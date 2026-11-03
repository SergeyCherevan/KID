using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        // Общие методы для UIElement (позиционирование)
        public static UIElement SetLeftX(this UIElement element, double x)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetLeft(element, x);
                return element;
            });
        }
        public static UIElement SetTopY(this UIElement element, double y)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetTop(element, y);
                return element;
            });
        }
        public static UIElement SetLeftTopXY(this UIElement element, double x, double y)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
                return element;
            });
        }
        public static UIElement SetLeftTopXY(this UIElement element, Point point)
        {
            return SetLeftTopXY(element, point.X, point.Y);
        }

        public static double GetLeftX(this UIElement element)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                return Canvas.GetLeft(element);
            });
        }
        public static double GetTopY(this UIElement element)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                return Canvas.GetTop(element);
            });
        }
        public static Point GetLeftTopXY(this UIElement element)
        {
            return InvokeOnCanvas(targetCanvas => new Point(Canvas.GetLeft(element), Canvas.GetTop(element)));
        }

        // Общие методы для FrameworkElement (центрирование и размеры)
        public static FrameworkElement SetCenterX(this FrameworkElement element, double x)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetLeft(element, x - element.ActualWidth / 2);
                return element;
            });
        }
        public static FrameworkElement SetCenterY(this FrameworkElement element, double y)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetTop(element, y - element.ActualHeight / 2);
                return element;
            });
        }
        public static FrameworkElement SetCenterXY(this FrameworkElement element, double x, double y)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetLeft(element, x - element.ActualWidth / 2);
                Canvas.SetTop(element, y - element.ActualHeight / 2);
                return element;
            });
        }
        public static FrameworkElement SetCenterXY(this FrameworkElement element, Point point)
        {
            return SetCenterXY(element, point.X, point.Y);
        }

        public static double GetCenterX(this FrameworkElement element)
        {
            return InvokeOnCanvas(targetCanvas => Canvas.GetLeft(element) + element.ActualWidth / 2);
        }
        public static double GetCenterY(this FrameworkElement element)
        {
            return InvokeOnCanvas(targetCanvas => Canvas.GetTop(element) + element.ActualHeight / 2);
        }
        public static Point GetCenterXY(this FrameworkElement element)
        {
            return InvokeOnCanvas(targetCanvas => new Point(
                Canvas.GetLeft(element) + element.ActualWidth / 2,
                Canvas.GetTop(element) + element.ActualHeight / 2));
        }

        public static UIElement MoveToRight(this UIElement element, double x)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + x);
                return element;
            });
        }
        public static UIElement MoveToBottom(this UIElement element, double y)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetTop(element, Canvas.GetTop(element) + y);
                return element;
            });
        }
        public static UIElement MoveToRightBottom(this UIElement element, double x, double y)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + x);
                Canvas.SetTop(element, Canvas.GetTop(element) + y);
                return element;
            });
        }

        public static FrameworkElement SetWidth(this FrameworkElement element, double width)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                element.Width = width;
                return element;
            });
        }
        public static FrameworkElement SetHeight(this FrameworkElement element, double height)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                element.Height = height;
                return element;
            });
        }
        public static FrameworkElement SetSize(this FrameworkElement element, double width, double height)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                element.Width = width;
                element.Height = height;
                return element;
            });
        }
        public static FrameworkElement SetSize(this FrameworkElement element, Point size)
        {
            return element.SetSize(size.X, size.Y);
        }

        // Общие методы для UIElement (управление Canvas)
        public static UIElement AddToCanvas(this UIElement element)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                targetCanvas.Children.Add(element);
                return element;
            });
        }

        public static UIElement RemoveFromCanvas(this UIElement element)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                targetCanvas.Children.Remove(element);
                return element;
            });
        }

        // Методы только для Shape (цвета)
        public static Shape SetStrokeColor(this Shape element, ColorType color)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                element.Stroke = color.CreateBrush();
                return element;
            });
        }
        public static Shape SetFillColor(this Shape element, ColorType color)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                element.Fill = color.CreateBrush();
                return element;
            });
        }
        public static Shape SetColor(this Shape element, ColorType color)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                element.Stroke = color.CreateBrush();
                element.Fill = color.CreateBrush();
                return element;
            });
        }
    }
}
