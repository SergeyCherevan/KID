using System;

namespace KID
{
    /// <summary>
    /// Статус нажатых модификаторов клавиатуры (флаги).
    /// </summary>
    [Flags]
    public enum PressedKeysStatus
    {
        /// <summary>
        /// Нет нажатых модификаторов.
        /// </summary>
        NoModifier = 0b0000,
        
        /// <summary>
        /// Нажата клавиша Ctrl (любая из сторон).
        /// </summary>
        Ctrl = 0b0001,
        
        /// <summary>
        /// Нажата клавиша Alt (любая из сторон).
        /// </summary>
        Alt = 0b0010,
        
        /// <summary>
        /// Нажата клавиша Shift (любая из сторон).
        /// </summary>
        Shift = 0b0100,
        
        /// <summary>
        /// Нажата клавиша Windows.
        /// </summary>
        Windows = 0b1000
    }
}

