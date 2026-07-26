namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single external seam to Open-Meteo. Surfaces failure as WeatherUnavailableException; this
/// happy-path story wires the missing/malformed-field conversion, and the remaining failure branches
/// (transport, non-200, error:true body, unit mismatch) are added by the failure-paths story.
/// </summary>
public interface IWeatherGateway
{
    Task<WeatherBundle> GetWeatherAsync(Location location, CancellationToken cancellationToken);

    /// <summary>
    /// Geocoding half of the seam: resolves a typed name to zero or more Search Candidates.
    /// An empty list means "no matching places" (not an error). Surfaces transport/parse/non-2xx
    /// failure as <see cref="LocationSearchUnavailableException"/>.
    /// </summary>
    Task<IReadOnlyList<SearchCandidate>> SearchAsync(string name, CancellationToken cancellationToken);
}
