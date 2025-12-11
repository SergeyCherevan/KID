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
            if (targetCanvas == null)
                throw new ArgumentNullException(nameof(targetCanvas));
            
            Canvas = targetCanvas;
            dispatcher = Application.Current?.Dispatcher;
        }

        public static void InvokeOnUI(Action action)
        {
            if (action == null)
                return;
            
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action, DispatcherPriority.Background);
            }
        }

        public static T InvokeOnUI<T>(Func<T> func)
        {
            if (func == null)
                return default(T)!;
            
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return func();
            }
            else
            {
                T result = default(T)!;
                dispatcher.Invoke(() => { result = func(); }, DispatcherPriority.Background);
                return result;
            }
        }

        public static void Clear()
        {
            InvokeOnUI(() =>
            {
                Canvas.Children.Clear();
            });
        }
    }
}
