namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single typed failure the app layer catches for a Location Search. The Gateway converts every
/// geocoding failure mode (transport/timeout, unparseable body, non-2xx status) into this —
/// fail-visible, always after logging endpoint + outcome at Error. Distinct from
/// <see cref="WeatherUnavailableException"/> so the two failure domains stay independently worded.
/// The message is diagnostic (logged), never shown to the user verbatim.
/// </summary>
public sealed class LocationSearchUnavailableException : Exception
{
    public LocationSearchUnavailableException(string message) : base(message) { }
    public LocationSearchUnavailableException(string message, Exception inner) : base(message, inner) { }
}
