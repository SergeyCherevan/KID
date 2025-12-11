using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace KID
{
    /// <summary>
    /// Частичный класс Mouse - работа с кликами мыши.
    /// </summary>
    public static partial class Mouse
    {
        private static MouseClickInfo _currentClick = new MouseClickInfo { Status = ClickStatus.NoClick, Position = null };
        private static MouseClickInfo _lastClick = new MouseClickInfo { Status = ClickStatus.NoClick, Position = null };

        // Для отслеживания двойных кликов правой кнопкой
        private static DateTime? _lastRightClickTime = null;
        private static Point? _lastRightClickPosition = null;
        private static DispatcherTimer? _rightClickTimer = null;
        private const int DoubleClickDelayMs = 500; // Интервал для определения двойного клика

        // Для отслеживания двойных кликов левой кнопкой (если MouseDoubleClick не сработает)
        private static DateTime? _lastLeftClickTime = null;
        private static Point? _lastLeftClickPosition = null;
        private static DispatcherTimer? _leftClickTimer = null;

        /// <summary>
        /// Структура с информацией о текущем клике по Canvas.
        /// </summary>
        public static MouseClickInfo CurrentClick
        {
            get
            {
                return InvokeOnUI(() => _currentClick);
            }
        }

        /// <summary>
        /// Структура с информацией о последнем зарегистрированном клике.
        /// </summary>
        public static MouseClickInfo LastClick
        {
            get
            {
                return InvokeOnUI(() => _lastClick);
            }
        }

        /// <summary>
        /// Обработчик события нажатия левой кнопки мыши.
        /// </summary>
        static partial void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (_canvas == null)
                return;

            try
            {
                var position = e.GetPosition(_canvas);
                var now = DateTime.Now;

                // Проверяем, не был ли это двойной клик
                if (_lastLeftClickTime.HasValue && 
                    _lastLeftClickPosition.HasValue &&
                    (now - _lastLeftClickTime.Value).TotalMilliseconds < DoubleClickDelayMs &&
                    IsSamePosition(_lastLeftClickPosition.Value, position))
                {
                    // Это двойной клик
                    StopLeftClickTimer();
                    RegisterClick(ClickStatus.DoubleLeftClick, position);
                }
                else
                {
                    // Возможно, это начало двойного клика
                    _lastLeftClickTime = now;
                    _lastLeftClickPosition = position;
                    StartLeftClickTimer(position);
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        /// <summary>
        /// Обработчик события нажатия правой кнопки мыши.
        /// </summary>
        static partial void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            if (_canvas == null)
                return;

            try
            {
                var position = e.GetPosition(_canvas);
                var now = DateTime.Now;

                // Проверяем, не был ли это двойной клик
                if (_lastRightClickTime.HasValue && 
                    _lastRightClickPosition.HasValue &&
                    (now - _lastRightClickTime.Value).TotalMilliseconds < DoubleClickDelayMs &&
                    IsSamePosition(_lastRightClickPosition.Value, position))
                {
                    // Это двойной клик
                    StopRightClickTimer();
                    RegisterClick(ClickStatus.DoubleRightClick, position);
                }
                else
                {
                    // Возможно, это начало двойного клика
                    _lastRightClickTime = now;
                    _lastRightClickPosition = position;
                    StartRightClickTimer(position);
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        /// <summary>
        /// Обработчик события отпускания левой кнопки мыши.
        /// </summary>
        static partial void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            // Обработка в таймере
        }

        /// <summary>
        /// Обработчик события отпускания правой кнопки мыши.
        /// </summary>
        static partial void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            // Обработка в таймере
        }


        /// <summary>
        /// Регистрирует клик.
        /// </summary>
        private static void RegisterClick(ClickStatus status, Point position)
        {
            var clickInfo = new MouseClickInfo
            {
                Status = status,
                Position = position
            };

            _lastClick = _currentClick;
            _currentClick = clickInfo;

            // Вызываем событие клика
            OnMouseClick(clickInfo);
        }

        /// <summary>
        /// Запускает таймер для определения одиночного клика левой кнопкой.
        /// </summary>
        private static void StartLeftClickTimer(Point position)
        {
            StopLeftClickTimer();

            _leftClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DoubleClickDelayMs)
            };

            _leftClickTimer.Tick += (s, e) =>
            {
                StopLeftClickTimer();
                // Если таймер сработал, значит это был одиночный клик
                RegisterClick(ClickStatus.OneLeftClick, position);
            };

            _leftClickTimer.Start();
        }

        /// <summary>
        /// Останавливает таймер для левой кнопки.
        /// </summary>
        private static void StopLeftClickTimer()
        {
            if (_leftClickTimer != null)
            {
                _leftClickTimer.Stop();
                _leftClickTimer = null;
            }
        }

        /// <summary>
        /// Запускает таймер для определения одиночного клика правой кнопкой.
        /// </summary>
        private static void StartRightClickTimer(Point position)
        {
            StopRightClickTimer();

            _rightClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DoubleClickDelayMs)
            };

            _rightClickTimer.Tick += (s, e) =>
            {
                StopRightClickTimer();
                // Если таймер сработал, значит это был одиночный клик
                RegisterClick(ClickStatus.OneRightClick, position);
            };

            _rightClickTimer.Start();
        }

        /// <summary>
        /// Останавливает таймер для правой кнопки.
        /// </summary>
        private static void StopRightClickTimer()
        {
            if (_rightClickTimer != null)
            {
                _rightClickTimer.Stop();
                _rightClickTimer = null;
            }
        }

        /// <summary>
        /// Проверяет, находятся ли две позиции в одном месте (с учетом небольшой погрешности).
        /// </summary>
        private static bool IsSamePosition(Point p1, Point p2, double tolerance = 5.0)
        {
            return Math.Abs(p1.X - p2.X) < tolerance && Math.Abs(p1.Y - p2.Y) < tolerance;
        }
    }
}
