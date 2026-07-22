using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.DependencyInjection;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddWeatherPoc2Core_resolves_the_view_model_and_its_gateway_graph()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWeatherPoc2Core();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var vm = provider.GetRequiredService<CurrentConditionsViewModel>();
        var gateway = provider.GetRequiredService<IWeatherGateway>();

        Assert.NotNull(vm);
        Assert.IsType<OpenMeteoGateway>(gateway);
    }

    [Fact]
    public void Named_open_meteo_client_has_a_15_second_timeout()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWeatherPoc2Core();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(OpenMeteoGateway.HttpClientName);

        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }

    [Fact]
    public void Named_open_meteo_client_caps_the_response_buffer_at_one_megabyte()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWeatherPoc2Core();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(OpenMeteoGateway.HttpClientName);

        Assert.Equal(1_048_576, client.MaxResponseContentBufferSize);
    }
}
