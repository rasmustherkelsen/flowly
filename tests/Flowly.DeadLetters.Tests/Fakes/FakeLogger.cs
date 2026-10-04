using Microsoft.Extensions.Logging;

namespace Flowly.DeadLetters.Tests.Fakes;

internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Messages { get; } = [];

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Messages.Add((logLevel, formatter(state, exception), exception));
}
