namespace KID.Services.CodeExecution.Interfaces
{
    public interface IConsoleContext : IDisposable
    {
        object ConsoleTarget { get; set; }

        void Init();
    }
}
