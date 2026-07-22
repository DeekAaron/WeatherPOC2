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
}
