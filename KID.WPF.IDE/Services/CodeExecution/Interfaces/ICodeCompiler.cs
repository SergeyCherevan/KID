namespace KID.Services.CodeExecution.Interfaces
{
    public interface ICodeCompiler
    {
        Task<CompilationResult> CompileAsync(string code, CancellationToken cancellationToken = default);
    }
}
