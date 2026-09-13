using KID.Services.CodeExecution.Contexts.Interfaces;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using KID.Services.Errors;

namespace KID.Services.CodeExecution.Contexts
{
    public class CanvasGraphicsContext : IGraphicsContext
    {
        private readonly object gate = new();
        private ExecutionDispatcherScope? scope;
        private bool initialized;
        private bool disposing;
        private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<Canvas> initializeRuntime;
        public object GraphicsTarget { get; set; }

        public CanvasGraphicsContext(Canvas graphicsCanvas) : this(graphicsCanvas, InitializeRuntime) { }

        internal CanvasGraphicsContext(Canvas graphicsCanvas, Action<Canvas> initializeRuntime)
        {
            if (graphicsCanvas == null)
                throw new ArgumentNullException(nameof(graphicsCanvas));
            
            GraphicsTarget = graphicsCanvas;
            this.initializeRuntime = initializeRuntime ?? throw new ArgumentNullException(nameof(initializeRuntime));
        }

        public void Init(long executionId, CancellationToken cancellationToken, Dispatcher dispatcher)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposing, this);
                if (initialized) throw new InvalidOperationException("Graphics context is already initialized.");
                if (GraphicsTarget is not Canvas canvas) throw new InvalidOperationException("Graphics target must be a Canvas.");
                canvas.VerifyAccess();
                if (canvas.Dispatcher != dispatcher) throw new ArgumentException("Dispatcher must own the Canvas.", nameof(dispatcher));
                initialized = true;
                scope = DispatcherManager.BeginExecution(executionId, dispatcher, cancellationToken);
                Graphics.Init(canvas, scope);
                initializeRuntime(canvas);
            }
        }

        private static void InitializeRuntime(Canvas canvas)
        {
            Mouse.Init(canvas);
            Music.Init();
            var window = Window.GetWindow(canvas);
            if (window != null) Keyboard.Init(window);
            // Ожидание shutdown этих модулей добавляется в этапах 6–7.
        }

        /// <summary>Ожидает принятые UI-команды и сбрасывает bridges до выгрузки пользовательской ALC.</summary>
        public ValueTask DisposeAsync()
        {
            lock (gate)
            {
                if (!disposing)
                {
                    disposing = true;
                    _ = DisposeCoreAsync();
                }
            }
            return new ValueTask(disposed.Task);
        }

        private async Task DisposeCoreAsync()
        {
            var failures = new ExecutionFailureCollector();
            var owned = scope;
            if (owned != null)
            {
                await failures.CaptureAsync(() => owned.ShutdownAsync(() => Graphics.Release(owned)).AsTask(),
                    finallyAction: () =>
                    {
                        // Сброс только managed static-ссылок допустим и после shutdown Dispatcher.
                        Graphics.Release(owned);
                        scope = null;
                        GraphicsTarget = null!;
                    });
            }
            else GraphicsTarget = null!;
            var failure = failures.CreateException("Graphics cleanup failed.");
            if (failure == null) disposed.TrySetResult();
            else disposed.TrySetException(failure);
        }
    }
}
