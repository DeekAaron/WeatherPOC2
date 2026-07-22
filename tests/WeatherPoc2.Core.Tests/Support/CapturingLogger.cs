using Microsoft.Extensions.Logging;

namespace WeatherPoc2.Core.Tests.Support;

/// <summary>
/// A minimal <see cref="ILogger{T}"/> that records the formatted message of every log entry,
/// so tests can assert the endpoint + outcome Instrumentation contract (Technical-Context).
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    /// <summary>The rendered message of each log entry, in the order it was emitted.</summary>
    public List<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Messages.Add(formatter(state, exception));
}
