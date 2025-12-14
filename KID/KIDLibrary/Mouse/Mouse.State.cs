using System.Windows;

namespace KID
{
    /// <summary>
    /// Частичный класс Mouse - управление состоянием нажатых кнопок.
    /// </summary>
    public static partial class Mouse
    {
        // Для отслеживания состояния нажатых кнопок
        private static PressButtonStatus _currentPressedButton = PressButtonStatus.NoButton;
        private static PressButtonStatus _lastActualPressedButton = PressButtonStatus.NoButton;

        /// <summary>
        /// Код с информацией о текущей нажатой кнопке мыши.
        /// Может включать комбинации флагов LeftButton, RightButton и OutOfArea.
        /// </summary>
        public static PressButtonStatus CurrentPressedButton
        {
            get
            {
                return InvokeOnUI<PressButtonStatus>(() =>
                {
                    // Если курсор не на Canvas, добавляем OutOfArea (сохраняя флаги кнопок)
                    if (_canvas == null || !_canvas.IsMouseOver)
                    {
                        return _currentPressedButton | PressButtonStatus.OutOfArea;
                    }
                    // Если курсор на Canvas, возвращаем только флаги кнопок (без OutOfArea)
                    return _currentPressedButton & ~PressButtonStatus.OutOfArea;
                });
            }
        }

        /// <summary>
        /// Код с информацией о последней нажатой кнопке мыши на Canvas.
        /// Никогда не содержит флаг OutOfArea.
        /// </summary>
        public static PressButtonStatus LastActualPressedButton
        {
            get
            {
                return InvokeOnUI<PressButtonStatus>(() => _lastActualPressedButton);
            }
        }

        /// <summary>
        /// Обновляет состояние нажатой кнопки.
        /// </summary>
        /// <param name="buttonFlag">Флаг кнопки для обновления (LeftButton или RightButton).</param>
        /// <param name="isPressed">true если кнопка нажата, false если отпущена.</param>
        internal static void UpdateButtonState(PressButtonStatus buttonFlag, bool isPressed)
        {
            if (isPressed)
            {
                _currentPressedButton |= buttonFlag;
            }
            else
            {
                _currentPressedButton &= ~buttonFlag;
            }

            // Обновляем последнее состояние на Canvas, если курсор на Canvas
            if (_canvas != null && _canvas.IsMouseOver)
            {
                UpdateLastActualPressedButton();
            }
        }

        /// <summary>
        /// Обновляет последнее актуальное состояние нажатых кнопок на Canvas.
        /// </summary>
        internal static void UpdateLastActualPressedButton()
        {
            _lastActualPressedButton = _currentPressedButton & ~PressButtonStatus.OutOfArea;
        }

        /// <summary>
        /// Устанавливает или снимает флаг OutOfArea.
        /// </summary>
        /// <param name="isOutOfArea">true если курсор вне Canvas, false если на Canvas.</param>
        internal static void SetOutOfArea(bool isOutOfArea)
        {
            if (isOutOfArea)
            {
                // Сохраняем последнее состояние на Canvas перед установкой OutOfArea
                UpdateLastActualPressedButton();
                // Устанавливаем OutOfArea (сохраняя флаги нажатых кнопок)
                _currentPressedButton |= PressButtonStatus.OutOfArea;
            }
            else
            {
                // Убираем OutOfArea из состояния
                _currentPressedButton &= ~PressButtonStatus.OutOfArea;
                // Обновляем последнее состояние на Canvas
                UpdateLastActualPressedButton();
            }
        }
    }
}
