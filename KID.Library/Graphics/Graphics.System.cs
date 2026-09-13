using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using KID;

namespace KID
{
    public static partial class Graphics
    {
        public static Canvas Canvas { get; private set; } = null!;

        private static Brush fillBrush = Brushes.Black;
        private static Brush strokeBrush = Brushes.Black;
        private static Typeface currentFont = new Typeface("Arial");
        private static double currentFontSize = 20;
        private static ExecutionDispatcherScope? owner;
        private static readonly object ownershipGate = new();

        internal static void Init(Canvas targetCanvas, ExecutionDispatcherScope scope)
        {
            if (targetCanvas == null)
                throw new ArgumentNullException(nameof(targetCanvas));
            
            targetCanvas.VerifyAccess();
            lock (ownershipGate)
            {
                if (owner != null) throw new InvalidOperationException("Graphics are already initialized.");
                owner = scope;
                Canvas = targetCanvas;
                ResetDefaults();
            }
        }

        // Host-only, без проверки Stop. Compare-and-release защищает новую сессию.
        internal static void Release(ExecutionDispatcherScope scope)
        {
            lock (ownershipGate)
            {
                if (!ReferenceEquals(owner, scope)) return;
                Canvas = null!;
                ResetDefaults();
                owner = null;
            }
        }

        private static void ResetDefaults()
        {
            fillBrush = Brushes.Black;
            strokeBrush = Brushes.Black;
            currentFont = new Typeface("Arial");
            currentFontSize = 20;
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
