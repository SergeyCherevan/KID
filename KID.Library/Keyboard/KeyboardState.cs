using System;
using WpfKey = global::System.Windows.Input.Key;

namespace KID
{
    /// <summary>
    /// Снимок состояния клавиатуры (для polling).
    /// </summary>
    public struct KeyboardState
    {
        /// <summary>
        /// Текущие модификаторы (Shift/Ctrl/Alt/Win).
        /// </summary>
        public KeyModifiers Modifiers;

        /// <summary>
        /// Включён CapsLock.
        /// </summary>
        public bool CapsLockOn;

        /// <summary>
        /// Включён NumLock.
        /// </summary>
        public bool NumLockOn;

        /// <summary>
        /// Включён ScrollLock.
        /// </summary>
        public bool ScrollLockOn;

        /// <summary>
        /// Последняя нажатая клавиша (KeyDown). <see cref="WpfKey.None"/> если ещё не было.
        /// </summary>
        public WpfKey LastKeyDown;

        /// <summary>
        /// Последняя отпущенная клавиша (KeyUp). <see cref="WpfKey.None"/> если ещё не было.
        /// </summary>
        public WpfKey LastKeyUp;

        /// <summary>
        /// Массив всех текущих зажатых клавиш (может быть пустым).
        /// </summary>
        public WpfKey[] DownKeys;

        /// <summary>
        /// Создаёт снимок состояния.
        /// </summary>
        public KeyboardState(
            KeyModifiers modifiers,
            bool capsLockOn,
            bool numLockOn,
            bool scrollLockOn,
            WpfKey lastKeyDown,
            WpfKey lastKeyUp,
            WpfKey[] downKeys)
        {
            Modifiers = modifiers;
            CapsLockOn = capsLockOn;
            NumLockOn = numLockOn;
            ScrollLockOn = scrollLockOn;
            LastKeyDown = lastKeyDown;
            LastKeyUp = lastKeyUp;
            DownKeys = downKeys ?? Array.Empty<WpfKey>();
        }
    }
}

