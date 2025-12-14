using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using KID;

namespace KID
{
    public static partial class Graphics
    {
        public static Canvas Canvas { get; private set; }

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
    }
}
