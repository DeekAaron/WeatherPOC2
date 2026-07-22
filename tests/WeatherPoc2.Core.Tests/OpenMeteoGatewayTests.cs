using System.Net;
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
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));
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
}
