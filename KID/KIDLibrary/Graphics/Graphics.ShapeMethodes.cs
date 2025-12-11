using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        public static Shape? SetLeftX(this Shape element, double x)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x);
                return element;
            });
        }
        public static Shape? SetTopY(this Shape element, double y)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetTop(element, y);
                return element;
            });
        }
        public static Shape? SetLeftTopXY(this Shape element, double x, double y)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
                return element;
            });
        }
        public static Shape? SetLeftTopXY(this Shape element, Point point)
        {
            return SetLeftTopXY(element, point.X, point.Y);
        }

        public static Shape? SetCenterX(this Shape element, double x)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x - element.ActualHeight / 2);
                return element;
            });
        }
        public static Shape? SetCenterY(this Shape element, double y)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetTop(element, y - element.ActualHeight / 2);
                return element;
            });
        }
        public static Shape? SetCenterXY(this Shape element, double x, double y)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.SetLeft(element, x - element.ActualWidth / 2);
                Canvas.SetTop(element, y - element.ActualHeight / 2);
                return element;
            });
        }
        public static Shape? SetCenterXY(this Shape element, Point point)
        {
            return SetCenterXY(element, point.X, point.Y);
        }

        public static Shape? SetWidth(this Shape element, double width)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Width = width;
                return element;
            });
        }
        public static Shape? SetHeight(this Shape element, double height)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Height = height;
                return element;
            });
        }
        public static Shape? SetSize(this Shape element, double width, double height)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Width = width;
                element.Height = height;
                return element;
            });
        }
        public static Shape? SetSize(this Shape element, Point size)
        {
            return SetSize(element, size.X, size.Y);
        }

        public static Shape? SetStrokeColor(this Shape element, ColorType color)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Stroke = color.CreateBrush();
                return element;
            });
        }
        public static Shape? SetFillColor(this Shape element, ColorType color)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Fill = color.CreateBrush();
                return element;
            });
        }
        public static Shape? SetColor(this Shape element, ColorType color)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                element.Stroke = color.CreateBrush();
                element.Fill = color.CreateBrush();
                return element;
            });
        }
        
        public static Shape? AddToCanvas(this Shape element)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.Children.Add(element);
                return element;
            });
        }

        public static Shape? RemoveFromCanvas(this Shape element)
        {
            return InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                Canvas.Children.Remove(element);
                return element;
            });
        }
    }
}