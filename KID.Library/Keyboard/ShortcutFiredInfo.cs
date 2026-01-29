using System;

namespace KID
{
    /// <summary>
    /// Информация о сработавшем хоткее.
    /// </summary>
    public struct ShortcutFiredInfo
    {
        /// <summary>
        /// Идентификатор регистрации хоткея.
        /// </summary>
        public int Id;

        /// <summary>
        /// Хоткей, который сработал.
        /// </summary>
        public Shortcut Shortcut;

        /// <summary>
        /// Какая клавиша стала триггером (обычно KeyDown).
        /// </summary>
        public KeyPressInfo Trigger;

        /// <summary>
        /// Время срабатывания (локальное).
        /// </summary>
        public DateTimeOffset Timestamp;

        public ShortcutFiredInfo(int id, Shortcut shortcut, KeyPressInfo trigger, DateTimeOffset timestamp)
        {
            Id = id;
            Shortcut = shortcut;
            Trigger = trigger;
            Timestamp = timestamp;
        }
    }
}

