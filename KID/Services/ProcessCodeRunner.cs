using KID.Services.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace KID.Services
{
    public class ProcessCodeRunner : ICodeRunner
    {
        private Process? process;

        public async Task RunAsync(CompilationResult compilationResult, CancellationToken cancellationToken = default)
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = compilationResult.ExePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // Используем существующий ConsoleRedirector через Console.WriteLine
            process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.Write(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Write(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await Task.Run(() =>
                {
                    process.WaitForExit();
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Kill();
                throw;
            }
        }

        public void Kill()
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Процесс мог уже завершиться
                }
            }
        }
    }
}