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
        var url = $"{BaseUrl}?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,wind_speed_10m,weather_code,is_day" +
                  "&hourly=precipitation_probability" +
                  "&temperature_unit=celsius&wind_speed_unit=kmh";

        var client = _httpClientFactory.CreateClient(HttpClientName);
        // Log the endpoint (URL) + outcome — Technical-Context Instrumentation contract.
        _logger.LogInformation("Open-Meteo GetWeather {Label} {Endpoint} → requesting", location.Label, url);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.GetAsync(url, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // HttpRequestException = network/DNS/oversized-read; TaskCanceledException = request-timeout expiry.
            _logger.LogError(ex, "Open-Meteo GetWeather {Label} {Endpoint} → transport failure", location.Label, url);
            throw new WeatherUnavailableException("Open-Meteo transport failure", ex);
        }

        OpenMeteoResponse? parsed;
        try
        {
            // A malformed body — or a temperature_2m present but non-numeric — must surface as the
            // contracted WeatherUnavailableException, never a raw JsonException. Catch ONLY JsonException
            // here; transport, error:true, non-200, missing-field and unit-mismatch each have their own
            // guard below (branch order: transport → JSON → error:true → status → missing → unit).
            parsed = JsonSerializer.Deserialize<OpenMeteoResponse>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Open-Meteo GetWeather {Label} {Endpoint} → malformed response body", location.Label, url);
            throw new WeatherUnavailableException("Open-Meteo response body was not valid JSON", ex);
        }

        if (parsed is { Error: true })
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → error body: {Reason} (HTTP {Status})",
                location.Label, url, parsed.Reason, (int)response.StatusCode);
            throw new WeatherUnavailableException($"Open-Meteo error: {parsed.Reason}");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → HTTP {Status}", location.Label, url, (int)response.StatusCode);
            throw new WeatherUnavailableException($"Open-Meteo HTTP {(int)response.StatusCode}");
        }

        if (parsed?.Current?.Temperature2m is not double temperatureCelsius)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → missing temperature_2m", location.Label, url);
            throw new WeatherUnavailableException("Open-Meteo response missing temperature_2m");
        }

        // Unit assertion — the °C guarantee is proven on the wire, never assumed from the API default.
        var unit = parsed.CurrentUnits?.Temperature2m;
        if (!string.Equals(unit, "°C", StringComparison.Ordinal))
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → unexpected unit {Unit} (expected °C)",
                location.Label, url, unit);
            throw new WeatherUnavailableException($"Open-Meteo returned unexpected unit '{unit}' (expected °C)");
        }

        // wind (happy path — value read; the unit assertion + missing-field guard land in Task 4)
        var windKmh = parsed.Current!.WindSpeed10m!.Value;

        // current-hour chance of rain (happy path): truncate current.time to the hour, find it in hourly.time[]
        var hourKey = parsed.Current.Time![..13] + ":00";                 // "2026-07-22T17:30" -> "2026-07-22T17:00"
        var idx = Array.IndexOf(parsed.Hourly!.Time!, hourKey);
        var chanceOfRain = parsed.Hourly.PrecipitationProbability![idx]!.Value;

        // lenient icon-only hints — flow through; the mapper (in the VM) resolves Unknown / day
        int? weatherCode = parsed.Current.WeatherCode;
        bool? isDay = parsed.Current.IsDay switch { 1 => true, 0 => false, _ => null };

        _logger.LogInformation("Open-Meteo GetWeather {Label} {Endpoint} → {Status}", location.Label, url, (int)response.StatusCode);
        return new WeatherBundle(temperatureCelsius, windKmh, chanceOfRain, weatherCode, isDay);
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("error")] public bool Error { get; init; }
        [JsonPropertyName("reason")] public string? Reason { get; init; }
        [JsonPropertyName("current")] public CurrentDto? Current { get; init; }
        [JsonPropertyName("current_units")] public CurrentUnitsDto? CurrentUnits { get; init; }
        [JsonPropertyName("hourly")] public HourlyDto? Hourly { get; init; }
    }

    private sealed class CurrentDto
    {
        [JsonPropertyName("time")] public string? Time { get; init; }
        [JsonPropertyName("temperature_2m")] public double? Temperature2m { get; init; }
        [JsonPropertyName("wind_speed_10m")] public double? WindSpeed10m { get; init; }
        [JsonPropertyName("weather_code")] public int? WeatherCode { get; init; }
        [JsonPropertyName("is_day")] public int? IsDay { get; init; }
    }

    private sealed class CurrentUnitsDto
    {
        [JsonPropertyName("temperature_2m")] public string? Temperature2m { get; init; }
        [JsonPropertyName("wind_speed_10m")] public string? WindSpeed10m { get; init; }
    }

    private sealed class HourlyDto
    {
        [JsonPropertyName("time")] public string[]? Time { get; init; }
        [JsonPropertyName("precipitation_probability")] public int?[]? PrecipitationProbability { get; init; }
    }
}
