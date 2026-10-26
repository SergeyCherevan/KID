using System.IO;
using System.Resources;
using System.Text;
using KID.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KID.Tests.Infrastructure;

internal static class ResourceCatalogTestSupport
{
    internal static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));

    internal static Stream? OpenFailure(string failure) => failure switch
    {
        "missing" => null,
        "open" => throw new IOException("Cannot open stream."),
        "read" => new ThrowingReadStream(),
        "access" => throw new UnauthorizedAccessException("Access denied."),
        "manifest" => throw new MissingManifestResourceException("Missing catalog."),
        _ => throw new InvalidOperationException()
    };

    private sealed class ThrowingReadStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Read failed.");
        public override int Read(Span<byte> buffer) => throw new IOException("Read failed.");
    }
}

internal sealed class ResourceCatalogTestLogger : ILogger
{
    internal List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));

    internal void AssertCatalogWarning(string resourceName)
    {
        var warning = Assert.Single(Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Equal(DiagnosticEventIds.SettingsFallback, warning.EventId);
        Assert.NotNull(warning.Exception);
        Assert.Contains(resourceName, warning.Message);
        Assert.Contains(warning.Exception.Message, warning.Message);
    }

    internal sealed record LogEntry(LogLevel Level, EventId EventId, Exception? Exception, string Message);
}
