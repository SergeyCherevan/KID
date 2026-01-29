namespace KID.Services.CodeExecution.Contexts.Interfaces
{
    public interface IConsoleContext : IDisposable
    {
        object ConsoleTarget { get; set; }

        void Init();
    }
}
