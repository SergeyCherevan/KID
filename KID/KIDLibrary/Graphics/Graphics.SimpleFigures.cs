using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        public static void Circle(double x, double y, double radius)
        {
            InvokeOnUI(() =>
            {
                if (Canvas == null) return;
                var ellipse = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.SetLeft(ellipse, x - radius);
                Canvas.SetTop(ellipse, y - radius);
                Canvas.Children.Add(ellipse);
            });
        }

        public static void Circle(Point center, double radius)
        {
            Circle(center.X, center.Y, radius);
        }

        public static void Ellipse(double x, double y, double radiusX, double radiusY)
        {
            InvokeOnUI(() =>
            {
                if (Canvas == null) return;
                var ellipse = new Ellipse
                {
                    Width = radiusX * 2,
                    Height = radiusY * 2,
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.SetLeft(ellipse, x - radiusX);
                Canvas.SetTop(ellipse, y - radiusY);
                Canvas.Children.Add(ellipse);
            });
        }

        public static void Ellipse(Point center, double radiusX, double radiusY)
        {
            Ellipse(center.X, center.Y, radiusX, radiusY);
        }

        public static void Rectangle(double x, double y, double width, double height)
        {
            InvokeOnUI(() =>
            {
                if (Canvas == null) return;
                var rect = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                Canvas.Children.Add(rect);
            });
        }

        public static void Rectangle(Point topLeft, double width, double height)
        {
            Rectangle(topLeft.X, topLeft.Y, width, height);
        }

        public static void Rectangle(Point topLeft, Point size)
        {
            Rectangle(topLeft.X, topLeft.Y, size.X, size.Y);
        }

        public static void Line(double x1, double y1, double x2, double y2)
        {
            InvokeOnUI(() =>
            {
                if (Canvas == null) return;
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = strokeBrush
                };
                Canvas.Children.Add(line);
            });
        }

        public static void Line(Point p1, Point p2)
        {
            Line(p1.X, p1.Y, p2.X, p2.Y);
        }

        public static void Polygon((double x, double y)[] points)
        {
            InvokeOnUI(() =>
            {
                if (Canvas == null) return;
                var polygon = new Polygon
                {
                    Points = [.. points.Select(p => new Point(p.x, p.y))],
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.Children.Add(polygon);
            });
        }

        public static void Polygon(Point[] points)
        {
            Polygon(points.Select(p => (p.X, p.Y)).ToArray());
        }
    }
}