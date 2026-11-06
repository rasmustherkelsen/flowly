using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception)
{
    public override string ToString() => $"{Level} {Category}: {Message}";
}

internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    public Observed<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new RecordingCategoryLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class RecordingCategoryLogger(string category, Observed<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add(new LogEntry(logLevel, category, formatter(state, exception), exception));
    }
}
