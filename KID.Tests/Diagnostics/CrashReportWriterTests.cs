using System.IO;
using System.Text.Json;
using KID.Services.Diagnostics;

namespace KID.Tests.Diagnostics;

public sealed class CrashReportWriterTests
{
    [Fact]
    public void TryWrite_WritesStructuredReportAtomically()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KID-tests",
            Guid.NewGuid().ToString("N"));
        var writer = new CrashReportWriter(directory);

        var report = writer.TryWrite(
            "UnitTest",
            new InvalidOperationException("boom"),
            executionId: 77,
            crashId: "test-crash");

        Assert.Equal("test-crash", report.CrashId);
        Assert.NotNull(report.Path);
        var path = report.Path!;
        Assert.True(File.Exists(path));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("UnitTest", document.RootElement.GetProperty("source").GetString());
        Assert.Equal(77, document.RootElement.GetProperty("executionId").GetInt64());
        Assert.Contains("boom", document.RootElement.GetProperty("exception").GetString());

        Directory.Delete(directory, recursive: true);
    }
}
