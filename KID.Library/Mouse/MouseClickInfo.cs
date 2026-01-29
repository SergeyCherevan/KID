using System.Windows;

namespace KID
{
    /// <summary>
    /// Информация о клике мышью по Canvas.
    /// </summary>
    public struct MouseClickInfo
    {
        /// <summary>
        /// Тип клика (одинарный/двойной, левая/правая кнопка).
        /// </summary>
        public ClickStatus Status;

        /// <summary>
        /// Координата клика относительно левого верхнего угла Canvas.
        /// </summary>
        public Point? Position;

        /// <summary>
        /// Создаёт информацию о клике.
        /// </summary>
        public MouseClickInfo(ClickStatus status, Point? position)
        {
            Status = status;
            Position = position;
        }
    }
}

