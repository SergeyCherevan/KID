using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KID;

namespace KID
{
    /// <summary>
    /// Частичный класс Mouse - работа с кликами мыши.
    /// </summary>
    public static partial class Mouse
    {
        private static MouseClickInfo _currentClick = new MouseClickInfo { Status = ClickStatus.NoClick, Position = null };
        private static MouseClickInfo _lastClick = new MouseClickInfo { Status = ClickStatus.NoClick, Position = null };

        // Для отслеживания двойных кликов
        private static DoubleClickTracker _leftClickTracker = new DoubleClickTracker
        {
            SingleClickStatus = ClickStatus.OneLeftClick,
            DoubleClickStatus = ClickStatus.DoubleLeftClick
        };

        private static DoubleClickTracker _rightClickTracker = new DoubleClickTracker
        {
            SingleClickStatus = ClickStatus.OneRightClick,
            DoubleClickStatus = ClickStatus.DoubleRightClick
        };

        /// <summary>
        /// Структура с информацией о текущем клике по Canvas.
        /// </summary>
        public static MouseClickInfo CurrentClick
        {
            get
            {
                return DispatcherManager.InvokeOnUI(() => _currentClick);
            }
        }

        /// <summary>
        /// Структура с информацией о последнем зарегистрированном клике.
        /// </summary>
        public static MouseClickInfo LastClick
        {
            get
            {
                return DispatcherManager.InvokeOnUI(() => _lastClick);
            }
        }

        /// <summary>
        /// Обработчик события нажатия левой кнопки мыши.
        /// </summary>
        static partial void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            HandleButtonDown(e, PressButtonStatus.LeftButton, ref _leftClickTracker);
        }

        /// <summary>
        /// Обработчик события нажатия правой кнопки мыши.
        /// </summary>
        static partial void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            HandleButtonDown(e, PressButtonStatus.RightButton, ref _rightClickTracker);
        }

        /// <summary>
        /// Обработчик события отпускания левой кнопки мыши.
        /// </summary>
        static partial void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            HandleButtonUp(e, PressButtonStatus.LeftButton);
        }

        /// <summary>
        /// Обработчик события отпускания правой кнопки мыши.
        /// </summary>
        static partial void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            HandleButtonUp(e, PressButtonStatus.RightButton);
        }

        /// <summary>
        /// Унифицированная обработка нажатия кнопки мыши.
        /// </summary>
        private static void HandleButtonDown(MouseButtonEventArgs e, PressButtonStatus buttonFlag, ref DoubleClickTracker tracker)
        {
            if (Canvas == null)
                return;

            try
            {
                var position = e.GetPosition(Canvas);
                var now = DateTime.Now;

                // Обновление состояния кнопки
                UpdateButtonState(buttonFlag, true);

                // Проверка двойного клика
                if (ProcessDoubleClick(position, now, ref tracker))
                {
                    return; // Двойной клик обработан
                }

                // Сохраняем статус в локальную переменную для использования в лямбде
                var singleClickStatus = tracker.SingleClickStatus;
                
                // Запуск таймера для одиночного клика
                tracker.StartTimer(position, (pos) => RegisterClick(singleClickStatus, pos));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mouse button down error: {ex.Message}");
            }
        }

        /// <summary>
        /// Унифицированная обработка отпускания кнопки мыши.
        /// </summary>
        private static void HandleButtonUp(MouseButtonEventArgs e, PressButtonStatus buttonFlag)
        {
            if (Canvas == null)
                return;

            try
            {
                // Убираем кнопку из состояния нажатых кнопок
                UpdateButtonState(buttonFlag, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mouse button up error: {ex.Message}");
            }
        }

        /// <summary>
        /// Обрабатывает потенциальный двойной клик.
        /// </summary>
        /// <param name="position">Позиция текущего клика.</param>
        /// <param name="now">Текущее время.</param>
        /// <param name="tracker">Трекер для отслеживания двойных кликов.</param>
        /// <returns>true если это был двойной клик, false иначе.</returns>
        private static bool ProcessDoubleClick(Point position, DateTime now, ref DoubleClickTracker tracker)
        {
            if (tracker.LastClickTime.HasValue &&
                tracker.LastClickPosition.HasValue &&
                (now - tracker.LastClickTime.Value).TotalMilliseconds < DoubleClickDelayMs &&
                IsSamePosition(tracker.LastClickPosition.Value, position))
            {
                // Это двойной клик
                tracker.StopTimer();
                RegisterClick(tracker.DoubleClickStatus, position);
                tracker.Reset();
                return true;
            }

            // Сохраняем информацию о клике для возможного двойного клика
            tracker.LastClickTime = now;
            tracker.LastClickPosition = position;
            return false;
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
        /// Проверяет, находятся ли две позиции в одном месте (с учетом небольшой погрешности).
        /// </summary>
        private static bool IsSamePosition(Point p1, Point p2)
        {
            return Math.Abs(p1.X - p2.X) < DoubleClickPositionTolerance &&
                   Math.Abs(p1.Y - p2.Y) < DoubleClickPositionTolerance;
        }

        /// <summary>
        /// Структура для отслеживания двойных кликов.
        /// </summary>
        private struct DoubleClickTracker
        {
            public DateTime? LastClickTime;
            public Point? LastClickPosition;
            public DispatcherTimer? Timer;
            public ClickStatus SingleClickStatus;
            public ClickStatus DoubleClickStatus;

            /// <summary>
            /// Сбрасывает состояние трекера.
            /// </summary>
            public void Reset()
            {
                LastClickTime = null;
                LastClickPosition = null;
                StopTimer();
            }

            /// <summary>
            /// Останавливает таймер.
            /// </summary>
            public void StopTimer()
            {
                if (Timer != null)
                {
                    Timer.Stop();
                    Timer = null;
                }
            }

            /// <summary>
            /// Запускает таймер для определения одиночного клика.
            /// </summary>
            public void StartTimer(Point position, Action<Point> onSingleClick)
            {
                StopTimer();

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(DoubleClickDelayMs)
                };

                // Сохраняем ссылку на таймер и позицию в локальные переменные для лямбды
                var savedPosition = position;
                
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer = null;
                    // Если таймер сработал, значит это был одиночный клик
                    onSingleClick(savedPosition);
                };

                Timer = timer;
                timer.Start();
            }
        }
    }
}
