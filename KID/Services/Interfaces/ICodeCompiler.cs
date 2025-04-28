namespace KID.Services.Interfaces
{
    public interface ICodeCompiler
    {
        CompilationResult Compile(string code);
    }
}
