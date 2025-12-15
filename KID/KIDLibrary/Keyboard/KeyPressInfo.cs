using System.Windows.Input;

namespace KID
{
    /// <summary>
    /// Информация о нажатии клавиши.
    /// </summary>
    public struct KeyPressInfo
    {
        /// <summary>
        /// Нажатая клавиша.
        /// </summary>
        public Key Key { get; set; }
        
        /// <summary>
        /// Статус нажатия.
        /// </summary>
        public KeyPressStatus Status { get; set; }
        
        /// <summary>
        /// Модификаторы, нажатые вместе с клавишей.
        /// </summary>
        public PressedKeysStatus Modifiers { get; set; }
        
        /// <summary>
        /// true, если это автоповтор нажатия.
        /// </summary>
        public bool IsRepeat { get; set; }
        
        /// <summary>
        /// Символ, соответствующий клавише в текущей раскладке клавиатуры.
        /// null, если клавиша не генерирует символ (например, функциональные клавиши, стрелки).
        /// </summary>
        public char? Character { get; set; }
    }
}

