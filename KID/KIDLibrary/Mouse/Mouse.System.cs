using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KID.Services.CodeExecution;

namespace KID
{
    /// <summary>
    /// Статический частичный класс для работы с мышью на Canvas.
    /// Предоставляет информацию о позиции курсора и кликах мыши.
    /// </summary>
    public static partial class Mouse
    {
        // Константы для обработки кликов
        internal const int DoubleClickDelayMs = 500; // Интервал для определения двойного клика в миллисекундах
        internal const double DoubleClickPositionTolerance = 5.0; // Допуск для определения двойного клика в пикселях

        /// <summary>
        /// Canvas, на котором отслеживаются события мыши.
        /// </summary>
        public static Canvas Canvas { get; private set; }

        /// <summary>
        /// Инициализация Mouse API с Canvas.
        /// </summary>
        /// <param name="targetCanvas">Canvas для отслеживания событий мыши.</param>
        public static void Init(Canvas targetCanvas)
        {
            if (targetCanvas == null)
                throw new ArgumentNullException(nameof(targetCanvas));

            // Отписываемся от предыдущего Canvas, если был
            if (Canvas != null)
            {
                UnsubscribeFromEvents(Canvas);
            }

            Canvas = targetCanvas;

            // Подписываемся на события Canvas
            SubscribeToEvents(Canvas);
        }

        /// <summary>
        /// Подписывается на события мыши Canvas.
        /// </summary>
        private static void SubscribeToEvents(Canvas canvas)
        {
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseLeave += Canvas_MouseLeave;
            canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            canvas.MouseRightButtonDown += Canvas_MouseRightButtonDown;
            canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            canvas.MouseRightButtonUp += Canvas_MouseRightButtonUp;
            // Двойной клик обрабатывается через логику в Mouse.Click.cs
        }

        /// <summary>
        /// Отписывается от событий мыши Canvas.
        /// </summary>
        private static void UnsubscribeFromEvents(Canvas canvas)
        {
            canvas.MouseMove -= Canvas_MouseMove;
            canvas.MouseLeave -= Canvas_MouseLeave;
            canvas.MouseLeftButtonDown -= Canvas_MouseLeftButtonDown;
            canvas.MouseRightButtonDown -= Canvas_MouseRightButtonDown;
            canvas.MouseLeftButtonUp -= Canvas_MouseLeftButtonUp;
            canvas.MouseRightButtonUp -= Canvas_MouseRightButtonUp;
        }

        // Обработчики событий - вызывают partial методы, реализованные в других файлах
        private static void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            OnMouseMove(e);
        }

        private static void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            OnMouseLeave(e);
        }

        private static void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OnMouseLeftButtonDown(e);
        }

        private static void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            OnMouseRightButtonDown(e);
        }

        private static void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OnMouseLeftButtonUp(e);
        }

        private static void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            OnMouseRightButtonUp(e);
        }

        // Partial методы для реализации в других файлах
        static partial void OnMouseMove(MouseEventArgs e);
        static partial void OnMouseLeave(MouseEventArgs e);
        static partial void OnMouseLeftButtonDown(MouseButtonEventArgs e);
        static partial void OnMouseRightButtonDown(MouseButtonEventArgs e);
        static partial void OnMouseLeftButtonUp(MouseButtonEventArgs e);
        static partial void OnMouseRightButtonUp(MouseButtonEventArgs e);
    }
}
