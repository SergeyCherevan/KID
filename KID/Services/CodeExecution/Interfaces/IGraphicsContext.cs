namespace KID.Services.CodeExecution.Interfaces
{
    public interface IGraphicsContext : IDisposable
    {
        object GraphicsTarget { get; set; }

        void Init();
    }
}
