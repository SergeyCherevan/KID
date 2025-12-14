using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        // Общие методы для UIElement (позиционирование)
        public static UIElement? SetLeftX(this UIElement element, double x)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x);
                return element;
            });
        }
        public static UIElement? SetTopY(this UIElement element, double y)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetTop(element, y);
                return element;
            });
        }
        public static UIElement? SetLeftTopXY(this UIElement element, double x, double y)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
                return element;
            });
        }
        public static UIElement? SetLeftTopXY(this UIElement element, Point point)
        {
            return SetLeftTopXY(element, point.X, point.Y);
        }

        // Общие методы для FrameworkElement (центрирование и размеры)
        public static FrameworkElement? SetCenterX(this FrameworkElement element, double x)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x - element.ActualWidth / 2);
                return element;
            });
        }
        public static FrameworkElement? SetCenterY(this FrameworkElement element, double y)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetTop(element, y - element.ActualHeight / 2);
                return element;
            });
        }
        public static FrameworkElement? SetCenterXY(this FrameworkElement element, double x, double y)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x - element.ActualWidth / 2);
                Canvas.SetTop(element, y - element.ActualHeight / 2);
                return element;
            });
        }
        public static FrameworkElement? SetCenterXY(this FrameworkElement element, Point point)
        {
            return SetCenterXY(element, point.X, point.Y);
        }

        public static FrameworkElement? SetWidth(this FrameworkElement element, double width)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Width = width;
                return element;
            });
        }
        public static FrameworkElement? SetHeight(this FrameworkElement element, double height)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Height = height;
                return element;
            });
        }
        public static FrameworkElement? SetSize(this FrameworkElement element, double width, double height)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Width = width;
                element.Height = height;
                return element;
            });
        }
        public static FrameworkElement? SetSize(this FrameworkElement element, Point size)
        {
            return SetSize(element, size.X, size.Y);
        }

        // Общие методы для UIElement (управление Canvas)
        public static UIElement? AddToCanvas(this UIElement element)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.Children.Add(element);
                return element;
            });
        }

        public static UIElement? RemoveFromCanvas(this UIElement element)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.Children.Remove(element);
                return element;
            });
        }

        // Методы только для Shape (цвета)
        public static Shape? SetStrokeColor(this Shape element, ColorType color)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Stroke = color.CreateBrush();
                return element;
            });
        }
        public static Shape? SetFillColor(this Shape element, ColorType color)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Fill = color.CreateBrush();
                return element;
            });
        }
        public static Shape? SetColor(this Shape element, ColorType color)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Stroke = color.CreateBrush();
                element.Fill = color.CreateBrush();
                return element;
            });
        }
    }
}