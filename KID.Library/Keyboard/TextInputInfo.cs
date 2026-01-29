using System;

namespace KID
{
    /// <summary>
    /// Информация о текстовом вводе (символы/строка), учитывающая раскладку и Unicode.
    /// </summary>
    public struct TextInputInfo
    {
        /// <summary>
        /// Текст, который ввёл пользователь (может содержать несколько символов).
        /// </summary>
        public string Text;

        /// <summary>
        /// Модификаторы (Shift/Ctrl/Alt/Win) в момент ввода.
        /// </summary>
        public KeyModifiers Modifiers;

        /// <summary>
        /// Время события (локальное).
        /// </summary>
        public DateTimeOffset Timestamp;

        /// <summary>
        /// Создаёт информацию о текстовом вводе.
        /// </summary>
        public TextInputInfo(string text, KeyModifiers modifiers, DateTimeOffset timestamp)
        {
            Text = text ?? string.Empty;
            Modifiers = modifiers;
            Timestamp = timestamp;
        }
    }
}

