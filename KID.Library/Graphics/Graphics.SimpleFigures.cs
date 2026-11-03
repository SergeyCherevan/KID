using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KID
{
    public static partial class Graphics
    {
        public static Rectangle Plot(double x, double y)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                var point = new Rectangle
                {
                    Width = 1,
                    Height = 1,
                    Fill = fillBrush,
                    SnapsToDevicePixels = true
                };
                Canvas.SetLeft(point, x);
                Canvas.SetTop(point, y);
                targetCanvas.Children.Add(point);
                return point;
            });
        }
        public static Rectangle Plot(Point point)
        {
            return Plot(point.X, point.Y);
        }

        public static Ellipse Circle(double x, double y, double radius)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                var ellipse = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.SetLeft(ellipse, x - radius);
                Canvas.SetTop(ellipse, y - radius);
                targetCanvas.Children.Add(ellipse);
                return ellipse;
            });
        }
        public static Ellipse Circle(Point center, double radius)
        {
            return Circle(center.X, center.Y, radius);
        }

        public static Ellipse Ellipse(double x, double y, double radiusX, double radiusY)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                var ellipse = new Ellipse
                {
                    Width = radiusX * 2,
                    Height = radiusY * 2,
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.SetLeft(ellipse, x - radiusX);
                Canvas.SetTop(ellipse, y - radiusY);
                targetCanvas.Children.Add(ellipse);
                return ellipse;
            });
        }
        public static Ellipse Ellipse(Point center, double radiusX, double radiusY)
        {
            return Ellipse(center.X, center.Y, radiusX, radiusY);
        }

        public static Rectangle Rectangle(double x, double y, double width, double height)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                var rect = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                targetCanvas.Children.Add(rect);
                return rect;
            });
        }
        public static Rectangle Rectangle(Point topLeft, double width, double height)
        {
            return Rectangle(topLeft.X, topLeft.Y, width, height);
        }
        public static Rectangle Rectangle(Point topLeft, Point size)
        {
            return Rectangle(topLeft.X, topLeft.Y, size.X, size.Y);
        }

        public static Line Line(double x1, double y1, double x2, double y2)
        {
            return InvokeOnCanvas(targetCanvas =>
            {
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = strokeBrush
                };
                targetCanvas.Children.Add(line);
                return line;
            });
        }
        public static Line Line(Point p1, Point p2)
        {
            return Line(p1.X, p1.Y, p2.X, p2.Y);
        }

        public static Path QuadraticBezier(Point[] points)
        {
            ArgumentNullException.ThrowIfNull(points);
            if (points.Length < 3)
                throw new ArgumentException("At least three points are required.", nameof(points));

            return InvokeOnCanvas(targetCanvas =>
            {
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

                targetCanvas.Children.Add(path);
                return path;
            });
        }
        public static Path CubicBezier(Point[] points)
        {
            ArgumentNullException.ThrowIfNull(points);
            if (points.Length < 4)
                throw new ArgumentException("At least four points are required.", nameof(points));

            return InvokeOnCanvas(targetCanvas =>
            {
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

                targetCanvas.Children.Add(path);
                return path;
            });
        }
        
        public static Polygon Polygon(Point[] points)
        {
            ArgumentNullException.ThrowIfNull(points);
            if (points.Length < 1)
                throw new ArgumentException("At least one point is required.", nameof(points));

            var target = CaptureTarget();
            return Polygon(target, points);
        }

        private static Polygon Polygon(CanvasTarget target, IEnumerable<Point> points)
        {
            return DispatcherManager.InvokeOnUI(target.Scope, () =>
            {
                var polygon = new Polygon
                {
                    Points = CreatePoints(points, target.Scope.Environment),
                    Fill = fillBrush,
                    Stroke = strokeBrush
                };
                target.Canvas.Children.Add(polygon);
                return polygon;
            });
        }
        public static Polygon Polygon((double x, double y)[] points)
        {
            var target = CaptureTarget();
            target.Scope.Environment.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(points);
            var converted = new Point[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                target.Scope.Environment.ThrowIfCancellationRequested();
                converted[i] = new Point(points[i].x, points[i].y);
            }
            return Polygon(target, converted);
        }

        private static PointCollection CreatePoints(
            IEnumerable<Point> points,
            ExecutionEnvironment environment)
        {
            var result = new PointCollection();
            foreach (var point in points)
            {
                environment.ThrowIfCancellationRequested();
                result.Add(point);
            }
            return result;
        }
    }
}
