namespace KID.Services.CodeExecution.Contexts.Interfaces
{
    public interface IGraphicsContext : IDisposable
    {
        object GraphicsTarget { get; set; }

        void Init();
    }
}
