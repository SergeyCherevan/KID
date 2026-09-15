using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KID
{
    public static partial class Mouse
    {
        private static readonly object _initLock = new object();

        private static MouseExecutionScope? _executionScope;

        private static bool _isLeftPressed;
        private static bool _isRightPressed;
        private static bool _isOutOfArea = true;

        private static int _clickPulseVersion;
        private const int CurrentClickPulseMs = 80; // 50–100ms, чтобы удобно ловилось polling'ом

        /// <summary>
        /// Инициализация Mouse API. Вызывается из контекста выполнения при наличии Canvas.
        /// </summary>
        public static void Init(Canvas canvas)
        {
            var environment = ExecutionEnvironmentManager.Current ??
                throw new InvalidOperationException("No execution is active.");
            _ = Init(canvas, environment);
        }

        /// <summary>Подключает Mouse к явно захваченному environment текущего запуска.</summary>
        internal static MouseExecutionScope Init(Canvas canvas, ExecutionEnvironment environment)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));

            canvas.VerifyAccess();

            lock (_initLock)
            {
                if (_executionScope != null)
                    throw new InvalidOperationException("Mouse is already initialized.");
                if (!ExecutionEnvironmentManager.IsCurrentAndAccepting(environment))
                    throw new InvalidOperationException("Execution does not own the current environment.");
                environment.ThrowIfCancellationRequested();

                ResetState();
                ClearUserEvents();
                var scope = new MouseExecutionScope(environment, canvas);
                Volatile.Write(ref _executionScope, scope);
                Subscribe(canvas);
                environment.ThrowIfCancellationRequested();
                return scope;
            }
        }

        /// <summary>
        /// Закрывает Mouse только для указанного environment. Повторный или stale shutdown
        /// не может снять ownership более нового запуска.
        /// </summary>
        internal static ValueTask ShutdownAsync(ExecutionEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(environment);
            var scope = Volatile.Read(ref _executionScope);
            if (scope == null || !ReferenceEquals(scope.Environment, environment))
                return ValueTask.CompletedTask;

            return scope.EventWorker.ShutdownAsync(
                () => UnsubscribeAsync(scope),
                () => Release(scope));
        }

        internal static MouseExecutionScope? CurrentScope => Volatile.Read(ref _executionScope);

        private static MouseExecutionScope? GetActiveScope()
        {
            var scope = Volatile.Read(ref _executionScope);
            return scope != null && IsCurrent(scope) && scope.EventWorker.IsAccepting ? scope : null;
        }

        private static bool IsCurrent(MouseExecutionScope scope) =>
            ReferenceEquals(Volatile.Read(ref _executionScope), scope) &&
            ExecutionEnvironmentManager.IsCurrentAndAccepting(scope.Environment);

        private static Task UnsubscribeAsync(MouseExecutionScope scope)
        {
            void RemoveHandlers() => Unsubscribe(scope.Canvas);
            if (scope.Canvas.Dispatcher.CheckAccess())
            {
                RemoveHandlers();
                return Task.CompletedTask;
            }

            return scope.Canvas.Dispatcher.InvokeAsync(RemoveHandlers).Task;
        }

        private static void Release(MouseExecutionScope scope)
        {
            lock (_initLock)
            {
                if (!ReferenceEquals(_executionScope, scope)) return;
                ResetState();
                ClearUserEvents();
                Volatile.Write(ref _executionScope, null);
            }
        }

        private static void ResetState()
        {
            lock (_stateLock)
            {
                _isLeftPressed = false;
                _isRightPressed = false;
                _isOutOfArea = true;

                _currentCursor = new CursorInfo(null, BuildPressedStatus());
                _lastActualCursor = new CursorInfo(null, PressButtonStatus.NoButton);

                _currentClick = new MouseClickInfo(ClickStatus.NoClick, null);
                _lastClick = new MouseClickInfo(ClickStatus.NoClick, null);
                _clickPulseVersion = unchecked(_clickPulseVersion + 1);
            }
        }

        private static void Subscribe(Canvas canvas)
        {
            canvas.MouseEnter += OnMouseEnter;
            canvas.MouseLeave += OnMouseLeave;
            canvas.MouseMove += OnMouseMove;
            canvas.MouseDown += OnMouseDown;
            canvas.MouseUp += OnMouseUp;
        }

        private static void Unsubscribe(Canvas canvas)
        {
            canvas.MouseEnter -= OnMouseEnter;
            canvas.MouseLeave -= OnMouseLeave;
            canvas.MouseMove -= OnMouseMove;
            canvas.MouseDown -= OnMouseDown;
            canvas.MouseUp -= OnMouseUp;
        }

        private static PressButtonStatus BuildPressedStatus()
        {
            PressButtonStatus status = PressButtonStatus.NoButton;

            if (_isLeftPressed)
                status |= PressButtonStatus.LeftButton;
            if (_isRightPressed)
                status |= PressButtonStatus.RightButton;
            if (_isOutOfArea)
                status |= PressButtonStatus.OutOfArea;

            return status;
        }

        private static void UpdateCursor(
            MouseExecutionScope scope,
            Point? position,
            bool isActualOnCanvas)
        {
            CursorInfo cursorSnapshot;
            CursorInfo? pressChangedSnapshot = null;
            bool positionChanged;

            lock (_stateLock)
            {
                var newPressed = BuildPressedStatus();
                var oldPressed = _currentCursor.PressedButton;
                var oldPosition = _currentCursor.Position;

                _currentCursor = new CursorInfo(position, newPressed);

                if (isActualOnCanvas)
                {
                    // LastActualCursor хранит последнее состояние НА Canvas (без OutOfArea).
                    var lastPressed = newPressed & ~PressButtonStatus.OutOfArea;
                    _lastActualCursor = new CursorInfo(position, lastPressed);
                }

                cursorSnapshot = _currentCursor;
                positionChanged = !NullablePointEquals(oldPosition, position);

                if (newPressed != oldPressed)
                    pressChangedSnapshot = cursorSnapshot;
            }

            if (positionChanged)
            {
                EnqueueHandlers(scope, MouseMoveEvent, cursorSnapshot);
            }

            if (pressChangedSnapshot.HasValue)
            {
                var pressSnapshot = pressChangedSnapshot.Value;
                EnqueueHandlers(scope, MousePressButtonEvent, pressSnapshot);
            }
        }

        private static bool NullablePointEquals(Point? a, Point? b)
        {
            if (!a.HasValue && !b.HasValue)
                return true;
            if (a.HasValue != b.HasValue)
                return false;

            return a!.Value == b!.Value;
        }

        private static void RegisterClick(
            MouseExecutionScope scope,
            ClickStatus status,
            Point position)
        {
            MouseClickInfo clickSnapshot;
            int pulseVersion;

            lock (_stateLock)
            {
                clickSnapshot = new MouseClickInfo(status, position);
                _lastClick = clickSnapshot;
                _currentClick = clickSnapshot;

                pulseVersion = ++_clickPulseVersion;
            }

            EnqueueHandlers(scope, MouseClickEvent, clickSnapshot);

            // Сброс CurrentClick через короткое окно, чтобы его можно было «поймать» polling'ом.
            _ = scope.EventWorker.TrySchedule(TimeSpan.FromMilliseconds(CurrentClickPulseMs), () =>
            {
                if (!IsCurrent(scope)) return;
                lock (_stateLock)
                {
                    if (_clickPulseVersion != pulseVersion)
                        return;

                    _currentClick = new MouseClickInfo(ClickStatus.NoClick, null);
                }
            });
        }

        private static void OnMouseEnter(object sender, MouseEventArgs e)
        {
            var scope = GetActiveScope();
            if (scope == null)
                return;

            _isOutOfArea = false;
            UpdateCursor(scope, e.GetPosition(scope.Canvas), isActualOnCanvas: true);
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            var scope = GetActiveScope();
            if (scope == null)
                return;
            _isOutOfArea = true;
            UpdateCursor(scope, position: null, isActualOnCanvas: false);
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            var scope = GetActiveScope();
            if (scope == null)
                return;

            if (_isOutOfArea)
            {
                UpdateCursor(scope, position: null, isActualOnCanvas: false);
                return;
            }

            UpdateCursor(scope, e.GetPosition(scope.Canvas), isActualOnCanvas: true);
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var scope = GetActiveScope();
            if (scope == null)
                return;

            _isOutOfArea = false;

            if (e.ChangedButton == MouseButton.Left)
                _isLeftPressed = true;
            else if (e.ChangedButton == MouseButton.Right)
                _isRightPressed = true;

            var pos = e.GetPosition(scope.Canvas);
            UpdateCursor(scope, pos, isActualOnCanvas: true);

            var clickStatus = ToClickStatus(e.ChangedButton, e.ClickCount);
            if (clickStatus != ClickStatus.NoClick)
                RegisterClick(scope, clickStatus, pos);
        }

        private static void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            var scope = GetActiveScope();
            if (scope == null)
                return;

            _isOutOfArea = false;

            if (e.ChangedButton == MouseButton.Left)
                _isLeftPressed = false;
            else if (e.ChangedButton == MouseButton.Right)
                _isRightPressed = false;

            UpdateCursor(scope, e.GetPosition(scope.Canvas), isActualOnCanvas: true);
        }

        private static ClickStatus ToClickStatus(MouseButton button, int clickCount)
        {
            if (button == MouseButton.Left)
                return clickCount >= 2 ? ClickStatus.DoubleLeftClick : ClickStatus.OneLeftClick;
            if (button == MouseButton.Right)
                return clickCount >= 2 ? ClickStatus.DoubleRightClick : ClickStatus.OneRightClick;

            return ClickStatus.NoClick;
        }
    }
}

