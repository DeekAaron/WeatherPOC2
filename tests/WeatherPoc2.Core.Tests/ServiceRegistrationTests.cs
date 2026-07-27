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
        // The display ViewModels now transitively depend on IAppDataPathProvider (via IUnitsService ->
        // UnitsService -> IPersistenceStore -> JsonPersistenceStore), which AddWeatherPoc2Core deliberately
        // does NOT register (host-supplied). Supply the fake here, exactly as MauiProgram supplies the MAUI one.
        services.AddSingleton<WeatherPoc2.Core.Persistence.IAppDataPathProvider>(new FakePathProvider());
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
    public void AddWeatherPoc2Core_resolves_the_weather_view_model_graph()
    {
        // BuildProvider supplies the INavigator the MAUI head owns — the coordinator now depends on it.
        using var provider = BuildProvider();

        var vm = provider.GetRequiredService<WeatherViewModel>();
        Assert.NotNull(vm);
        Assert.NotNull(vm.CurrentConditions);
        Assert.NotNull(vm.HourlyForecast);
        Assert.NotNull(provider.GetRequiredService<HourlyWindow>());
    }

    [Fact]
    public void AddWeatherPoc2Core_resolves_the_location_search_view_model()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetRequiredService<LocationSearchViewModel>());
    }

    [Fact]
    public void SearchHistory_is_registered_as_a_singleton()
    {
        using var provider = BuildProvider();
        var a = provider.GetRequiredService<SearchHistory>();
        var b = provider.GetRequiredService<SearchHistory>();
        Assert.Same(a, b);
    }

    [Fact]
    public void LocationLoader_resolves_as_ILocationLoader_singleton()
    {
        using var provider = BuildProvider();
        var a = provider.GetRequiredService<ILocationLoader>();
        var b = provider.GetRequiredService<ILocationLoader>();
        Assert.Same(a, b);
        Assert.IsType<LocationLoader>(a);
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

    [Fact]
    public void AddWeatherPoc2Core_resolves_the_units_graph_when_a_path_provider_is_supplied()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<WeatherPoc2.Core.Persistence.IAppDataPathProvider>(
            new FakePathProvider()); // the host (MauiProgram) supplies the MAUI one
        services.AddWeatherPoc2Core();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<WeatherPoc2.Core.Persistence.IPersistenceStore>());
        Assert.NotNull(provider.GetRequiredService<WeatherPoc2.Core.Units.IUnitsService>());
        Assert.NotNull(provider.GetRequiredService<WeatherPoc2.Core.Units.UnitFormatter>());
        Assert.NotNull(provider.GetRequiredService<WeatherPoc2.Core.ViewModels.SettingsViewModel>());
    }

    [Fact]
    public void The_settings_view_model_is_transient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<WeatherPoc2.Core.Persistence.IAppDataPathProvider>(new FakePathProvider());
        services.AddWeatherPoc2Core();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotSame(
            provider.GetRequiredService<WeatherPoc2.Core.ViewModels.SettingsViewModel>(),
            provider.GetRequiredService<WeatherPoc2.Core.ViewModels.SettingsViewModel>());
    }

    [Fact]
    public void The_units_service_is_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<WeatherPoc2.Core.Persistence.IAppDataPathProvider>(new FakePathProvider());
        services.AddWeatherPoc2Core();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Same(
            provider.GetRequiredService<WeatherPoc2.Core.Units.IUnitsService>(),
            provider.GetRequiredService<WeatherPoc2.Core.Units.IUnitsService>());
    }

    private sealed class FakePathProvider : WeatherPoc2.Core.Persistence.IAppDataPathProvider
    {
        public string GetAppDataDirectory() => Path.Combine(Path.GetTempPath(), "weatherpoc2-di-" + Guid.NewGuid().ToString("N"));
    }
}
