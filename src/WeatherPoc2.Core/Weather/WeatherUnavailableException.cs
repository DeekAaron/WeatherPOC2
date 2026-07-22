namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single typed failure the app layer catches. The Gateway will convert every failure mode
/// (transport, non-200, error:true body, malformed/missing field) into this — fail-visible, always
/// after logging; this happy-path story wires the malformed/missing-field conversion, and the
/// remaining branches arrive with the failure-paths story. The message is diagnostic (logged),
/// never shown to the user verbatim.
/// </summary>
public sealed class WeatherUnavailableException : Exception
{
    public WeatherUnavailableException(string message) : base(message) { }
    public WeatherUnavailableException(string message, Exception inner) : base(message, inner) { }
}
