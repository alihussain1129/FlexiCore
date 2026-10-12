using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FlexiCore.IntegrationTests.Infrastructure;

/// <summary>Records every log entry (message, structured values, exception and scopes) so tests can assert nothing sensitive was logged.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            sink.Enqueue($"{category} [scope] {Describe(state)}");
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            sink.Enqueue($"{category} [{logLevel}] {formatter(state, exception)} | {Describe(state)} | {exception}");

        private static string Describe(object? state) =>
            state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(", ", pairs.Select(p => $"{p.Key}={p.Value}"))
                : state?.ToString() ?? string.Empty;
    }
}