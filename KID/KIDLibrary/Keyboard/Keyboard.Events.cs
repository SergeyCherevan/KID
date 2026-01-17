using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace KID
{
    public static partial class Keyboard
    {
        private static readonly ConcurrentQueue<Action> _eventQueue = new ConcurrentQueue<Action>();
        private static readonly SemaphoreSlim _eventSignal = new SemaphoreSlim(0, int.MaxValue);

        private static CancellationTokenSource? _eventCts;
        private static Task? _eventWorkerTask;

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

        private static void EnqueueEvent(Action action)
        {
            if (action == null)
                return;

            var cts = _eventCts;
            if (cts == null || cts.IsCancellationRequested)
                return;

            _eventQueue.Enqueue(action);
            _eventSignal.Release();
        }

        private static void StartEventWorker()
        {
            StopEventWorker();

            _eventCts = new CancellationTokenSource();
            var token = _eventCts.Token;

            _eventWorkerTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await _eventSignal.WaitAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    while (_eventQueue.TryDequeue(out var a))
                    {
                        try
                        {
                            a();
                        }
                        catch
                        {
                            // Ошибки пользовательских обработчиков не должны «ронять» поток доставки событий.
                        }
                    }
                }

                while (_eventQueue.TryDequeue(out _)) { }
            }, token);
        }

        private static void StopEventWorker()
        {
            try
            {
                _eventCts?.Cancel();
            }
            catch { }
            finally
            {
                _eventCts?.Dispose();
                _eventCts = null;
            }

            // «Пнуть» WaitAsync, чтобы loop смог выйти.
            try { _eventSignal.Release(); } catch { }

            _eventWorkerTask = null;

            while (_eventQueue.TryDequeue(out _)) { }
        }
    }
}

