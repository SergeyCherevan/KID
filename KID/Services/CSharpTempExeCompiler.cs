using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using KID.Services.Interfaces;

namespace KID.Services
{
    public class CSharpTempExeCompiler : ICodeCompiler
    {
        public async Task<CompilationResult> CompileAsync(string code, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                // Создаём временную директорию для проекта
                var tempPath = Path.Combine(Path.GetTempPath(), $"KID_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempPath);

                try
                {
                    // Создаём файл проекта
                    var csprojContent = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""KID"">
      <HintPath>" + typeof(Graphics).Assembly.Location + @"</HintPath>
    </Reference>
  </ItemGroup>
</Project>";

                    File.WriteAllText(Path.Combine(tempPath, "UserProgram.csproj"), csprojContent);

                    // Создаём файл с кодом
                    File.WriteAllText(Path.Combine(tempPath, "Program.cs"), code);

                    // Запускаем dotnet publish
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "publish -c Release",
                        WorkingDirectory = tempPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(startInfo);
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        return new CompilationResult 
                        { 
                            Success = false, 
                            Errors = new List<string> { "Ошибка компиляции:", error }
                        };
                    }

                    var exePath = Path.Combine(tempPath, "bin", "Release", "net8.0-windows", "win-x64", "publish", "UserProgram.exe");

                    if (!File.Exists(exePath))
                    {
                        return new CompilationResult 
                        { 
                            Success = false, 
                            Errors = new List<string> { "Не удалось найти скомпилированный файл" }
                        };
                    }

                    return new CompilationResult
                    {
                        Success = true,
                        ExePath = exePath,
                        Errors = new List<string>()
                    };
                }
                catch (Exception ex)
                {
                    return new CompilationResult 
                    { 
                        Success = false, 
                        Errors = new List<string> { ex.Message }
                    };
                }
            }, cancellationToken);
        }
    }
}