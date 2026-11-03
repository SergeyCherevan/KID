using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using KID;

namespace KID
{
    public static partial class Graphics
    {
        private static Canvas canvas = null!;

        public static Canvas Canvas { get { lock (ownershipGate) return canvas; } }

        private static Brush fillBrush = Brushes.Black;
        private static Brush strokeBrush = Brushes.Black;
        private static Typeface currentFont = new Typeface("Arial");
        private static double currentFontSize = 20;
        private static DispatcherScope? owner;
        private static readonly Lock ownershipGate = new();

        internal static void Init(Canvas targetCanvas, DispatcherScope scope)
        {
            if (targetCanvas == null)
                throw new ArgumentNullException(nameof(targetCanvas));
            
            targetCanvas.VerifyAccess();
            lock (ownershipGate)
            {
                if (owner != null) throw new InvalidOperationException("Graphics are already initialized.");
                owner = scope;
                canvas = targetCanvas;
                ResetDefaults();
            }
        }

        // Host-only, без проверки Stop. Compare-and-release защищает новую сессию.
        internal static void Release(DispatcherScope scope)
        {
            lock (ownershipGate)
            {
                if (!ReferenceEquals(owner, scope)) return;
                canvas = null!;
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

        /// <summary>
        /// Выбирает холст активной графической сессии без ожидания очереди UI.
        /// Содержимое, оформление и подключённые компоненты сессии сохраняются.
        /// </summary>
        /// <exception cref="ArgumentNullException">Холст не задан.</exception>
        /// <exception cref="InvalidOperationException">Графика не подключена.</exception>
        /// <exception cref="ObjectDisposedException">Сессия закрыла приём команд.</exception>
        /// <exception cref="ArgumentException">Холст принадлежит другому Dispatcher.</exception>
        public static void SetCanvas(Canvas targetCanvas)
        {
            ArgumentNullException.ThrowIfNull(targetCanvas);
            lock (ownershipGate)
            {
                var scope = owner ?? throw new InvalidOperationException("Graphics Canvas is not initialized.");
                using var admission = scope.Environment.EnterWork();
                if (!scope.Environment.OwnsDispatcher(scope))
                    throw new ObjectDisposedException(nameof(DispatcherScope));
                if (!ReferenceEquals(targetCanvas.Dispatcher, scope.Dispatcher))
                    throw new ArgumentException("The Canvas must use the session Dispatcher.", nameof(targetCanvas));
                canvas = targetCanvas;
            }
        }

        // Capture the destination and its owner together; dispatch must never resolve a newer scope.
        private readonly record struct CanvasTarget(Canvas Canvas, DispatcherScope Scope);

        private static CanvasTarget CaptureTarget()
        {
            // Do not take the scope gate under ownershipGate: Dispatcher hooks can select a canvas.
            var scope = DispatcherManager.GetScope();
            lock (ownershipGate)
            {
                if (owner == null) throw new InvalidOperationException("Graphics Canvas is not initialized.");
                if (!ReferenceEquals(owner, scope)) throw new ObjectDisposedException(nameof(DispatcherScope));
                return new CanvasTarget(canvas, scope);
            }
        }

        private static void InvokeOnCanvas(Action<Canvas> action)
        {
            var target = CaptureTarget();
            DispatcherManager.InvokeOnUI(target.Scope, () => action(target.Canvas));
        }

        private static T InvokeOnCanvas<T>(Func<Canvas, T> action)
        {
            var target = CaptureTarget();
            return DispatcherManager.InvokeOnUI(target.Scope, () => action(target.Canvas));
        }

        public static void Clear()
        {
            InvokeOnCanvas(targetCanvas =>
            {
                targetCanvas.Children.Clear();
            });
        }
    }
}
