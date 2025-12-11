using System.Windows;

namespace KID
{
    /// <summary>
    /// Информация о клике мыши на Canvas.
    /// </summary>
    public struct MouseClickInfo
    {
        /// <summary>
        /// Статус клика.
        /// </summary>
        public ClickStatus Status { get; set; }
        
        /// <summary>
        /// Координаты клика относительно верхнего левого угла Canvas.
        /// null, если клик не был зарегистрирован.
        /// </summary>
        public Point? Position { get; set; }
    }
}
