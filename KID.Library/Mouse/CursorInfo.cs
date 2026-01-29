using System.Windows;

namespace KID
{
    /// <summary>
    /// Снимок состояния курсора мыши относительно Canvas.
    /// </summary>
    public struct CursorInfo
    {
        /// <summary>
        /// Координата курсора относительно левого верхнего угла Canvas.
        /// Если курсор вне Canvas, то значение равно <c>null</c>.
        /// </summary>
        public Point? Position;

        /// <summary>
        /// Состояние нажатых кнопок мыши (включая флаг <see cref="PressButtonStatus.OutOfArea"/>).
        /// </summary>
        public PressButtonStatus PressedButton;

        /// <summary>
        /// Создаёт снимок состояния курсора.
        /// </summary>
        public CursorInfo(Point? position, PressButtonStatus pressedButton)
        {
            Position = position;
            PressedButton = pressedButton;
        }
    }
}

