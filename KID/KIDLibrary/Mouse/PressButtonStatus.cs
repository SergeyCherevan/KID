using System;

namespace KID
{
    /// <summary>
    /// Статус нажатых кнопок мыши (флаги).
    /// </summary>
    [Flags]
    public enum PressButtonStatus
    {
        /// <summary>
        /// Нет нажатых кнопок.
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
        /// Курсор находится вне Canvas.
        /// </summary>
        OutOfArea = 0b100
    }
}
