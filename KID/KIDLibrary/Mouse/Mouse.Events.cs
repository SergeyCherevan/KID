using System;
using System.Windows;

namespace KID
{
    /// <summary>
    /// Частичный класс Mouse - события мыши.
    /// </summary>
    public static partial class Mouse
    {
        /// <summary>
        /// Событие перемещения мыши по Canvas.
        /// </summary>
        public static event EventHandler<Point>? MouseMoveEvent;

        /// <summary>
        /// Событие клика мыши по Canvas.
        /// </summary>
        public static event EventHandler<MouseClickInfo>? MouseClickEvent;

        /// <summary>
        /// Вызывает событие перемещения мыши.
        /// </summary>
        /// <param name="position">Позиция курсора.</param>
        internal static void OnMouseMove(Point position)
        {
            MouseMoveEvent?.Invoke(null, position);
        }

        /// <summary>
        /// Вызывает событие клика мыши.
        /// </summary>
        /// <param name="clickInfo">Информация о клике.</param>
        internal static void OnMouseClick(MouseClickInfo clickInfo)
        {
            MouseClickEvent?.Invoke(null, clickInfo);
        }
    }
}
