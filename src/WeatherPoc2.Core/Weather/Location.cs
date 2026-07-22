namespace WeatherPoc2.Core.Weather;

/// <summary>
/// A resolved geographic place (Context.MD: a Location is a resolved place, never a bare string).
/// OpenMeteoId is null until geocoding mints it (Feature 3); Feature 1 uses a fixed constant.
/// </summary>
public sealed record Location(double Latitude, double Longitude, string Label, int? OpenMeteoId = null)
{
    /// <summary>The single hard-coded Location used by the Feature-1 tracer bullet.</summary>
    public static Location LondonGb { get; } = new(51.5074, -0.1278, "London, GB");
}
