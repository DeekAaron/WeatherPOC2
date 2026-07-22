using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WeatherPoc2.Core.Weather;

public sealed class OpenMeteoGateway : IWeatherGateway
{
    public const string HttpClientName = "OpenMeteo";
    private const string BaseUrl = "https://api.open-meteo.com/v1/forecast";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenMeteoGateway> _logger;

    public OpenMeteoGateway(IHttpClientFactory httpClientFactory, ILogger<OpenMeteoGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WeatherBundle> GetWeatherAsync(Location location, CancellationToken cancellationToken)
    {
        var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"{BaseUrl}?latitude={lat}&longitude={lon}&current=temperature_2m&temperature_unit=celsius";

        var client = _httpClientFactory.CreateClient(HttpClientName);
        // Log the endpoint (URL) + outcome — Technical-Context Instrumentation contract.
        _logger.LogInformation("Open-Meteo GetWeather {Label} {Endpoint} → requesting", location.Label, url);

        var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        OpenMeteoResponse? parsed;
        try
        {
            // A malformed body — or a temperature_2m present but non-numeric — must surface as the
            // contracted WeatherUnavailableException, never a raw JsonException. Catch ONLY JsonException:
            // transport / non-200 / error:true / unit-mismatch conversion is deferred to the failure-paths story.
            parsed = JsonSerializer.Deserialize<OpenMeteoResponse>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Open-Meteo GetWeather {Label} {Endpoint} → malformed response body", location.Label, url);
            throw new WeatherUnavailableException("Open-Meteo response body was not valid JSON", ex);
        }

        if (parsed?.Current?.Temperature2m is not double temperatureCelsius)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → missing temperature_2m", location.Label, url);
            throw new WeatherUnavailableException("Open-Meteo response missing temperature_2m");
        }

        _logger.LogInformation("Open-Meteo GetWeather {Label} {Endpoint} → {Status}", location.Label, url, (int)response.StatusCode);
        return new WeatherBundle(temperatureCelsius);
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("error")] public bool Error { get; init; }
        [JsonPropertyName("reason")] public string? Reason { get; init; }
        [JsonPropertyName("current")] public CurrentDto? Current { get; init; }
        [JsonPropertyName("current_units")] public CurrentUnitsDto? CurrentUnits { get; init; }
    }

    private sealed class CurrentDto
    {
        [JsonPropertyName("temperature_2m")] public double? Temperature2m { get; init; }
    }

    private sealed class CurrentUnitsDto
    {
        [JsonPropertyName("temperature_2m")] public string? Temperature2m { get; init; }
    }
}
