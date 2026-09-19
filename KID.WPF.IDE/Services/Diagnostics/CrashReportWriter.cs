using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KID.Services.Diagnostics;

public sealed record CrashReportInfo(string CrashId, string? Path);

/// <summary>
/// Writes a small local JSON report without depending on the DI graph or WPF resources.
/// </summary>
public sealed class CrashReportWriter
{
    private readonly string logsDirectory;

    public CrashReportWriter()
        : this(LoggingConfiguration.LogsDirectory)
    {
    }

    internal CrashReportWriter(string logsDirectory)
    {
        this.logsDirectory = string.IsNullOrWhiteSpace(logsDirectory)
            ? throw new ArgumentException("A logs directory is required.", nameof(logsDirectory))
            : Path.GetFullPath(logsDirectory);
    }

    public CrashReportInfo TryWrite(
        string source,
        Exception exception,
        long? executionId = null,
        string? crashId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(exception);

        var id = string.IsNullOrWhiteSpace(crashId)
            ? Guid.NewGuid().ToString("N")
            : crashId;
        var reportPath = Path.Combine(logsDirectory, $"crash-{id}.json");
        var temporaryPath = Path.Combine(logsDirectory, $".crash-{id}-{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(logsDirectory);

            var report = new CrashReportPayload(
                id,
                DateTimeOffset.UtcNow,
                source,
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,
                Environment.ProcessId,
                Environment.CurrentManagedThreadId,
                "Fatal",
                LoggingConfiguration.LogFilePattern,
                executionId,
                exception.ToString());

            var json = JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, reportPath, overwrite: true);
            return new CrashReportInfo(id, reportPath);
        }
        catch (Exception reportException)
        {
            Trace.TraceError("Crash report writing failed: {0}", reportException);
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
            {
                Trace.TraceError("Crash report temporary cleanup failed: {0}", cleanupException);
                // A crash handler must never throw while trying to report another failure.
            }

            return new CrashReportInfo(id, null);
        }
    }

    private sealed record CrashReportPayload(
        [property: JsonPropertyName("crashId")] string CrashId,
        [property: JsonPropertyName("timestampUtc")] DateTimeOffset TimestampUtc,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("applicationVersion")] string? ApplicationVersion,
        [property: JsonPropertyName("framework")] string Framework,
        [property: JsonPropertyName("os")] string OperatingSystem,
        [property: JsonPropertyName("processId")] int ProcessId,
        [property: JsonPropertyName("managedThreadId")] int ManagedThreadId,
        [property: JsonPropertyName("applicationState")] string ApplicationState,
        [property: JsonPropertyName("logFilePattern")] string LogFilePattern,
        [property: JsonPropertyName("executionId")] long? ExecutionId,
        [property: JsonPropertyName("exception")] string Exception);
}
