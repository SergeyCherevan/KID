namespace KID
{
    /// <summary>
    /// Статус клика мышью по Canvas.
    /// </summary>
    public enum ClickStatus
    {
        /// <summary>
        /// Кликов нет.
        /// </summary>
        NoClick = 0,

        /// <summary>
        /// Одинарный клик левой кнопкой.
        /// </summary>
        OneLeftClick,

        /// <summary>
        /// Одинарный клик правой кнопкой.
        /// </summary>
        OneRightClick,

        /// <summary>
        /// Двойной клик левой кнопкой.
        /// </summary>
        DoubleLeftClick,

        /// <summary>
        /// Двойной клик правой кнопкой.
        /// </summary>
        DoubleRightClick,
    }
}

