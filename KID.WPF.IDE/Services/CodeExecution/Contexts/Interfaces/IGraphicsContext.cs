namespace KID.Services.CodeExecution.Contexts.Interfaces
{
    public interface IGraphicsContext : IAsyncDisposable
    {
        object GraphicsTarget { get; set; }

        void Init(long executionId, CancellationToken cancellationToken, System.Windows.Threading.Dispatcher dispatcher);
    }
}
