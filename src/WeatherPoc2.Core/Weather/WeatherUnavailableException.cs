namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single typed failure the app layer catches. The Gateway converts every failure mode
/// (transport/timeout, unparseable body, error:true body, non-200 status, missing temperature_2m,
/// and a non-°C unit) into this — fail-visible, always after logging endpoint + outcome at Error.
/// The message is diagnostic (logged), never shown to the user verbatim.
/// </summary>
public sealed class WeatherUnavailableException : Exception
{
    public WeatherUnavailableException(string message) : base(message) { }
    public WeatherUnavailableException(string message, Exception inner) : base(message, inner) { }
}
