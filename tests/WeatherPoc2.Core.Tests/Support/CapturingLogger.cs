using Microsoft.Extensions.Logging;

namespace WeatherPoc2.Core.Tests.Support;

/// <summary>
/// A minimal <see cref="ILogger{T}"/> that records the formatted message of every log entry,
/// so tests can assert the endpoint + outcome Instrumentation contract (Technical-Context).
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>A single captured log entry: its level and rendered message.</summary>
    public readonly record struct Entry(LogLevel Level, string Message);

    /// <summary>Every log entry (level + rendered message), in the order it was emitted.</summary>
    public List<Entry> Entries { get; } = new();

    /// <summary>The rendered message of each log entry, in the order it was emitted.</summary>
    public IEnumerable<string> Messages => Entries.Select(e => e.Message);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add(new Entry(logLevel, formatter(state, exception)));
}
