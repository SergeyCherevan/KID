using System;
using WpfKey = global::System.Windows.Input.Key;

namespace KID
{
    /// <summary>
    /// Информация о событии клавиши (нажатие/отпускание/повтор).
    /// </summary>
    public struct KeyPressInfo
    {
        /// <summary>
        /// Клавиша.
        /// </summary>
        public WpfKey Key;

        /// <summary>
        /// Модификаторы (Shift/Ctrl/Alt/Win) в момент события.
        /// </summary>
        public KeyModifiers Modifiers;

        /// <summary>
        /// True, если это повтор удерживаемой клавиши (auto-repeat).
        /// </summary>
        public bool IsRepeat;

        /// <summary>
        /// Время события (локальное).
        /// </summary>
        public DateTimeOffset Timestamp;

        /// <summary>
        /// Создаёт информацию о событии клавиши.
        /// </summary>
        public KeyPressInfo(WpfKey key, KeyModifiers modifiers, bool isRepeat, DateTimeOffset timestamp)
        {
            Key = key;
            Modifiers = modifiers;
            IsRepeat = isRepeat;
            Timestamp = timestamp;
        }
    }
}

