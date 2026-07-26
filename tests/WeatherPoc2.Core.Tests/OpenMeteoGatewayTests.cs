using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class OpenMeteoGatewayTests
{
    private static string LoadFixture(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static OpenMeteoGateway GatewayWith(HttpMessageHandler handler)
        => GatewayWith(handler, NullLogger<OpenMeteoGateway>.Instance);

    private static OpenMeteoGateway GatewayWith(HttpMessageHandler handler, ILogger<OpenMeteoGateway> logger)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));
        return new OpenMeteoGateway(factory, logger);
    }

    // A Gateway whose HttpClient caps the buffered response body at 1 MiB (1,048,576 bytes) — the
    // same cap the production named client carries. Constructing HttpClient directly around the stub
    // handler is sanctioned test idiom (known-issue A5); the IHttpClientFactory rule is a production
    // constraint, and the ViewModel + DI story wires the identical cap on the named client.
    private const long OneMebibyte = 1_048_576;

    private static OpenMeteoGateway GatewayWithCappedClient(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
               .Returns(_ => new HttpClient(handler) { MaxResponseContentBufferSize = OneMebibyte });
        return new OpenMeteoGateway(factory, NullLogger<OpenMeteoGateway>.Instance);
    }

    [Fact]
    public async Task GetWeatherAsync_maps_current_temperature_from_a_200_body()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-temperature-london-200.json"));
        var gateway = GatewayWith(handler);

        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.Equal(23.3, bundle.CurrentTemperatureCelsius);
    }

    [Fact]
    public async Task GetWeatherAsync_requests_current_temperature_in_celsius_explicitly()
    {
        // AC #5: the request URL must ask Open-Meteo for the current temperature in Celsius
        // *explicitly* — not rely on the API default. Assert the outgoing request URI carries
        // both the current=temperature_2m selection and the temperature_unit=celsius clause.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-temperature-london-200.json"));
        var gateway = GatewayWith(handler);

        await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        var requestUrl = handler.LastRequest?.RequestUri?.ToString();
        Assert.NotNull(requestUrl);
        Assert.Contains("current=temperature_2m", requestUrl);
        Assert.Contains("temperature_unit=celsius", requestUrl);
    }

    [Fact]
    public async Task GetWeatherAsync_logs_the_endpoint_and_outcome_for_Current_Conditions()
    {
        // AC #7: the Instrumentation contract requires the Gateway to log the endpoint (URL) on the
        // request line and the endpoint (URL) + outcome (status) on every outcome line, via
        // ILogger<OpenMeteoGateway>. Assert both lines are emitted with the endpoint present.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-temperature-london-200.json"));
        var logger = new CapturingLogger<OpenMeteoGateway>();
        var gateway = GatewayWith(handler, logger);

        await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        const string endpoint = "https://api.open-meteo.com/v1/forecast";

        var requestLine = Assert.Single(logger.Messages, m => m.Contains(endpoint) && m.Contains("requesting"));
        Assert.Contains(Location.LondonGb.Label, requestLine);

        var outcomeLine = Assert.Single(
            logger.Messages,
            m => m.Contains(endpoint) && m.Contains(((int)HttpStatusCode.OK).ToString()));
        Assert.Contains(Location.LondonGb.Label, outcomeLine);
    }

    [Fact]
    public async Task GetWeatherAsync_converts_a_malformed_200_body_to_WeatherUnavailableException()
    {
        // A 200 with an unparseable body must surface as the typed failure the app layer catches,
        // not a raw JsonException. (Malformed-field conversion is in scope for this story; the
        // transport / non-200 / error:true / unit-mismatch branches are deferred to the failure-paths story.)
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{ this is not valid json");
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_converts_a_non_numeric_temperature_to_WeatherUnavailableException()
    {
        // temperature_2m present but not a number must also convert to the typed failure.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"current\":{\"temperature_2m\":\"n/a\"}}");
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_converts_a_transport_failure_to_WeatherUnavailableException()
    {
        // A network/DNS failure surfaces as HttpRequestException on the transport call; the Gateway
        // must convert it into the single typed failure the app layer catches (fail-visible).
        var handler = new StubHttpMessageHandler(new HttpRequestException("network down"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_converts_a_request_timeout_to_WeatherUnavailableException()
    {
        // Security AC: a request-timeout expiry surfaces as TaskCanceledException — the timeout half
        // of the fail-closed promise. It must convert to WeatherUnavailableException, not hang or
        // escape untyped, so a slow/cancelled Open-Meteo response never stalls the load.
        var handler = new StubHttpMessageHandler(new TaskCanceledException("timed out"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_converts_an_oversized_response_to_WeatherUnavailableException()
    {
        // Security AC: a 200 body larger than the 1 MiB buffer cap must never make the app buffer an
        // unbounded body. Reading past MaxResponseContentBufferSize surfaces as HttpRequestException,
        // which converts through the existing transport catch (no new Gateway branch).
        var oversizedBody = new string('x', (int)(OneMebibyte + 1024));
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, oversizedBody);
        var gateway = GatewayWithCappedClient(handler);

        var ex = await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));

        // The over-cap read must surface as HttpRequestException through the transport catch —
        // not as an incidental JSON-parse failure on the body — so assert the wrapped cause.
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task GetWeatherAsync_converts_an_error_true_body_and_logs_the_reason_at_Error()
    {
        // The live 400 fixture carries {"error":true,"reason":…}. The Gateway must convert it to the
        // typed failure AND log the API's reason at Error before throwing (fail-visible; the reason is
        // diagnostic, logged not shown). The error:true branch owns this — not the missing-temp guard.
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, LoadFixture("error-latitude-out-of-range-400.json"));
        var logger = new CapturingLogger<OpenMeteoGateway>();
        var gateway = GatewayWith(handler, logger);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));

        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Error && e.Message.Contains("Latitude must be in range"));
    }

    [Fact]
    public async Task GetWeatherAsync_converts_a_non_200_status_with_a_wellformed_body_to_WeatherUnavailableException()
    {
        // A non-200 status carrying a WELL-FORMED body (temperature_2m present, °C) must be rejected
        // by the status guard — a valid body ensures the test reaches the status check rather than
        // tripping the JsonException path first. The number in the body must never be mapped through.
        var handler = new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable,
            "{\"current_units\":{\"temperature_2m\":\"°C\"},\"current\":{\"temperature_2m\":23.3}}");
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_converts_a_200_body_missing_temperature_2m_to_WeatherUnavailableException()
    {
        // A 200 body whose `current` object is present but carries no temperature_2m at all
        // (live-shaped fixture) must convert to the typed failure — never a fabricated number.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("malformed-missing-temperature.json"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_throws_when_the_unit_is_not_celsius()
    {
        // A well-formed 200 body whose current_units.temperature_2m is "°F" must be rejected by the
        // unit assertion — the °C guarantee is proven on the wire, never assumed. The plausible-but-
        // wrong 73.9 (Fahrenheit) is never mapped through as a Celsius number.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("wrong-unit-fahrenheit-200.json"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    // ---- Feature 2: the widened Current Conditions bundle (Story #55, Plan Task 3) ----

    [Fact]
    public async Task GetWeatherAsync_maps_the_full_current_conditions_from_a_200_body()
    {
        // The widened bundle carries every Current Conditions measure: Temperature and Wind Speed in
        // canonical units, the current hour's Chance of Rain (from the matched hourly entry), plus the
        // raw weather_code / is_day hints the mapper resolves downstream.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-conditions-london-200.json"));
        var gateway = GatewayWith(handler);

        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.Equal(26.5, bundle.CurrentTemperatureCelsius);
        Assert.Equal(12.6, bundle.CurrentWindSpeedKmh);
        Assert.Equal(0, bundle.CurrentChanceOfRainPercent);   // current hour 17:00's precip probability
        Assert.Equal(3, bundle.CurrentWeatherCode);
        Assert.True(bundle.IsDay);
    }

    [Fact]
    public async Task GetWeatherAsync_requests_the_new_current_and_hourly_fields()
    {
        // The widened Feature-4 request adds timezone=auto, forecast_days=2, and the full hourly series
        // (temperature_2m,weather_code,precipitation_probability,is_day), keeping the current fields and
        // pinning BOTH canonical units explicitly (never rely on API defaults).
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-conditions-london-200.json"));
        var gateway = GatewayWith(handler);

        await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        // Security AC — transport confidentiality: the request must be issued over HTTPS. A base-URL
        // regression to http fails here, pinning coordinate confidentiality + payload integrity in transit.
        Assert.Equal("https", handler.LastRequest?.RequestUri?.Scheme);

        var url = handler.LastRequest?.RequestUri?.ToString();
        Assert.NotNull(url);
        Assert.StartsWith("https://", url);
        Assert.Contains("current=temperature_2m,wind_speed_10m,weather_code,is_day", url);
        Assert.Contains("hourly=temperature_2m,weather_code,precipitation_probability,is_day", url);
        Assert.Contains("timezone=auto", url);
        Assert.Contains("forecast_days=2", url);
        Assert.Contains("wind_speed_unit=kmh", url);
        Assert.Contains("temperature_unit=celsius", url);
    }

    [Fact]
    public async Task GetWeatherAsync_matches_the_current_hour_by_truncating_minutes()
    {
        // Chance of Rain is an hourly measure (Context.MD): current.time "…T17:30" must match the
        // top-of-hour hourly entry "…T17:00" — not 16:00's 5 or 18:00's 10.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-conditions-london-200.json"));
        var gateway = GatewayWith(handler);

        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.Equal(0, bundle.CurrentChanceOfRainPercent);
    }

    // ---- Feature 2: strict failure paths for the new fields (Story #55, Plan Task 4) ----

    [Fact]
    public async Task GetWeatherAsync_throws_when_wind_unit_is_not_kmh()
    {
        // Belt-and-suspenders, mirrors F1's °C assertion: the km/h guarantee is proven on the wire.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("wrong-wind-unit-200.json"));
        var gateway = GatewayWith(handler);
        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_throws_when_wind_speed_is_missing()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("missing-wind-200.json"));
        var gateway = GatewayWith(handler);
        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_throws_when_current_hour_precipitation_is_null()
    {
        // Chance of Rain is a strict measure: the current hour is present but its probability is null.
        // (0 is a valid probability, never a fallback — null must fail closed.)
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("precip-null-200.json"));
        var gateway = GatewayWith(handler);
        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_throws_when_current_hour_is_not_in_the_hourly_series()
    {
        // current.time's top-of-hour is absent from hourly.time[] — no probability to read, fail closed.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("hour-unmatched-200.json"));
        var gateway = GatewayWith(handler);
        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_throws_when_hourly_arrays_are_mismatched_in_length()
    {
        // Security AC: hourly.time[] is longer than precipitation_probability[], so the matched
        // current-hour index (2) falls at/beyond the probability array's length (2). The Gateway must
        // fail closed with WeatherUnavailableException — never an unhandled IndexOutOfRangeException on
        // an untrusted, degenerate Open-Meteo response.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("hourly-arrays-mismatched-200.json"));
        var gateway = GatewayWith(handler);
        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_still_succeeds_when_weather_code_and_is_day_are_absent()
    {
        // Lenient: absent icon-only hints do NOT fail the fetch (they resolve to Unknown / day downstream);
        // the strict numeric measures are still parsed.
        const string body = "{\"current_units\":{\"temperature_2m\":\"°C\",\"wind_speed_10m\":\"km/h\"}," +
            "\"current\":{\"time\":\"2026-07-22T17:00\",\"temperature_2m\":26.5,\"wind_speed_10m\":12.6}," +
            "\"hourly_units\":{\"temperature_2m\":\"°C\",\"precipitation_probability\":\"%\"}," +
            "\"hourly\":{\"time\":[\"2026-07-22T17:00\"],\"temperature_2m\":[18.5],\"weather_code\":[3]," +
            "\"precipitation_probability\":[0],\"is_day\":[1]}}";
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
        var gateway = GatewayWith(handler);

        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.Null(bundle.CurrentWeatherCode);
        Assert.Null(bundle.IsDay);
        Assert.Equal(26.5, bundle.CurrentTemperatureCelsius);
    }

    // ---- Feature 4: the widened timezone=auto hourly series (Story #69, Plan Task 3) ----

    [Fact]
    public async Task GetWeatherAsync_projects_the_full_hourly_series_and_local_now()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-conditions-london-200.json"));
        var gateway = GatewayWith(handler);

        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.Equal(new DateTime(2026, 7, 22, 17, 30, 0, DateTimeKind.Unspecified), bundle.LocalNow);
        Assert.Equal(3, bundle.Hourly.Count);
        Assert.Equal(new DateTime(2026, 7, 22, 16, 0, 0, DateTimeKind.Unspecified), bundle.Hourly[0].LocalTime);
        Assert.Equal(19.0, bundle.Hourly[0].TemperatureCelsius);
        Assert.Equal(3, bundle.Hourly[0].WeatherCode);
        Assert.True(bundle.Hourly[0].IsDay);
        Assert.Equal(5, bundle.Hourly[0].ChanceOfRainPercent);
        Assert.Equal(2, bundle.Hourly[2].WeatherCode);               // 18:00 partly cloudy
    }

    [Fact]
    public async Task GetWeatherAsync_fails_closed_when_a_new_hourly_array_is_shorter_than_time()
    {
        // Seam 1 (mismatched-length branch): temperature_2m[] shorter than time[] must fail closed
        // — never IndexOutOfRange.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("hourly-temp-array-short-200.json"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_fails_closed_when_a_new_hourly_array_is_missing()
    {
        // Seam 1 (missing-array branch, distinct from the length branch above): the weather_code[]
        // array is absent entirely from hourly. The presence check must fail closed —
        // never NullReferenceException / IndexOutOfRangeException.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("hourly-weather-code-array-missing-200.json"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_fails_closed_when_hourly_units_are_not_canonical()
    {
        // Seam 1 (data-format, negative): hourly_units.temperature_2m is °F, not °C. The hourly-unit
        // pin must fail closed — makes the °C / % contract falsifiable, not transitively assumed.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("wrong-hourly-unit-fahrenheit-200.json"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<WeatherUnavailableException>(
            () => gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None));
    }

    [Fact]
    public async Task GetWeatherAsync_soft_passes_a_null_hourly_element_in_a_non_current_hour()
    {
        // Seam 1 (soft-passthrough, at the Gateway boundary): the current hour (17:00) has a real
        // precip (0, so the F2 current-hour chance step succeeds), while a later windowed hour (18:00)
        // has precipitation_probability null. The fetch must SUCCEED and project the null element as a
        // null ChanceOfRainPercent — never fail closed. Distinct from the current-hour strict-null case.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("hourly-null-precip-windowed-200.json"));
        var gateway = GatewayWith(handler);

        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.Equal(3, bundle.Hourly.Count);
        Assert.Equal(0, bundle.Hourly[1].ChanceOfRainPercent);   // 17:00 current hour: real 0
        Assert.Null(bundle.Hourly[2].ChanceOfRainPercent);       // 18:00 windowed hour: null soft-passed
        Assert.Equal(18.0, bundle.Hourly[2].TemperatureCelsius); // other fields on the null-precip hour intact
        Assert.Equal(2, bundle.Hourly[2].WeatherCode);
    }

    [Fact]
    public async Task GetWeatherAsync_projects_the_committed_live_timezone_auto_capture()
    {
        // Seam 1 (d) external-seam proof: parse the REAL captured Open-Meteo timezone=auto payload
        // (openmeteo-tzauto.json, London 2026-07-26) — the genuine wire shape from the real writer,
        // not a hand-authored sample. Asserts the full projection + LocalNow over the real 48-length
        // series, and that every projected timestamp is offset-less local wall-clock (Kind=Unspecified).
        // Exact per-hour values are read from the committed capture (temp 20.7, weather_code 2 at 00:00).
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("openmeteo-tzauto.json"));
        var gateway = GatewayWith(handler);

        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.Equal(new DateTime(2026, 7, 26, 13, 30, 0, DateTimeKind.Unspecified), bundle.LocalNow);
        Assert.Equal(DateTimeKind.Unspecified, bundle.LocalNow.Kind);
        Assert.Equal(48, bundle.Hourly.Count);
        Assert.Equal(new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Unspecified), bundle.Hourly[0].LocalTime);
        Assert.Equal(20.7, bundle.Hourly[0].TemperatureCelsius);
        Assert.Equal(2, bundle.Hourly[0].WeatherCode);
        Assert.Equal(0, bundle.Hourly[0].ChanceOfRainPercent);   // 0 is a real value, not absence
        Assert.False(bundle.Hourly[0].IsDay);                    // midnight -> is_day 0
        Assert.All(bundle.Hourly, p => Assert.Equal(DateTimeKind.Unspecified, p.LocalTime.Kind));
    }

    [Fact]
    public async Task GetWeatherAsync_parses_local_timestamps_invariant_to_culture_and_device_timezone()
    {
        // Seam 2: force a non-invariant culture whose default date formatting differs (fr-FR).
        // The offset-less local strings must still parse to the exact Kind=Unspecified wall clock,
        // and the window must be identical — no device-locale or device-timezone shift. The
        // Kind=Unspecified invariant on EVERY parsed timestamp is the device-timezone-independence
        // proof (a shift would produce a Local/Utc kind or a different value — both asserted against).
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-conditions-london-200.json"));
            var gateway = GatewayWith(handler);

            var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

            var expectedNow = new DateTime(2026, 7, 22, 17, 30, 0, DateTimeKind.Unspecified);
            Assert.Equal(expectedNow, bundle.LocalNow);
            Assert.Equal(DateTimeKind.Unspecified, bundle.LocalNow.Kind);
            Assert.Equal(new DateTime(2026, 7, 22, 16, 0, 0, DateTimeKind.Unspecified), bundle.Hourly[0].LocalTime);
            Assert.Equal(DateTimeKind.Unspecified, bundle.Hourly[0].LocalTime.Kind);
            Assert.All(bundle.Hourly, p => Assert.Equal(DateTimeKind.Unspecified, p.LocalTime.Kind));

            // And the pure window computed from these parsed values is stable under the flipped culture.
            var window = new HourlyWindow().Compute(bundle.Hourly, bundle.LocalNow);
            Assert.Equal(new DateTime(2026, 7, 22, 17, 0, 0, DateTimeKind.Unspecified), window[0].LocalTime);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task GetWeatherAsync_local_timestamp_parse_and_window_are_identical_across_cultures()
    {
        // Story #70 (Seam 2 proof, complete): the offset-less local ISO strings must parse to the EXACT
        // same Kind=Unspecified wall-clock values — and yield the byte-identical HourlyWindow — no matter
        // the host CurrentCulture. Parse the same payload once under the invariant culture (baseline) and
        // once under fr-FR (whose default date formatting differs), then assert full equality. This is
        // the PRD "Location whose local time differs from the device's" case proven end-to-end.
        var invariant = await ParseUnderCulture(CultureInfo.InvariantCulture);
        var french = await ParseUnderCulture(new CultureInfo("fr-FR"));

        // AC1: current.time and EVERY hourly timestamp parse to the exact expected wall clock — and to
        // the identical value under the flipped culture (no locale shift on any element).
        var expectedNow = new DateTime(2026, 7, 22, 17, 30, 0, DateTimeKind.Unspecified);
        var expectedHourly = new[]
        {
            new DateTime(2026, 7, 22, 16, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 7, 22, 17, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 7, 22, 18, 0, 0, DateTimeKind.Unspecified),
        };
        Assert.Equal(expectedNow, french.LocalNow);
        Assert.Equal(expectedHourly, french.Hourly.Select(p => p.LocalTime));
        Assert.Equal(invariant.LocalNow, french.LocalNow);
        Assert.Equal(invariant.Hourly.Select(p => p.LocalTime), french.Hourly.Select(p => p.LocalTime));

        // AC2: every parsed timestamp (local-now AND all hourly points) is Kind=Unspecified under the
        // flipped culture — an Unspecified kind is the device-timezone-independence proof (a device-tz
        // shift would have produced a Local/Utc kind or a different value).
        Assert.Equal(DateTimeKind.Unspecified, french.LocalNow.Kind);
        Assert.All(french.Hourly, p => Assert.Equal(DateTimeKind.Unspecified, p.LocalTime.Kind));

        // AC3: the window computed from the parsed values is identical under the flipped culture — the
        // WHOLE ordered slice, compared element-for-element against the invariant-culture baseline.
        var invariantWindow = new HourlyWindow().Compute(invariant.Hourly, invariant.LocalNow);
        var frenchWindow = new HourlyWindow().Compute(french.Hourly, french.LocalNow);
        Assert.Equal(
            invariantWindow.Select(p => p.LocalTime),
            frenchWindow.Select(p => p.LocalTime));
        Assert.Equal(
            new[]
            {
                new DateTime(2026, 7, 22, 17, 0, 0, DateTimeKind.Unspecified),
                new DateTime(2026, 7, 22, 18, 0, 0, DateTimeKind.Unspecified),
            },
            frenchWindow.Select(p => p.LocalTime));

        // AC4 (no ToLocalTime/ToUniversalTime/AssumeLocal/AdjustToUniversal in the parse): proven
        // structurally by the Kind=Unspecified invariant above — any of those conversions would have
        // produced a Local/Utc kind or a shifted value, both of which are asserted against here.
    }

    // Parse the shared London payload with the process CurrentCulture pinned to `culture`, restoring the
    // original afterwards. The Gateway reads CultureInfo.CurrentCulture at parse time, so this is the seam
    // through which a culture-dependent parse would leak into the result if TryParseLocal were not invariant.
    private static async Task<WeatherBundle> ParseUnderCulture(CultureInfo culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-conditions-london-200.json"));
            return await GatewayWith(handler).GetWeatherAsync(Location.LondonGb, CancellationToken.None);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task GetWeatherAsync_never_logs_the_geolocation_coordinates_in_cleartext()
    {
        // Security AC — precise-geolocation not logged: no log line (success OR fail-closed path) may
        // contain the request's latitude/longitude. Endpoint-only logging (BaseUrl + Label) still
        // satisfies the "endpoint + outcome" instrumentation contract. Assert with CapturingLogger:
        // no captured message contains London's coordinate substrings.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("current-conditions-london-200.json"));
        var logger = new CapturingLogger<OpenMeteoGateway>();
        var gateway = GatewayWith(handler, logger);

        await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        var lat = Location.LondonGb.Latitude.ToString(CultureInfo.InvariantCulture);   // "51.5074"
        var lon = Location.LondonGb.Longitude.ToString(CultureInfo.InvariantCulture);  // "-0.1278"
        Assert.All(logger.Messages, m => Assert.DoesNotContain(lat, m));
        Assert.All(logger.Messages, m => Assert.DoesNotContain(lon, m));
        // The endpoint is still logged (scheme+host+path only) — the contract is satisfied, not dropped.
        Assert.Contains(logger.Messages, m => m.Contains("https://api.open-meteo.com/v1/forecast"));
    }
}
