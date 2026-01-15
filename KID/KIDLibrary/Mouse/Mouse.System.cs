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

        private static Canvas? _canvas;

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
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            lock (_initLock)
            {
                if (_canvas != null)
                    Unsubscribe(_canvas);

                _canvas = canvas;

                ResetState();
                Subscribe(canvas);

                StartEventWorker();
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

        private static void UpdateCursor(Point? position, bool isActualOnCanvas)
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
                var moveHandler = MouseMoveEvent;
                if (moveHandler != null)
                {
                    EnqueueEvent(() =>
                    {
                        try { moveHandler(cursorSnapshot); } catch { }
                    });
                }
            }

            if (pressChangedSnapshot.HasValue)
            {
                var pressHandler = MousePressButtonEvent;
                var pressSnapshot = pressChangedSnapshot.Value;
                if (pressHandler != null)
                {
                    EnqueueEvent(() =>
                    {
                        try { pressHandler(pressSnapshot); } catch { }
                    });
                }
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

        private static void RegisterClick(ClickStatus status, Point position)
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

            var clickHandler = MouseClickEvent;
            if (clickHandler != null)
            {
                EnqueueEvent(() =>
                {
                    try { clickHandler(clickSnapshot); } catch { }
                });
            }

            // Сброс CurrentClick через короткое окно, чтобы его можно было «поймать» polling'ом.
            _ = Task.Run(async () =>
            {
                await Task.Delay(CurrentClickPulseMs).ConfigureAwait(false);

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
            if (_canvas == null)
                return;

            _isOutOfArea = false;
            UpdateCursor(e.GetPosition(_canvas), isActualOnCanvas: true);
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isOutOfArea = true;
            UpdateCursor(position: null, isActualOnCanvas: false);
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_canvas == null)
                return;

            if (_isOutOfArea)
            {
                UpdateCursor(position: null, isActualOnCanvas: false);
                return;
            }

            UpdateCursor(e.GetPosition(_canvas), isActualOnCanvas: true);
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_canvas == null)
                return;

            _isOutOfArea = false;

            if (e.ChangedButton == MouseButton.Left)
                _isLeftPressed = true;
            else if (e.ChangedButton == MouseButton.Right)
                _isRightPressed = true;

            var pos = e.GetPosition(_canvas);
            UpdateCursor(pos, isActualOnCanvas: true);

            var clickStatus = ToClickStatus(e.ChangedButton, e.ClickCount);
            if (clickStatus != ClickStatus.NoClick)
                RegisterClick(clickStatus, pos);
        }

        private static void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_canvas == null)
                return;

            _isOutOfArea = false;

            if (e.ChangedButton == MouseButton.Left)
                _isLeftPressed = false;
            else if (e.ChangedButton == MouseButton.Right)
                _isRightPressed = false;

            UpdateCursor(e.GetPosition(_canvas), isActualOnCanvas: true);
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

