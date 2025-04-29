using KID.Services.Interfaces;
using KID.Services;
using System.Windows.Controls;
using KID;

public class CodeExecutionService
{
    private readonly ICodeCompiler compiler;
    private readonly ICodeRunner runner;
    private CancellationTokenSource? executionCts;

    public bool IsRunning => executionCts != null;

    public CodeExecutionService(ICodeCompiler compiler, ICodeRunner runner)
    {
        this.compiler = compiler;
        this.runner = runner;
    }

    public async Task ExecuteAsync(
        string code,
        Action<string> outputCallback,
        Canvas graphicsCanvas)
    {
        if (executionCts != null)
        {
            outputCallback?.Invoke("Программа уже запущена.");
            return;
        }

        executionCts = new CancellationTokenSource();
        var token = executionCts.Token;

        Graphics.Init(graphicsCanvas);

        var originalConsoleOut = Console.Out;
        Console.SetOut(new ConsoleRedirector(outputCallback));

        try
        {
            var compilationResult = await compiler.CompileAsync(code, token);

            if (!compilationResult.Success)
            {
                foreach (var error in compilationResult.Errors)
                {
                    outputCallback?.Invoke(error);
                }

                return;
            }

            await runner.RunAsync(compilationResult, token);
        }
        catch (OperationCanceledException)
        {
            outputCallback?.Invoke("Выполнение остановлено пользователем.");
        }
        catch (Exception ex)
        {
            outputCallback?.Invoke($"Ошибка выполнения: {ex.Message}");
        }
        finally
        {
            Console.SetOut(originalConsoleOut);
            executionCts = null;
        }
    }

    public void Cancel()
    {
        executionCts?.Cancel();
        executionCts = null;
    }
}
