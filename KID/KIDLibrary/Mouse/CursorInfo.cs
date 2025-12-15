using System.Windows;

namespace KID
{
    /// <summary>
    /// Информация о курсоре мыши на Canvas.
    /// </summary>
    public struct CursorInfo
    {
        /// <summary>
        /// Позиция курсора относительно верхнего левого угла Canvas.
        /// null, если курсор сейчас не на Canvas.
        /// </summary>
        public Point? Position { get; set; }
        
        /// <summary>
        /// Состояние нажатых кнопок мыши.
        /// Может включать комбинации флагов LeftButton, RightButton и OutOfArea.
        /// </summary>
        public PressButtonStatus PressedButton { get; set; }
    }
}

