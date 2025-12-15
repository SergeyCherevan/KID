namespace KID
{
    /// <summary>
    /// Статус нажатия клавиши.
    /// </summary>
    public enum KeyPressStatus
    {
        /// <summary>
        /// Нет нажатия.
        /// </summary>
        NoPress,
        
        /// <summary>
        /// Одиночное нажатие клавиши.
        /// </summary>
        SinglePress,
        
        /// <summary>
        /// Автоповтор нажатия клавиши (клавиша удерживается).
        /// </summary>
        RepeatPress
    }
}

