using System.Windows;

namespace KID
{
    /// <summary>
    /// Частичный класс Mouse - управление состоянием нажатых кнопок.
    /// </summary>
    public static partial class Mouse
    {
        // Для отслеживания состояния нажатых кнопок
        internal static PressButtonStatus _currentPressedButton = PressButtonStatus.NoButton;
        internal static PressButtonStatus _lastActualPressedButton = PressButtonStatus.NoButton;

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
            if (Canvas != null && Canvas.IsMouseOver)
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
