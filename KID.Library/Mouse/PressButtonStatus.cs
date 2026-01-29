using System;

namespace KID
{
    /// <summary>
    /// Состояние нажатых кнопок мыши относительно Canvas.
    /// Может содержать комбинации флагов.
    /// </summary>
    [Flags]
    public enum PressButtonStatus
    {
        /// <summary>
        /// Кнопки не нажаты.
        /// </summary>
        NoButton = 0b000,

        /// <summary>
        /// Нажата левая кнопка мыши.
        /// </summary>
        LeftButton = 0b001,

        /// <summary>
        /// Нажата правая кнопка мыши.
        /// </summary>
        RightButton = 0b010,

        /// <summary>
        /// Курсор находится вне Canvas (область недоступна).
        /// </summary>
        OutOfArea = 0b100,
    }
}

