using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class LocationSearchViewModelTests
{
    private static SearchCandidate LondonGb =>
        new(2643743, "London", "England", "United Kingdom", 51.50853, -0.12574);

    // The VM now takes the loader + history instead of setting ILoadedLocation itself (Spec D1/D4).
    private static LocationSearchViewModel VmWith(
        IWeatherGateway gateway,
        ILocationLoader loader,
        SearchHistory history,
        INavigator navigator,
        ILogger<LocationSearchViewModel>? logger = null)
        => new(gateway, loader, history, navigator, logger ?? NullLogger<LocationSearchViewModel>.Instance);

    [Fact]
    public async Task Search_populates_candidates_and_clears_messages_on_hits()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.SearchAsync("London", Arg.Any<CancellationToken>())
               .Returns(new[] { LondonGb } as IReadOnlyList<SearchCandidate>);
        var vm = VmWith(gateway, Substitute.For<ILocationLoader>(), new SearchHistory(), Substitute.For<INavigator>());
        vm.Query = "London";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Single(vm.Candidates);
        Assert.Null(vm.StatusMessage);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Search_does_not_call_the_gateway_for_a_blank_query()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        var vm = VmWith(gateway, Substitute.For<ILocationLoader>(), new SearchHistory(), Substitute.For<INavigator>());
        vm.Query = "   ";

        await vm.SearchCommand.ExecuteAsync(null);

        await gateway.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_shows_no_matches_message_and_keeps_screen_on_empty_result()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns(Array.Empty<SearchCandidate>() as IReadOnlyList<SearchCandidate>);
        var vm = VmWith(gateway, Substitute.For<ILocationLoader>(), new SearchHistory(), Substitute.For<INavigator>());
        vm.Query = "zzxqwplkjhg";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Candidates);
        Assert.Equal("No matching places found", vm.StatusMessage);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Search_shows_friendly_error_on_transport_failure()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
               .Returns<IReadOnlyList<SearchCandidate>>(_ => throw new LocationSearchUnavailableException("boom"));
        var vm = VmWith(gateway, Substitute.For<ILocationLoader>(), new SearchHistory(), Substitute.For<INavigator>());
        vm.Query = "London";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Candidates);
        Assert.Null(vm.StatusMessage);
        Assert.Equal("Couldn't reach the search service — check your connection and try again.", vm.ErrorMessage);
    }

    [Fact]
    public async Task SelectCandidate_loads_via_the_coordinator_then_navigates()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        var loader = Substitute.For<ILocationLoader>();
        var navigator = Substitute.For<INavigator>();
        var vm = VmWith(gateway, loader, new SearchHistory(), navigator);

        // Capture the minted Location at call time (robust across NSubstitute versions).
        Location? loadedArg = null;
        loader.When(l => l.LoadAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>()))
              .Do(ci => loadedArg = ci.Arg<Location>());

        await vm.SelectCandidateCommand.ExecuteAsync(LondonGb);

        // The minted Location carries the picked Candidate's coordinates, label, and Open-Meteo id.
        Assert.NotNull(loadedArg);
        Assert.Equal(51.50853, loadedArg!.Latitude);
        Assert.Equal(-0.12574, loadedArg.Longitude);
        Assert.Equal("London, England, United Kingdom", loadedArg.Label);
        Assert.Equal(2643743, loadedArg.OpenMeteoId);

        // The VM no longer sets ILoadedLocation directly — the coordinator owns that now (Spec D1).
        await loader.Received(1).LoadAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());

        // load-ordering clause: LoadAsync (record + set holder + persist) MUST complete before navigation.
        Received.InOrder(() =>
        {
            loader.LoadAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());
            navigator.GoToCurrentConditionsAsync();
        });
    }

    [Fact]
    public async Task SelectRecent_loads_via_the_coordinator_then_navigates_without_a_gateway_call()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        var loader = Substitute.For<ILocationLoader>();
        var navigator = Substitute.For<INavigator>();
        var vm = VmWith(gateway, loader, new SearchHistory(), navigator);
        var recent = new Location(51.50853, -0.12574, "London, England, United Kingdom", 2643743);

        await vm.SelectRecentCommand.ExecuteAsync(recent);

        // Tapping a Recent entry is a load like any other — same coordinator, no gateway search.
        await loader.Received(1).LoadAsync(recent, Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            loader.LoadAsync(recent, Arg.Any<CancellationToken>());
            navigator.GoToCurrentConditionsAsync();
        });
    }

    [Fact]
    public void Recent_reflects_history_entries_most_recent_first_and_rebuilds_on_change()
    {
        var history = new SearchHistory();
        var vm = VmWith(Substitute.For<IWeatherGateway>(), Substitute.For<ILocationLoader>(), history, Substitute.For<INavigator>());

        Assert.Empty(vm.Recent); // empty history -> empty Recent

        history.Record(new Location(0, 0, "a", 1));
        history.Record(new Location(0, 0, "b", 2));

        Assert.Equal(new[] { "b", "a" }, vm.Recent.Select(l => l.Label).ToArray());
    }

    [Fact]
    public void Recent_reflects_a_history_already_populated_at_construction()
    {
        var history = new SearchHistory();
        history.Record(new Location(0, 0, "a", 1));
        history.Record(new Location(0, 0, "b", 2));

        var vm = VmWith(Substitute.For<IWeatherGateway>(), Substitute.For<ILocationLoader>(), history, Substitute.For<INavigator>());

        Assert.Equal(new[] { "b", "a" }, vm.Recent.Select(l => l.Label).ToArray());
    }

    [Fact]
    public async Task No_coordinate_of_a_loaded_location_leaks_into_the_view_model_logs()
    {
        // Security AC (data-exposure): a Location's Latitude/Longitude must NEVER appear in any log the
        // VM emits on the load paths — at most the Label may (mirrors the OpenMeteoGateway control).
        // Distinctive coordinates so any accidental interpolation is unmistakable in the captured text.
        const string lat = "12.3456789";
        const string lon = "98.7654321";
        var spy = new CapturingLogger();
        var loader = Substitute.For<ILocationLoader>();
        var vm = VmWith(Substitute.For<IWeatherGateway>(), loader, new SearchHistory(), Substitute.For<INavigator>(), spy);

        var candidate = new SearchCandidate(2643743, "London", "England", "United Kingdom", 12.3456789, 98.7654321);
        await vm.SelectCandidateCommand.ExecuteAsync(candidate);
        await vm.SelectRecentCommand.ExecuteAsync(new Location(12.3456789, 98.7654321, "London, England, United Kingdom", 2643743));

        Assert.DoesNotContain(spy.Messages, m => m.Contains(lat) || m.Contains(lon));
    }

    [Fact]
    public void Dispose_detaches_the_history_changed_subscription_so_recent_no_longer_rebuilds()
    {
        var history = new SearchHistory();
        history.Record(new Location(0, 0, "a", 1));
        var vm = VmWith(Substitute.For<IWeatherGateway>(), Substitute.For<ILocationLoader>(), history, Substitute.For<INavigator>());

        Assert.Equal(new[] { "a" }, vm.Recent.Select(l => l.Label).ToArray());

        vm.Dispose();

        // After Dispose the singleton history no longer roots this transient VM: a later Changed raise
        // (a fresh load recorded) must NOT rebuild the disposed VM's Recent collection.
        history.Record(new Location(0, 0, "b", 2));

        Assert.Equal(new[] { "a" }, vm.Recent.Select(l => l.Label).ToArray());
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var vm = VmWith(Substitute.For<IWeatherGateway>(), Substitute.For<ILocationLoader>(), new SearchHistory(), Substitute.For<INavigator>());

        vm.Dispose();
        vm.Dispose(); // second call must not throw
    }

    private sealed class CapturingLogger : ILogger<LocationSearchViewModel>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
