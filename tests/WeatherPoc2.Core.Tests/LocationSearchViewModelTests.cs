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

    private static LocationSearchViewModel VmWith(
        IWeatherGateway gateway, ILoadedLocation loaded, INavigator navigator)
        => new(gateway, loaded, navigator, NullLogger<LocationSearchViewModel>.Instance);

    [Fact]
    public async Task Search_populates_candidates_and_clears_messages_on_hits()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.SearchAsync("London", Arg.Any<CancellationToken>())
               .Returns(new[] { LondonGb } as IReadOnlyList<SearchCandidate>);
        var vm = VmWith(gateway, new LoadedLocation(), Substitute.For<INavigator>());
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
        var vm = VmWith(gateway, new LoadedLocation(), Substitute.For<INavigator>());
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
        var vm = VmWith(gateway, new LoadedLocation(), Substitute.For<INavigator>());
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
        var vm = VmWith(gateway, new LoadedLocation(), Substitute.For<INavigator>());
        vm.Query = "London";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Candidates);
        Assert.Null(vm.StatusMessage);
        Assert.Equal("Couldn't reach the search service — check your connection and try again.", vm.ErrorMessage);
    }

    [Fact]
    public async Task SelectCandidate_mints_the_location_sets_the_holder_then_navigates()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        var loaded = Substitute.For<ILoadedLocation>();   // substitute so ordering can be red-guarded
        var navigator = Substitute.For<INavigator>();
        var vm = VmWith(gateway, loaded, navigator);

        // Capture the minted Location at call time (robust across NSubstitute versions).
        Location? minted = null;
        loaded.When(l => l.Set(Arg.Any<Location>())).Do(ci => minted = ci.Arg<Location>());

        await vm.SelectCandidateCommand.ExecuteAsync(LondonGb);

        // The minted Location carries the picked Candidate's coordinates, label, and Open-Meteo id.
        loaded.Received(1).Set(Arg.Any<Location>());
        Assert.NotNull(minted);
        Assert.Equal(51.50853, minted!.Latitude);
        Assert.Equal(-0.12574, minted.Longitude);
        Assert.Equal("London, England, United Kingdom", minted.Label);
        Assert.Equal(2643743, minted.OpenMeteoId);

        // Seam 2 ordering contract: Set MUST run BEFORE navigation (else Current Conditions reads a null
        // Current on appearing). Received.InOrder red-guards the ordering itself, not just the end-state —
        // a navigate-before-Set reorder makes this test fail.
        Received.InOrder(() =>
        {
            loaded.Set(Arg.Any<Location>());
            navigator.GoToCurrentConditionsAsync();
        });
    }
}
