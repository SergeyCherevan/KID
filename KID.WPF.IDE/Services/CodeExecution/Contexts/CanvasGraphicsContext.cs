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
        private DispatcherScope? scope;
        private ExecutionEnvironment? environment;
        private bool initialized;
        private bool disposing;
        private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action<Canvas, ExecutionEnvironment> initializeRuntime;
        public object GraphicsTarget { get; set; }

        public CanvasGraphicsContext(Canvas graphicsCanvas) : this(graphicsCanvas, InitializeRuntime) { }

        internal CanvasGraphicsContext(Canvas graphicsCanvas, Action<Canvas> initializeRuntime)
            : this(
                graphicsCanvas,
                initializeRuntime == null
                    ? throw new ArgumentNullException(nameof(initializeRuntime))
                    : (canvas, _) => initializeRuntime(canvas))
        {
        }

        internal CanvasGraphicsContext(
            Canvas graphicsCanvas,
            Action<Canvas, ExecutionEnvironment> initializeRuntime)
        {
            if (graphicsCanvas == null)
                throw new ArgumentNullException(nameof(graphicsCanvas));
            
            GraphicsTarget = graphicsCanvas;
            this.initializeRuntime = initializeRuntime ?? throw new ArgumentNullException(nameof(initializeRuntime));
        }

        public void Init(long executionId, Dispatcher dispatcher)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposing, this);
                if (initialized) throw new InvalidOperationException("Graphics context is already initialized.");
                if (GraphicsTarget is not Canvas canvas) throw new InvalidOperationException("Graphics target must be a Canvas.");
                canvas.VerifyAccess();
                if (canvas.Dispatcher != dispatcher) throw new ArgumentException("Dispatcher must own the Canvas.", nameof(dispatcher));
                initialized = true;
                environment = ExecutionEnvironmentManager.GetCurrent(executionId);
                scope = DispatcherManager.AttachDispatcher(executionId, dispatcher);
                Graphics.Init(canvas, scope);
                initializeRuntime(canvas, environment);
            }
        }

        private static void InitializeRuntime(Canvas canvas, ExecutionEnvironment environment)
        {
            Mouse.Init(canvas, environment);
            Music.Init(environment);
            var window = Window.GetWindow(canvas);
            if (window != null) Keyboard.Init(window, environment);
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
            var ownedEnvironment = environment;

            if (ownedEnvironment != null)
            {
                // Все runtime-модули синхронно закрывают приём в ShutdownAsync. Запускаем
                // shutdown до первого await: никакой новый звук или input callback уже не
                // сможет попасть в завершающуюся execution-сессию.
                _ = Keyboard.ShutdownAsync(ownedEnvironment);
                _ = Mouse.ShutdownAsync(ownedEnvironment);
                _ = Music.ShutdownAsync(ownedEnvironment);
                await failures.CaptureAsync(() => Keyboard.ShutdownAsync(ownedEnvironment).AsTask());
                await failures.CaptureAsync(() => Mouse.ShutdownAsync(ownedEnvironment).AsTask());
                await failures.CaptureAsync(() => Music.ShutdownAsync(ownedEnvironment).AsTask());
            }

            var owned = scope;
            if (owned != null)
            {
                await failures.CaptureAsync(() => owned.ShutdownAsync(() => Graphics.Release(owned)).AsTask(),
                    finallyAction: () =>
                    {
                        // Сброс только managed static-ссылок допустим и после shutdown Dispatcher.
                        Graphics.Release(owned);
                        scope = null;
                        environment = null;
                        GraphicsTarget = null!;
                    });
            }
            else
            {
                environment = null;
                GraphicsTarget = null!;
            }
            var failure = failures.CreateException("Graphics cleanup failed.");
            if (failure == null) disposed.TrySetResult();
            else disposed.TrySetException(failure);
        }
    }
}
