using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WeatherPoc2.Core.DependencyInjection;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class ServiceRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<INavigator>()); // supplied by the MAUI head in production
        services.AddWeatherPoc2Core();
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddWeatherPoc2Core_resolves_the_view_model_and_its_gateway_graph()
    {
        using var provider = BuildProvider();

        var vm = provider.GetRequiredService<CurrentConditionsViewModel>();
        var gateway = provider.GetRequiredService<IWeatherGateway>();

        Assert.NotNull(vm);
        Assert.IsType<OpenMeteoGateway>(gateway);
    }

    [Fact]
    public void AddWeatherPoc2Core_resolves_the_location_search_view_model()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetRequiredService<LocationSearchViewModel>());
    }

    [Fact]
    public void AddWeatherPoc2Core_registers_the_weather_condition_mapper()
    {
        // Preserved from Feature 2: the mapper stays registered and injected into CurrentConditionsViewModel.
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetRequiredService<WeatherConditionMapper>());
        Assert.NotNull(provider.GetRequiredService<CurrentConditionsViewModel>()); // resolves with the mapper injected
    }

    [Fact]
    public void Loaded_location_is_a_singleton()
    {
        using var provider = BuildProvider();
        var a = provider.GetRequiredService<ILoadedLocation>();
        var b = provider.GetRequiredService<ILoadedLocation>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Named_open_meteo_client_has_a_15_second_timeout()
    {
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(OpenMeteoGateway.HttpClientName);

        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
    }

    [Fact]
    public void Named_open_meteo_client_caps_the_response_buffer_at_one_megabyte()
    {
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(OpenMeteoGateway.HttpClientName);

        Assert.Equal(1_048_576, client.MaxResponseContentBufferSize);
    }
}
