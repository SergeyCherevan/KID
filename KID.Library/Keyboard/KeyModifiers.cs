using System;

namespace KID
{
    /// <summary>
    /// Модификаторы клавиатуры (можно комбинировать).
    /// </summary>
    [Flags]
    public enum KeyModifiers
    {
        /// <summary>
        /// Нет модификаторов.
        /// </summary>
        None = 0,

        /// <summary>
        /// Shift.
        /// </summary>
        Shift = 1 << 0,

        /// <summary>
        /// Ctrl.
        /// </summary>
        Ctrl = 1 << 1,

        /// <summary>
        /// Alt.
        /// </summary>
        Alt = 1 << 2,

        /// <summary>
        /// Win (клавиша Windows).
        /// </summary>
        Win = 1 << 3,
    }
}

