using System;

namespace KID
{
    /// <summary>
    /// Статический частичный класс для получения информации о мыши относительно Canvas.
    /// </summary>
    public static partial class Mouse
    {
        private static readonly object _stateLock = new object();

        private static CursorInfo _currentCursor = new CursorInfo(null, PressButtonStatus.OutOfArea);
        private static CursorInfo _lastActualCursor = new CursorInfo(null, PressButtonStatus.NoButton);

        private static MouseClickInfo _currentClick = new MouseClickInfo(ClickStatus.NoClick, null);
        private static MouseClickInfo _lastClick = new MouseClickInfo(ClickStatus.NoClick, null);

        /// <summary>
        /// Текущее состояние курсора.
        /// </summary>
        public static CursorInfo CurrentCursor
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentCursor;
                }
            }
        }

        /// <summary>
        /// Последнее актуальное состояние курсора, когда он находился на Canvas.
        /// </summary>
        public static CursorInfo LastActualCursor
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastActualCursor;
                }
            }
        }

        /// <summary>
        /// Текущий клик (короткий «пульс»). После короткого интервала сбрасывается в <see cref="ClickStatus.NoClick"/>.
        /// </summary>
        public static MouseClickInfo CurrentClick
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentClick;
                }
            }
        }

        /// <summary>
        /// Последний зарегистрированный клик по Canvas.
        /// </summary>
        public static MouseClickInfo LastClick
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastClick;
                }
            }
        }
    }
}

