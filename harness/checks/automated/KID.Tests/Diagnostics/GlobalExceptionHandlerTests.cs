using System.IO;
using System.Text.Json;
using KID.Services.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;

namespace KID.Tests.Diagnostics;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public void RecordFatal_WritesCrashReportAndStructuredFatalEvent()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KID-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var logPath = Path.Combine(directory, "kid-.jsonl");
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(logPath)
            .CreateLogger();
        var handler = new GlobalExceptionHandler(logger, new CrashReportWriter(directory));

        var report = handler.RecordFatal(
            "UnitTest",
            new InvalidOperationException("fatal"),
            executionId: 42,
            eventId: new EventId(9001, "TestFatal"));

        Assert.Equal("UnitTest", JsonDocument.Parse(File.ReadAllText(report.Path!))
            .RootElement.GetProperty("source").GetString());
        logger.Dispose();
        Assert.Contains("EventId=9001", File.ReadAllText(logPath));

        Directory.Delete(directory, recursive: true);
    }
}
