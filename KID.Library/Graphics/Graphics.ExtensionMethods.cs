using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        private static T InvokeOnElement<T>(UIElement element, Func<T> action)
        {
            var scope = DispatcherManager.GetScope();
            GraphicsElementAccess.ValidateDispatcher(element, scope);
            return DispatcherManager.InvokeOnUI(scope, action);
        }

        // Общие методы для UIElement (позиционирование)
        public static UIElement SetLeftX(this UIElement element, double x)
        {
            return InvokeOnElement(element, () =>
            {
                Canvas.SetLeft(element, x);
                return element;
            });
        }
        public static UIElement SetTopY(this UIElement element, double y)
        {
            return InvokeOnElement(element, () =>
            {
                Canvas.SetTop(element, y);
                return element;
            });
        }
        public static UIElement SetLeftTopXY(this UIElement element, double x, double y)
        {
            return InvokeOnElement(element, () =>
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
            return InvokeOnElement(element, () =>
            {
                return Canvas.GetLeft(element);
            });
        }
        public static double GetTopY(this UIElement element)
        {
            return InvokeOnElement(element, () =>
            {
                return Canvas.GetTop(element);
            });
        }
        public static Point GetLeftTopXY(this UIElement element)
        {
            return InvokeOnElement(element, () => new Point(Canvas.GetLeft(element), Canvas.GetTop(element)));
        }

        // Общие методы для FrameworkElement (центрирование и размеры)
        public static FrameworkElement SetCenterX(this FrameworkElement element, double x)
        {
            return InvokeOnElement(element, () =>
            {
                Canvas.SetLeft(element, x - element.ActualWidth / 2);
                return element;
            });
        }
        public static FrameworkElement SetCenterY(this FrameworkElement element, double y)
        {
            return InvokeOnElement(element, () =>
            {
                Canvas.SetTop(element, y - element.ActualHeight / 2);
                return element;
            });
        }
        public static FrameworkElement SetCenterXY(this FrameworkElement element, double x, double y)
        {
            return InvokeOnElement(element, () =>
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
            return InvokeOnElement(element, () => Canvas.GetLeft(element) + element.ActualWidth / 2);
        }
        public static double GetCenterY(this FrameworkElement element)
        {
            return InvokeOnElement(element, () => Canvas.GetTop(element) + element.ActualHeight / 2);
        }
        public static Point GetCenterXY(this FrameworkElement element)
        {
            return InvokeOnElement(element, () => new Point(
                Canvas.GetLeft(element) + element.ActualWidth / 2,
                Canvas.GetTop(element) + element.ActualHeight / 2));
        }

        public static UIElement MoveToRight(this UIElement element, double x)
        {
            return InvokeOnElement(element, () =>
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + x);
                return element;
            });
        }
        public static UIElement MoveToBottom(this UIElement element, double y)
        {
            return InvokeOnElement(element, () =>
            {
                Canvas.SetTop(element, Canvas.GetTop(element) + y);
                return element;
            });
        }
        public static UIElement MoveToRightBottom(this UIElement element, double x, double y)
        {
            return InvokeOnElement(element, () =>
            {
                Canvas.SetLeft(element, Canvas.GetLeft(element) + x);
                Canvas.SetTop(element, Canvas.GetTop(element) + y);
                return element;
            });
        }

        public static FrameworkElement SetWidth(this FrameworkElement element, double width)
        {
            return InvokeOnElement(element, () =>
            {
                element.Width = width;
                return element;
            });
        }
        public static FrameworkElement SetHeight(this FrameworkElement element, double height)
        {
            return InvokeOnElement(element, () =>
            {
                element.Height = height;
                return element;
            });
        }
        public static FrameworkElement SetSize(this FrameworkElement element, double width, double height)
        {
            return InvokeOnElement(element, () =>
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
            var target = CaptureTarget();
            GraphicsElementAccess.ValidateDispatcher(element, target.Scope);
            return DispatcherManager.InvokeOnUI(target.Scope, () =>
            {
                if (GraphicsElementAccess.GetParent(element) != null)
                    throw new InvalidOperationException("The element already has a parent. Remove it before adding it.");
                target.Canvas.Children.Add(element);
                return element;
            });
        }

        public static UIElement RemoveFromCanvas(this UIElement element)
        {
            return InvokeOnElement(element, () =>
            {
                GraphicsElementAccess.GetParentCanvas(element)?.Children.Remove(element);
                return element;
            });
        }

        // Методы только для Shape (цвета)
        public static Shape SetStrokeColor(this Shape element, ColorType color)
        {
            return InvokeOnElement(element, () =>
            {
                element.Stroke = color.CreateBrush();
                return element;
            });
        }
        public static Shape SetFillColor(this Shape element, ColorType color)
        {
            return InvokeOnElement(element, () =>
            {
                element.Fill = color.CreateBrush();
                return element;
            });
        }
        public static Shape SetColor(this Shape element, ColorType color)
        {
            return InvokeOnElement(element, () =>
            {
                element.Stroke = color.CreateBrush();
                element.Fill = color.CreateBrush();
                return element;
            });
        }
    }
}
