using System;

namespace KID
{
    public static partial class Keyboard
    {
        /// <summary>
        /// Событие нажатия клавиши (KeyDown).
        /// Обработчики вызываются в фоновом потоке (не в UI).
        /// </summary>
        public static event Action<KeyPressInfo>? KeyDownEvent;

        /// <summary>
        /// Событие отпускания клавиши (KeyUp).
        /// Обработчики вызываются в фоновом потоке (не в UI).
        /// </summary>
        public static event Action<KeyPressInfo>? KeyUpEvent;

        /// <summary>
        /// Событие текстового ввода (PreviewTextInput).
        /// Обработчики вызываются в фоновом потоке (не в UI).
        /// </summary>
        public static event Action<TextInputInfo>? TextInputEvent;

        /// <summary>
        /// Событие срабатывания хоткея (зарегистрированный Shortcut).
        /// Обработчики вызываются в фоновом потоке (не в UI).
        /// </summary>
        public static event Action<ShortcutFiredInfo>? ShortcutEvent;

        /// <summary>
        /// Создаёт отдельное действие для каждого подписчика. Исключение одного handler не
        /// пропускает остальных подписчиков и обрабатывается общей границей event worker.
        /// </summary>
        private static void EnqueueHandlers<T>(
            KeyboardExecutionScope scope,
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
            KeyDownEvent = null;
            KeyUpEvent = null;
            TextInputEvent = null;
            ShortcutEvent = null;
        }
    }
}

