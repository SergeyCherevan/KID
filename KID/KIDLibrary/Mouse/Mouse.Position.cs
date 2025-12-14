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
        /// Текущая координата курсора относительно верхнего левого угла Canvas.
        /// Если курсор сейчас не на Canvas, значение равно null.
        /// </summary>
        public static Point? CurrentPosition
        {
            get
            {
                return DispatcherManager.InvokeOnUI<Point?>(() =>
                {
                    if (Canvas == null || !Canvas.IsMouseOver)
                        return null;

                    try
                    {
                        var mousePosition = System.Windows.Input.Mouse.GetPosition(Canvas);
                        return mousePosition;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Mouse position error: {ex.Message}");
                        return null;
                    }
                });
            }
        }

        /// <summary>
        /// Последняя актуальная позиция курсора относительно верхнего левого угла Canvas.
        /// Если курсор сейчас на Canvas, значение равно CurrentPosition.
        /// </summary>
        public static Point LastActualPosition
        {
            get
            {
                return DispatcherManager.InvokeOnUI<Point>(() => _lastActualPosition);
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
