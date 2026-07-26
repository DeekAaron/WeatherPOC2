using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WeatherPoc2.Core.Weather;

public sealed class OpenMeteoGateway : IWeatherGateway
{
    public const string HttpClientName = "OpenMeteo";
    private const string BaseUrl = "https://api.open-meteo.com/v1/forecast";
    private const string GeocodingBaseUrl = "https://geocoding-api.open-meteo.com/v1/search";

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
                  "&hourly=temperature_2m,weather_code,precipitation_probability,is_day" +
                  "&timezone=auto&forecast_days=2" +
                  "&temperature_unit=celsius&wind_speed_unit=kmh";

        var client = _httpClientFactory.CreateClient(HttpClientName);
        // Log the endpoint + outcome — Technical-Context Instrumentation contract. Endpoint-only logging
        // (BaseUrl = scheme+host+path, never the coordinate-bearing `url`) keeps the Location's
        // latitude/longitude out of the log sink (Story #69 security control); `url` is used only for the
        // actual GetAsync request below.
        _logger.LogInformation("Open-Meteo GetWeather {Label} {Endpoint} → requesting", location.Label, BaseUrl);

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
            _logger.LogError(ex, "Open-Meteo GetWeather {Label} {Endpoint} → transport failure", location.Label, BaseUrl);
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
            _logger.LogError(ex, "Open-Meteo GetWeather {Label} {Endpoint} → malformed response body", location.Label, BaseUrl);
            throw new WeatherUnavailableException("Open-Meteo response body was not valid JSON", ex);
        }

        if (parsed is { Error: true })
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → error body: {Reason} (HTTP {Status})",
                location.Label, BaseUrl, parsed.Reason, (int)response.StatusCode);
            throw new WeatherUnavailableException($"Open-Meteo error: {parsed.Reason}");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → HTTP {Status}", location.Label, BaseUrl, (int)response.StatusCode);
            throw new WeatherUnavailableException($"Open-Meteo HTTP {(int)response.StatusCode}");
        }

        if (parsed?.Current?.Temperature2m is not double temperatureCelsius)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → missing temperature_2m", location.Label, BaseUrl);
            throw new WeatherUnavailableException("Open-Meteo response missing temperature_2m");
        }

        // Unit assertion — the °C guarantee is proven on the wire, never assumed from the API default.
        var unit = parsed.CurrentUnits?.Temperature2m;
        if (!string.Equals(unit, "°C", StringComparison.Ordinal))
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → unexpected unit {Unit} (expected °C)",
                location.Label, BaseUrl, unit);
            throw new WeatherUnavailableException($"Open-Meteo returned unexpected unit '{unit}' (expected °C)");
        }

        // wind unit — belt-and-suspenders, mirrors F1's °C assertion
        var windUnit = parsed.CurrentUnits?.WindSpeed10m;
        if (!string.Equals(windUnit, "km/h", StringComparison.Ordinal))
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → unexpected wind unit {Unit} (expected km/h)",
                location.Label, BaseUrl, windUnit);
            throw new WeatherUnavailableException($"Open-Meteo returned unexpected wind unit '{windUnit}' (expected km/h)");
        }

        if (parsed.Current?.WindSpeed10m is not double windKmh)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → missing wind_speed_10m", location.Label, BaseUrl);
            throw new WeatherUnavailableException("Open-Meteo response missing wind_speed_10m");
        }

        // current-hour chance of rain (strict): truncate current.time to the hour, find it in hourly.time[],
        // read the parallel precipitation_probability[] at that index. An absent series, an unmatched hour,
        // a mismatched-length probability array, or a null probability all fail closed (0 is a valid value).
        var currentTime = parsed.Current.Time;
        var hourlyTimes = parsed.Hourly?.Time;
        var hourlyProbs = parsed.Hourly?.PrecipitationProbability;
        if (currentTime is null || currentTime.Length < 13 || hourlyTimes is null || hourlyProbs is null)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → hourly precipitation series unavailable", location.Label, BaseUrl);
            throw new WeatherUnavailableException("Open-Meteo hourly precipitation series unavailable");
        }
        var hourKey = currentTime[..13] + ":00";                          // "2026-07-22T17:30" -> "2026-07-22T17:00"
        var idx = Array.IndexOf(hourlyTimes, hourKey);
        if (idx < 0 || idx >= hourlyProbs.Length || hourlyProbs[idx] is not int chanceOfRain)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → no precipitation probability for {Hour}", location.Label, BaseUrl, hourKey);
            throw new WeatherUnavailableException("Open-Meteo has no current-hour precipitation probability");
        }

        // lenient icon-only hints — flow through; the mapper (in the VM) resolves Unknown / day
        int? weatherCode = parsed.Current.WeatherCode;
        bool? isDay = parsed.Current.IsDay switch { 1 => true, 0 => false, _ => null };

        // --- Feature 4: the Location-local "now" + the full hourly series (timezone=auto, ADR-0002) ---
        // current.time is a local wall-clock ISO8601 string with NO offset designator (Seam 2): parse
        // invariantly to a Kind=Unspecified DateTime — never apply a device tz/locale shift.
        if (!TryParseLocal(currentTime, out var localNow))
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → unparseable current.time '{Time}'", location.Label, BaseUrl, currentTime);
            throw new WeatherUnavailableException("Open-Meteo current.time was not a parseable local timestamp");
        }

        var temps = parsed.Hourly?.Temperature2m;
        var codes = parsed.Hourly?.WeatherCode;
        var isDays = parsed.Hourly?.IsDay;
        // Fail closed: every requested hourly array must be present and equal-length to time[]
        // (hourlyTimes / hourlyProbs already validated non-null above for the current-hour chance).
        if (temps is null || codes is null || isDays is null ||
            temps.Length != hourlyTimes.Length ||
            codes.Length != hourlyTimes.Length ||
            hourlyProbs.Length != hourlyTimes.Length ||
            isDays.Length != hourlyTimes.Length)
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → hourly arrays absent or mismatched in length", location.Label, BaseUrl);
            throw new WeatherUnavailableException("Open-Meteo hourly arrays absent or mismatched in length");
        }

        // Pin the hourly-series units on the wire — °C / % — the same belt-and-suspenders assertion
        // F1/F2 make for current_units, rather than inferring them transitively from the request param.
        // Runs after the array-structure guard, so a mismatched/missing-array fixture still fails on its
        // own branch first.
        if (parsed.HourlyUnits?.Temperature2m != "°C" || parsed.HourlyUnits?.PrecipitationProbability != "%")
        {
            _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → hourly units not canonical (°C / %)", location.Label, BaseUrl);
            throw new WeatherUnavailableException("Open-Meteo hourly units were not the requested °C / %");
        }

        var hourly = new List<HourlyForecastPoint>(hourlyTimes.Length);
        for (var i = 0; i < hourlyTimes.Length; i++)
        {
            if (!TryParseLocal(hourlyTimes[i], out var pointTime))
            {
                _logger.LogError("Open-Meteo GetWeather {Label} {Endpoint} → unparseable hourly.time[{Index}] '{Time}'", location.Label, BaseUrl, i, hourlyTimes[i]);
                throw new WeatherUnavailableException("Open-Meteo hourly.time carried an unparseable local timestamp");
            }
            // A null element in any value array soft-passes as a null field (Seam 1 soft-passthrough);
            // it is never a fetch failure.
            bool? pointIsDay = isDays[i] switch { 1 => true, 0 => false, _ => null };
            hourly.Add(new HourlyForecastPoint(pointTime, temps[i], codes[i], pointIsDay, hourlyProbs[i]));
        }

        _logger.LogInformation("Open-Meteo GetWeather {Label} {Endpoint} → {Status}", location.Label, BaseUrl, (int)response.StatusCode);
        return new WeatherBundle(temperatureCelsius, windKmh, chanceOfRain, weatherCode, isDay, hourly, localNow);
    }

    // Seam 2 (host-OS/runtime, internal): Open-Meteo's offset-less local ISO strings parse to a
    // Kind=Unspecified wall-clock DateTime, invariant-culture, with NO tz/locale shift applied.
    private static bool TryParseLocal(string? iso, out DateTime local) =>
        DateTime.TryParseExact(
            iso, "yyyy-MM-dd'T'HH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

    public async Task<IReadOnlyList<SearchCandidate>> SearchAsync(string name, CancellationToken cancellationToken)
    {
        // name is untrusted free-text — percent-encode it INSIDE the `name` value so a crafted query
        // can neither inject an extra query parameter nor override the fixed count/format/language.
        var url = $"{GeocodingBaseUrl}?name={Uri.EscapeDataString(name)}&count=10&language=en&format=json";
        var client = _httpClientFactory.CreateClient(HttpClientName);
        // Log the endpoint (URL) + outcome — Technical-Context Instrumentation contract.
        _logger.LogInformation("Open-Meteo Search {Query} {Endpoint} → requesting", name, url);

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
            _logger.LogError(ex, "Open-Meteo Search {Query} {Endpoint} → transport failure", name, url);
            throw new LocationSearchUnavailableException("Open-Meteo geocoding transport failure", ex);
        }

        // Branch order mirrors GetWeatherAsync: transport → JSON → status → map.
        GeocodingResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeocodingResponse>(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Open-Meteo Search {Query} {Endpoint} → malformed response body", name, url);
            throw new LocationSearchUnavailableException("Open-Meteo geocoding response body was not valid JSON", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Open-Meteo Search {Query} {Endpoint} → HTTP {Status}", name, url, (int)response.StatusCode);
            throw new LocationSearchUnavailableException($"Open-Meteo geocoding HTTP {(int)response.StatusCode}");
        }

        // No matches: the `results` key is ABSENT on a 200 (proven live 2026-07-23) → Results is null.
        // Treat absent-or-empty as zero Candidates — never dereference a null list.
        var results = parsed?.Results;
        if (results is null || results.Count == 0)
        {
            _logger.LogInformation("Open-Meteo Search {Query} {Endpoint} → {Status}, 0 candidates", name, url, (int)response.StatusCode);
            return Array.Empty<SearchCandidate>();
        }

        var candidates = results
            .Select(r => new SearchCandidate(r.Id, r.Name, r.Admin1, r.Country, r.Latitude, r.Longitude))
            .ToList();
        _logger.LogInformation("Open-Meteo Search {Query} {Endpoint} → {Status}, {Count} candidates",
            name, url, (int)response.StatusCode, candidates.Count);
        return candidates;
    }

    private sealed class GeocodingResponse
    {
        // Absent on a no-match 200 → null. Never assume an empty array.
        [JsonPropertyName("results")] public List<GeocodingResult>? Results { get; init; }
    }

    private sealed class GeocodingResult
    {
        [JsonPropertyName("id")] public int Id { get; init; }
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("latitude")] public double Latitude { get; init; }
        [JsonPropertyName("longitude")] public double Longitude { get; init; }
        [JsonPropertyName("country")] public string Country { get; init; } = string.Empty;
        [JsonPropertyName("admin1")] public string? Admin1 { get; init; } // region — may be absent
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("error")] public bool Error { get; init; }
        [JsonPropertyName("reason")] public string? Reason { get; init; }
        [JsonPropertyName("current")] public CurrentDto? Current { get; init; }
        [JsonPropertyName("current_units")] public CurrentUnitsDto? CurrentUnits { get; init; }
        [JsonPropertyName("hourly")] public HourlyDto? Hourly { get; init; }
        [JsonPropertyName("hourly_units")] public HourlyUnitsDto? HourlyUnits { get; init; }
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
        [JsonPropertyName("temperature_2m")] public double?[]? Temperature2m { get; init; }
        [JsonPropertyName("weather_code")] public int?[]? WeatherCode { get; init; }
        [JsonPropertyName("precipitation_probability")] public int?[]? PrecipitationProbability { get; init; }
        [JsonPropertyName("is_day")] public int?[]? IsDay { get; init; }
    }

    private sealed class HourlyUnitsDto
    {
        [JsonPropertyName("temperature_2m")] public string? Temperature2m { get; init; }
        [JsonPropertyName("precipitation_probability")] public string? PrecipitationProbability { get; init; }
    }
}
