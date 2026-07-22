using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

// Tier-2 live drift guard: makes ONE real call to api.open-meteo.com through the real
// OpenMeteoGateway. Trait-gated so it never runs on the per-commit (Tier-1) path:
//   dotnet test --filter "Tier!=2-Live"   → per-commit run (no network dependency)
//   dotnet test --filter "Tier=2-Live"    → scheduled (daily) run
// Cost ceiling: ≤ 5 live calls per scheduled run, once per day. Open-Meteo is free and
// keyless, so the ceiling is call-volume, not money. This is the ratchet's guard against
// fixture drift at the Open-Meteo seam. No pipeline/schedule wiring lives here — the trait
// makes the split possible; the schedule lands with the Feature's CI setup.
[Trait("Tier", "2-Live")]
public class LiveOpenMeteoTests
{
    [Fact]
    public async Task Live_London_fetch_returns_a_celsius_bundle()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient());
        var gateway = new OpenMeteoGateway(factory, NullLogger<OpenMeteoGateway>.Instance);

        // The call succeeding IS the unit-aware assertion: the Gateway throws
        // WeatherUnavailableException unless current_units.temperature_2m == "°C",
        // so a returned bundle proves the live response is in Celsius — a server-side
        // unit-default change would fail here, not pass a loose plausibility band.
        var bundle = await gateway.GetWeatherAsync(Location.LondonGb, CancellationToken.None);

        Assert.InRange(bundle.CurrentTemperatureCelsius, -60.0, 60.0); // sanity band on top of the unit guarantee
    }
}
