using System.Windows;
using System.Windows.Input;

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
                return InvokeOnUI<Point?>(() =>
                {
                    if (_canvas == null || !_canvas.IsMouseOver)
                        return null;

                    try
                    {
                        var mousePosition = System.Windows.Input.Mouse.GetPosition(_canvas);
                        return mousePosition;
                    }
                    catch
                    {
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
                return InvokeOnUI<Point>(() => _lastActualPosition);
            }
        }

        /// <summary>
        /// Обработчик события перемещения мыши по Canvas.
        /// </summary>
        static partial void OnMouseMove(MouseEventArgs e)
        {
            if (_canvas == null)
                return;

            try
            {
                var position = e.GetPosition(_canvas);
                _lastActualPosition = position;

                // Убираем OutOfArea из состояния (если был установлен)
                _currentPressedButton &= ~PressButtonStatus.OutOfArea;
                
                // Обновляем последнее состояние на Canvas (без OutOfArea)
                _lastActualPressedButton = _currentPressedButton & ~PressButtonStatus.OutOfArea;

                // Вызываем событие перемещения
                OnMouseMove(position);
            }
            catch
            {
                // Игнорируем ошибки
            }
        }

        /// <summary>
        /// Обработчик события выхода мыши за пределы Canvas.
        /// </summary>
        static partial void OnMouseLeave(MouseEventArgs e)
        {
            if (_canvas == null)
                return;

            try
            {
                // Сохраняем последнее состояние на Canvas перед установкой OutOfArea
                _lastActualPressedButton = _currentPressedButton & ~PressButtonStatus.OutOfArea;
                
                // Устанавливаем OutOfArea (сохраняя флаги нажатых кнопок)
                _currentPressedButton |= PressButtonStatus.OutOfArea;
            }
            catch
            {
                // Игнорируем ошибки
            }
        }
    }
}
