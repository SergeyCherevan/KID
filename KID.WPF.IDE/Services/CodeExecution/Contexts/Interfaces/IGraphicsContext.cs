namespace KID.Services.CodeExecution.Contexts.Interfaces
{
    public interface IGraphicsContext : IAsyncDisposable
    {
        object GraphicsTarget { get; set; }

        void Init(long executionId, System.Windows.Threading.Dispatcher dispatcher);
    }
}
