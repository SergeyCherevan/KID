using KID.Services.Errors;
using KID.Services.Localization.Interfaces;
using Microsoft.Extensions.Logging;

namespace KID.Tests.Diagnostics;

public sealed class AsyncOperationErrorHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LogsOneStructuredFailureAndInvokesDialog()
    {
        var logger = new RecordingLogger<AsyncOperationErrorHandler>();
        Exception? shownException = null;
        string? shownKey = null;
        var handler = new AsyncOperationErrorHandler(
            new TestLocalizationService(),
            logger,
            (exception, key) =>
            {
                shownException = exception;
                shownKey = key;
            });

        await handler.ExecuteAsync(
            static () => Task.FromException(new InvalidOperationException("failure")),
            "Error_Test");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("AsyncOperationFailed", entry.EventId.Name);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal("Error_Test", shownKey);
        Assert.IsType<InvalidOperationException>(shownException);
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public string CurrentCulture => "en-US";
        public event EventHandler? CultureChanged
        {
            add { }
            remove { }
        }

        public string GetString(string key) => key;
        public string GetString(string key, params object[] args) => key;
        public Task SetCultureAsync(string cultureCode) => Task.CompletedTask;
        public IEnumerable<string> GetAvailableLanguages() => [];
        public string GetCultureCodeByLanguageKey(string languageKey) => string.Empty;
        public string GetLanguageKeyByCultureCode(string cultureCode) => string.Empty;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        internal List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
        }

        internal sealed record LogEntry(
            LogLevel Level,
            EventId EventId,
            Exception? Exception,
            string Message);
    }
}
