using Serilog;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace KID.Services.Diagnostics;

/// <summary>
/// Last-chance diagnostics that can operate before the regular DI graph exists.
/// </summary>
public sealed class GlobalExceptionHandler
{
    private readonly Serilog.ILogger logger;
    private readonly CrashReportWriter crashReportWriter;

    public GlobalExceptionHandler(
        Serilog.ILogger logger,
        CrashReportWriter crashReportWriter)
    {
        this.logger = logger?.ForContext<GlobalExceptionHandler>()
            ?? throw new ArgumentNullException(nameof(logger));
        this.crashReportWriter = crashReportWriter
            ?? throw new ArgumentNullException(nameof(crashReportWriter));
    }

    public CrashReportInfo RecordFatal(
        string source,
        Exception exception,
        long? executionId = null,
        EventId? eventId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(exception);

        var crashId = Guid.NewGuid().ToString("N");
        try
        {
            logger.Fatal(
                exception,
                "Unhandled application exception. EventId={EventId} Source={Source} CrashId={CrashId} ExecutionId={ExecutionId}",
                eventId?.Id,
                source,
                crashId,
                executionId);
        }
        catch (Exception loggingException)
        {
            Trace.TraceError("Fatal structured logging failed: {0}", loggingException);
        }

        var report = crashReportWriter.TryWrite(source, exception, executionId, crashId);
        if (report.Path == null)
        {
            try
            {
                logger.Error(
                    "Crash report could not be written. Source={Source} CrashId={CrashId}",
                    source,
                    crashId);
            }
            catch (Exception loggingException)
            {
                Trace.TraceError("Crash-report failure logging failed: {0}", loggingException);
            }
        }

        return report;
    }

    public void RecordUnobservedTask(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            logger.Error(
                exception,
                "Unobserved task exception. EventId={EventId} Origin={Origin}",
                DiagnosticEventIds.UnobservedTask.Id,
                "UnobservedTask");
        }
        catch (Exception loggingException)
        {
            Trace.TraceError("Unobserved-task logging failed: {0}", loggingException);
        }
    }
}
