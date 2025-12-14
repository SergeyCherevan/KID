using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        public static Ellipse? Circle(double x, double y, double radius)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
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
                return ellipse;
            });
        }
        public static Ellipse? Circle(Point center, double radius)
        {
            return Circle(center.X, center.Y, radius);
        }

        public static Ellipse? Ellipse(double x, double y, double radiusX, double radiusY)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
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
                return ellipse;
            });
        }
        public static Ellipse? Ellipse(Point center, double radiusX, double radiusY)
        {
            return Ellipse(center.X, center.Y, radiusX, radiusY);
        }

        public static Rectangle? Rectangle(double x, double y, double width, double height)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
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
                return rect;
            });
        }
        public static Rectangle? Rectangle(Point topLeft, double width, double height)
        {
            return Rectangle(topLeft.X, topLeft.Y, width, height);
        }
        public static Rectangle? Rectangle(Point topLeft, Point size)
        {
            return Rectangle(topLeft.X, topLeft.Y, size.X, size.Y);
        }

        public static Line? Line(double x1, double y1, double x2, double y2)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = strokeBrush
                };
                Canvas.Children.Add(line);
                return line;
            });
        }
        public static Line? Line(Point p1, Point p2)
        {
            return Line(p1.X, p1.Y, p2.X, p2.Y);
        }

        public static Path? QuadraticBezier(Point[] points)
        {
            if (points == null || points.Length == 0)
                return null;
            
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                var path = new Path
                {
                    Stroke = strokeBrush,
                    StrokeThickness = 1
                };
                
                var figure = new PathFigure
                {
                    StartPoint = points[0]
                };

                var bezier = new QuadraticBezierSegment
                {
                    Point1 = points[1],
                    Point2 = points[2],
                    IsStroked = true
                };

                figure.Segments.Add(bezier);

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);

                path.Data = geometry;

                Canvas.Children.Add(path);
                return path;
            });
        }
        public static Path? CubicBezier(Point[] points)
        {
            if (points == null || points.Length == 0)
                return null;
            
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                var path = new Path
                {
                    Stroke = strokeBrush,
                    StrokeThickness = 1
                };
                
                var figure = new PathFigure
                {
                    StartPoint = points[0]
                };

                var bezier = new BezierSegment
                {
                    Point1 = points[1],
                    Point2 = points[2],
                    Point3 = points[3],
                    IsStroked = true
                };

                figure.Segments.Add(bezier);

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);

                path.Data = geometry;

                    Canvas.Children.Add(path);
                    return path;
                });
        }
        
        public static Polygon? Polygon(Point[] points)
        {
            if (points == null || points.Length == 0)
                return null;
            
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Canvas == null) return null;
                var polygon = new Polygon
                {
                    Points = [.. points],
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.Children.Add(polygon);
                return polygon;
            });
        }
        public static Polygon? Polygon((double x, double y)[] points)
        {
            return Polygon(points.Select(p => new Point(p.x, p.y)).ToArray());
        }
    }
}