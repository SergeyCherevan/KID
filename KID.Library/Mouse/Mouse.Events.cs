using System;

namespace KID
{
    public static partial class Mouse
    {
        /// <summary>
        /// Событие перемещения мыши по Canvas.
        /// Обработчики вызываются в фоновом потоке (не в UI).
        /// </summary>
        public static event Action<CursorInfo>? MouseMoveEvent;

        /// <summary>
        /// Событие, связанное с изменением <see cref="CurrentCursor"/>.<see cref="CursorInfo.PressedButton"/>.
        /// Обработчики вызываются в фоновом потоке (не в UI).
        /// </summary>
        public static event Action<CursorInfo>? MousePressButtonEvent;

        /// <summary>
        /// Событие клика мышью по Canvas.
        /// Обработчики вызываются в фоновом потоке (не в UI).
        /// </summary>
        public static event Action<MouseClickInfo>? MouseClickEvent;

        /// <summary>
        /// Создаёт отдельное действие для каждого подписчика. Исключение одного handler не
        /// пропускает остальных подписчиков и обрабатывается общей границей event worker.
        /// </summary>
        private static void EnqueueHandlers<T>(
            MouseExecutionScope scope,
            Action<T>? handlers,
            T value)
        {
            if (handlers == null) return;
            foreach (var candidate in handlers.GetInvocationList())
            {
                var handler = (Action<T>)candidate;
                _ = scope.EventWorker.TryEnqueue(() =>
                {
                    if (IsCurrent(scope) && scope.EventWorker.IsAccepting)
                        handler(value);
                });
            }
        }

        /// <summary>Освобождает все delegates, которые могли ссылаться на пользовательскую ALC.</summary>
        private static void ClearUserEvents()
        {
            MouseMoveEvent = null;
            MousePressButtonEvent = null;
            MouseClickEvent = null;
        }
    }
}

