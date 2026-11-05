using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;

namespace KID.Services.Diagnostics;

/// <summary>
/// Creates the process-wide structured logger before the WPF DI graph is built.
/// </summary>
public static class LoggingConfiguration
{
    private const string ApplicationFolderName = "KID";
    private const string LogFileName = "kid-.jsonl";

    public static string LogsDirectory
    {
        get => KID.Services.Files.ApplicationDataPaths.LogsDirectory;
    }

    public static string LogFilePattern => Path.Combine(LogsDirectory, LogFileName);

    public static Serilog.ILogger CreateBootstrapLogger()
    {
        // Validate an explicit test root outside the best-effort logging catch.
        // An invalid override must never continue with real profile data.
        var logsDirectory = LogsDirectory;
        try
        {
            Directory.CreateDirectory(logsDirectory);

            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("Application", ApplicationFolderName)
                .Enrich.WithProperty("Category", "KID.App")
                .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    LogFilePattern,
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    retainedFileCountLimit: 14,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();
        }
        catch (Exception exception)
        {
            // Diagnostics must never prevent the application from starting. The caller still
            Trace.TraceError("Structured logging initialization failed: {0}", exception);
            // receives a valid no-op logger and crash-report writing remains best effort.
            return new LoggerConfiguration()
                .MinimumLevel.Warning()
                .CreateLogger();
        }
    }

    public static void AddTo(IServiceCollection services, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(logger, dispose: false);
        });
    }
}
