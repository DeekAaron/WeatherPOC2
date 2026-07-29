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

    // The VM takes the loader + history instead of setting ILoadedLocation itself (Spec D1/D4), plus the
    // IFavouritesService for the Favourites list (Feature 7, #48 D5). All args default so a test names only
    // what it exercises; a self-made IFavouritesService gets an empty Entries so the ctor's RebuildFavourites
    // has a non-null list to iterate.
    private static LocationSearchViewModel VmWith(
        IWeatherGateway? gateway = null,
        ILocationLoader? loader = null,
        SearchHistory? history = null,
        INavigator? navigator = null,
        IFavouritesService? favourites = null,
        ILogger<LocationSearchViewModel>? logger = null)
    {
        if (favourites is null)
        {
            favourites = Substitute.For<IFavouritesService>();
            favourites.Entries.Returns(new List<Location>());
        }
        return new(
            gateway ?? Substitute.For<IWeatherGateway>(),
            loader ?? Substitute.For<ILocationLoader>(),
            history ?? new SearchHistory(),
            navigator ?? Substitute.For<INavigator>(),
            favourites,
            logger ?? NullLogger<LocationSearchViewModel>.Instance);
    }

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
        var vm = VmWith(Substitute.For<IWeatherGateway>(), loader, new SearchHistory(), Substitute.For<INavigator>(), logger: spy);

        var candidate = new SearchCandidate(2643743, "London", "England", "United Kingdom", 12.3456789, 98.7654321);
        await vm.SelectCandidateCommand.ExecuteAsync(candidate);
        await vm.SelectRecentCommand.ExecuteAsync(new Location(12.3456789, 98.7654321, "London, England, United Kingdom", 2643743));

        Assert.DoesNotContain(spy.Messages, m => m.Contains(lat) || m.Contains(lon));
    }

    [Fact]
    public async Task OpenFavourite_loads_via_the_location_loader_then_navigates()
    {
        var loader = Substitute.For<ILocationLoader>();
        var navigator = Substitute.For<INavigator>();
        var vm = VmWith(Substitute.For<IWeatherGateway>(), loader, new SearchHistory(), navigator);
        var fav = new Location(51.5, -0.12, "London, GB", 2643743);

        await vm.OpenFavouriteCommand.ExecuteAsync(fav);

        // Opening a Favourite reuses Feature 6's single load choke point (records history + sets holder +
        // persists), THEN navigates — behaviourally identical to tapping a Recent entry (Spec D1).
        Received.InOrder(() =>
        {
            loader.LoadAsync(fav, Arg.Any<CancellationToken>());
            navigator.GoToCurrentConditionsAsync();
        });
    }

    [Fact]
    public void Favourites_collection_is_empty_when_the_service_has_no_entries_and_rebuilds_on_changed()
    {
        var favourites = Substitute.For<IFavouritesService>();
        favourites.Entries.Returns(new List<Location>());
        var vm = VmWith(favourites: favourites);

        Assert.Empty(vm.Favourites); // empty Favourites -> empty collection (no list section)

        favourites.Entries.Returns(new List<Location>
        {
            new(51.5, -0.12, "London, GB", 2643743),
            new(48.85, 2.35, "Paris, FR", 2988507),
        });
        favourites.Changed += Raise.Event<EventHandler>(favourites, EventArgs.Empty);

        Assert.Equal(new[] { "London, GB", "Paris, FR" }, vm.Favourites.Select(l => l.Label));
    }

    [Fact]
    public async Task OpenFavourite_does_not_call_the_gateway()
    {
        // The load path never re-searches Open-Meteo, and (by construction) the VM holds no
        // ILoadedLocation — the loader owns the holder, so the VM cannot set it directly (Spec D1).
        var gateway = Substitute.For<IWeatherGateway>();
        var vm = VmWith(gateway, Substitute.For<ILocationLoader>(), new SearchHistory(), Substitute.For<INavigator>());

        await vm.OpenFavouriteCommand.ExecuteAsync(new Location(51.5, -0.12, "London, GB", 2643743));

        await gateway.DidNotReceive().GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());
        await gateway.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
    public void Dispose_detaches_the_favourites_changed_subscription_so_favourites_no_longer_rebuilds()
    {
        var favourites = Substitute.For<IFavouritesService>();
        favourites.Entries.Returns(new List<Location> { new(51.5, -0.12, "London, GB", 2643743) });
        var vm = VmWith(favourites: favourites);

        Assert.Equal(new[] { "London, GB" }, vm.Favourites.Select(l => l.Label).ToArray());

        vm.Dispose();

        // After Dispose the singleton service no longer roots this transient VM: a later Changed raise
        // must NOT rebuild the disposed VM's Favourites collection.
        favourites.Entries.Returns(new List<Location>
        {
            new(51.5, -0.12, "London, GB", 2643743),
            new(48.85, 2.35, "Paris, FR", 2988507),
        });
        favourites.Changed += Raise.Event<EventHandler>(favourites, EventArgs.Empty);

        Assert.Equal(new[] { "London, GB" }, vm.Favourites.Select(l => l.Label).ToArray());
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
