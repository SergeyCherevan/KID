using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Threading;

namespace KID
{
    public static partial class Graphics
    {
        public static Canvas Canvas { get; private set; }

        private static Brush fillBrush = Brushes.Black;
        private static Brush strokeBrush = Brushes.Black;
        private static Typeface currentFont = new Typeface("Arial");
        private static double currentFontSize = 20;
        private static Dispatcher dispatcher;

        public static void Init(Canvas targetCanvas)
        {
            Canvas = targetCanvas;
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