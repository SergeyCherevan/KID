using System.Windows;
using System.Windows.Input;
using KID;

namespace KID
{
    /// <summary>
    /// Частичный класс Mouse - работа с позицией курсора.
    /// </summary>
    public static partial class Mouse
    {
        private static Point _lastActualPosition = new Point(0, 0);

        /// <summary>
        /// Информация о текущем состоянии курсора на Canvas.
        /// Объединяет позицию курсора и состояние нажатых кнопок.
        /// </summary>
        public static CursorInfo CurrentCursor
        {
            get
            {
                return DispatcherManager.InvokeOnUI<CursorInfo>(() =>
                {
                    Point? position = null;
                    PressButtonStatus pressedButton = PressButtonStatus.NoButton;

                    if (Canvas == null || !Canvas.IsMouseOver)
                    {
                        // Курсор вне Canvas
                        pressedButton = _currentPressedButton | PressButtonStatus.OutOfArea;
                    }
                    else
                    {
                        // Курсор на Canvas
                        try
                        {
                            position = System.Windows.Input.Mouse.GetPosition(Canvas);
                            pressedButton = _currentPressedButton & ~PressButtonStatus.OutOfArea;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Mouse position error: {ex.Message}");
                            pressedButton = _currentPressedButton | PressButtonStatus.OutOfArea;
                        }
                    }

                    return new CursorInfo
                    {
                        Position = position,
                        PressedButton = pressedButton
                    };
                });
            }
        }

        /// <summary>
        /// Информация о последнем актуальном состоянии курсора на Canvas.
        /// Объединяет последнюю позицию курсора и последнее состояние нажатых кнопок.
        /// </summary>
        public static CursorInfo LastActualCursor
        {
            get
            {
                return DispatcherManager.InvokeOnUI<CursorInfo>(() =>
                {
                    return new CursorInfo
                    {
                        Position = _lastActualPosition,
                        PressedButton = _lastActualPressedButton
                    };
                });
            }
        }

        /// <summary>
        /// Обработчик события перемещения мыши по Canvas.
        /// </summary>
        static partial void OnMouseMove(MouseEventArgs e)
        {
            if (Canvas == null)
                return;

            try
            {
                var position = e.GetPosition(Canvas);
                _lastActualPosition = position;

                // Убираем OutOfArea из состояния (если был установлен)
                SetOutOfArea(false);

                // Вызываем событие перемещения
                OnMouseMove(position);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mouse move error: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик события выхода мыши за пределы Canvas.
        /// </summary>
        static partial void OnMouseLeave(MouseEventArgs e)
        {
            if (Canvas == null)
                return;

            try
            {
                // Устанавливаем OutOfArea (сохраняя флаги нажатых кнопок)
                SetOutOfArea(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mouse leave error: {ex.Message}");
            }
        }
    }
}
