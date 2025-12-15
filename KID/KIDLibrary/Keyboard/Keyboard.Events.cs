using System;

namespace KID
{
    /// <summary>
    /// Частичный класс Keyboard - события клавиатуры.
    /// </summary>
    public static partial class Keyboard
    {
        /// <summary>
        /// Событие нажатия клавиши.
        /// </summary>
        public static event EventHandler<KeyPressInfo>? KeyPressEvent;

        /// <summary>
        /// Событие отпускания клавиши.
        /// </summary>
        public static event EventHandler<KeyPressInfo>? KeyReleaseEvent;

        /// <summary>
        /// Вызывает событие нажатия клавиши.
        /// </summary>
        internal static void OnKeyPress(KeyPressInfo pressInfo)
        {
            KeyPressEvent?.Invoke(null, pressInfo);
        }

        /// <summary>
        /// Вызывает событие отпускания клавиши.
        /// </summary>
        internal static void OnKeyRelease(KeyPressInfo pressInfo)
        {
            KeyReleaseEvent?.Invoke(null, pressInfo);
        }
    }
}

