using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class OpenMeteoGeocodingTests
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

    [Fact]
    public async Task SearchAsync_maps_candidates_from_a_200_body()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("geocoding-london-200.json"));
        var gateway = GatewayWith(handler);

        var candidates = await gateway.SearchAsync("London", CancellationToken.None);

        Assert.Equal(2, candidates.Count);
        var first = candidates[0];
        Assert.Equal(2643743, first.Id);
        Assert.Equal("London", first.Name);
        Assert.Equal("England", first.Region);
        Assert.Equal("United Kingdom", first.Country);
        Assert.Equal("London, England, United Kingdom", first.Label);
        Assert.Equal(51.50853, first.Latitude);
        Assert.Equal(-0.12574, first.Longitude);
        Assert.Equal("London, Ohio, United States", candidates[1].Label);
    }

    [Fact]
    public async Task SearchAsync_composes_label_without_region_when_admin1_absent()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("geocoding-singapore-admin1-absent-200.json"));
        var gateway = GatewayWith(handler);

        var candidates = await gateway.SearchAsync("Singapore", CancellationToken.None);

        var only = Assert.Single(candidates);
        Assert.Null(only.Region);
        Assert.Equal("Singapore, Singapore", only.Label);
    }

    [Fact]
    public async Task SearchAsync_returns_empty_list_when_results_key_is_absent()
    {
        // No matches: a 200 with the `results` key ABSENT (proven live). Must be an empty list, NOT an
        // exception — this is how the app tells "no such place" from "couldn't reach the service".
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("geocoding-no-match-200.json"));
        var gateway = GatewayWith(handler);

        var candidates = await gateway.SearchAsync("zzxqwplkjhg", CancellationToken.None);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task SearchAsync_converts_a_transport_failure_to_LocationSearchUnavailableException()
    {
        var handler = new StubHttpMessageHandler(new HttpRequestException("network down"));
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<LocationSearchUnavailableException>(
            () => gateway.SearchAsync("London", CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_converts_a_malformed_body_to_LocationSearchUnavailableException()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{ this is not valid json");
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<LocationSearchUnavailableException>(
            () => gateway.SearchAsync("London", CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_converts_a_non_success_status_to_LocationSearchUnavailableException()
    {
        // The Spec/Seam-1 name non-2xx as a distinct failure mode; SearchAsync has a
        // !IsSuccessStatusCode branch. Body is valid JSON so it clears the parse step and the STATUS
        // branch is the one exercised (the Gateway parses before checking status).
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var gateway = GatewayWith(handler);

        await Assert.ThrowsAsync<LocationSearchUnavailableException>(
            () => gateway.SearchAsync("London", CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_requests_geocoding_with_name_count_and_json_format()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("geocoding-london-200.json"));
        var gateway = GatewayWith(handler);

        await gateway.SearchAsync("London", CancellationToken.None);

        var url = handler.LastRequest?.RequestUri?.ToString();
        Assert.NotNull(url);
        Assert.Contains("geocoding-api.open-meteo.com/v1/search", url);
        Assert.Contains("name=London", url);
        Assert.Contains("count=10", url);
        Assert.Contains("format=json", url);
    }

    [Fact]
    public async Task SearchAsync_percent_encodes_reserved_characters_and_keeps_fixed_parameters_fixed()
    {
        // Security regression (query-parameter injection at the geocoding boundary): a crafted query
        // carrying URL-reserved characters must be percent-encoded INSIDE the `name` value only. It can
        // neither inject an extra query parameter nor override the fixed count/format/language values.
        // This locks the Plan's Uri.EscapeDataString(name) encoding so a later refactor cannot drop it.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("geocoding-london-200.json"));
        var gateway = GatewayWith(handler);

        await gateway.SearchAsync("London&count=999", CancellationToken.None);

        var uri = handler.LastRequest?.RequestUri;
        Assert.NotNull(uri);
        var query = uri!.Query; // includes the leading '?'

        // The crafted "count=999" must NOT appear as its own parameter — the '&' was encoded inside name.
        Assert.DoesNotContain("count=999", query);
        Assert.Contains("London%26count%3D999", query);

        // Exactly one fixed pair each, with the expected values (no injection, no override).
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(query, @"(^|[?&])count=10(&|$)"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(query, @"(^|[?&])format=json(&|$)"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(query, @"(^|[?&])language=en(&|$)"));
    }

    [Fact]
    public async Task SearchAsync_logs_the_endpoint_and_outcome()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, LoadFixture("geocoding-london-200.json"));
        var logger = new CapturingLogger<OpenMeteoGateway>();
        var gateway = GatewayWith(handler, logger);

        await gateway.SearchAsync("London", CancellationToken.None);

        const string endpoint = "https://geocoding-api.open-meteo.com/v1/search";
        Assert.Single(logger.Messages, m => m.Contains(endpoint) && m.Contains("requesting"));
        Assert.Single(logger.Messages, m => m.Contains(endpoint) && m.Contains("200"));
    }
}
