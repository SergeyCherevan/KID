using KID.Services.CodeExecution.Contexts.Interfaces;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KID.Services.CodeExecution.Contexts;

/// <summary>Adapts IDE controls and sizing to the shared library lifecycle.</summary>
public class CanvasGraphicsContext : IGraphicsContext
{
    private readonly Lock gate = new();
    private readonly Action<Canvas, ExecutionEnvironment>? initializeRuntime;
    private RuntimeSessionOwner? owner;
    private Canvas? initializedCanvas;
    private bool initializationFailed;
    private bool cleanupStarted;
    private Task? cleanup;

    public object GraphicsTarget { get; set; }

    public CanvasGraphicsContext(Canvas graphicsCanvas)
    {
        ArgumentNullException.ThrowIfNull(graphicsCanvas);
        GraphicsTarget = graphicsCanvas;
    }

    internal CanvasGraphicsContext(Canvas canvas, Action<Canvas> initializeRuntime)
        : this(canvas, (target, _) => initializeRuntime(target))
    {
        ArgumentNullException.ThrowIfNull(initializeRuntime);
    }

    internal CanvasGraphicsContext(Canvas canvas, Action<Canvas, ExecutionEnvironment> initializeRuntime)
        : this(canvas) => this.initializeRuntime = initializeRuntime ?? throw new ArgumentNullException(nameof(initializeRuntime));

    public void Init(long executionId, Dispatcher dispatcher)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(cleanupStarted, this);
            if (initializedCanvas != null)
            {
                if (owner!.Environment.ExecutionId == executionId &&
                    ReferenceEquals(initializedCanvas, GraphicsTarget) &&
                    ReferenceEquals(initializedCanvas.Dispatcher, dispatcher)) return;
                throw new InvalidOperationException("Graphics context is already initialized for another session or UI target.");
            }
            if (initializationFailed)
                throw new InvalidOperationException("Graphics context cannot be initialized after a partial initialization failure.");
            if (GraphicsTarget is not Canvas canvas)
                throw new InvalidOperationException("Graphics target must be a Canvas.");
            canvas.VerifyAccess();
            if (canvas.Dispatcher != dispatcher)
                throw new ArgumentException("Dispatcher must own the Canvas.", nameof(dispatcher));

            var runtime = RuntimeSessionOwner.GetCurrent(executionId);
            var hadGraphics = Graphics.Canvas != null;
            try
            {
                runtime.Initialize(new KIDRuntimeOptions
                {
                    Canvas = canvas,
                    MouseCanvas = initializeRuntime == null ? canvas : null,
                    KeyboardWindow = initializeRuntime == null ? Window.GetWindow(canvas) : null,
                    EnableMusic = initializeRuntime == null,
                    CanvasSizeHandler = new CanvasLayoutAdapter(canvas).TrySetSize
                }, initializeRuntime: initializeRuntime == null ? null : environment => initializeRuntime(canvas, environment));
                owner = runtime;
                initializedCanvas = canvas;
            }
            catch
            {
                if (!hadGraphics && ReferenceEquals(Graphics.Canvas, canvas)) owner = runtime;
                initializationFailed = true;
                throw;
            }
        }
    }

    public void BeginCleanup()
    {
        lock (gate)
        {
            if (cleanupStarted) return;
            cleanupStarted = true;
            owner?.BeginCleanup();
        }
    }

    public ValueTask DisposeAsync()
    {
        BeginCleanup();
        lock (gate) return new ValueTask(cleanup ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try { if (owner != null) await owner.CleanupAsync(); }
        finally { initializedCanvas = null; GraphicsTarget = null!; }
    }
}
