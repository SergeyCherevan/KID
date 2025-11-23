using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Threading;

namespace KID
{
    public static partial class Graphics
    {
        private static Canvas canvas;
        private static Brush fillBrush = Brushes.Black;
        private static Brush strokeBrush = Brushes.Black;
        private static Typeface currentFont = new Typeface("Arial");
        private static double currentFontSize = 20;
        private static Dispatcher dispatcher;

        public static void Init(Canvas targetCanvas)
        {
            canvas = targetCanvas;
            dispatcher = Application.Current.Dispatcher;
        }

        private static void InvokeOnUI(Action action)
        {
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action, DispatcherPriority.Background);
            }
        }
    }
}