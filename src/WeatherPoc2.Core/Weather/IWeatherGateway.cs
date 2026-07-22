namespace WeatherPoc2.Core.Weather;

/// <summary>The single external seam to Open-Meteo. Throws WeatherUnavailableException on any failure.</summary>
public interface IWeatherGateway
{
    Task<WeatherBundle> GetWeatherAsync(Location location, CancellationToken cancellationToken);
}
