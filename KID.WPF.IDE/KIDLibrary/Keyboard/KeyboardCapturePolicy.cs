namespace KID
{
    /// <summary>
    /// Политика захвата клавиатуры модулем Keyboard (когда считать ввод «активным»).
    /// </summary>
    public enum KeyboardCapturePolicy
    {
        /// <summary>
        /// Всегда захватывать ввод клавиатуры (по всему окну приложения).
        /// </summary>
        CaptureAlways = 0,

        /// <summary>
        /// Игнорировать ввод, если фокус находится в элементе ввода текста (например, TextBox).
        /// </summary>
        IgnoreWhenTextInputFocused = 1,
    }
}

