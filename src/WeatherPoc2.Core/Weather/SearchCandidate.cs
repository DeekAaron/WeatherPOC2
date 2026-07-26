namespace WeatherPoc2.Core.Weather;

/// <summary>
/// One possible place a Location Search turns up for a typed name (Context.MD: a Search Candidate
/// carries label, region/country, coordinates; it is NOT yet a Location — it becomes one only when
/// the user picks it). <see cref="Region"/> is the Open-Meteo `admin1`, which can be absent.
/// </summary>
public sealed record SearchCandidate(
    int Id, string Name, string? Region, string Country, double Latitude, double Longitude)
{
    /// <summary>Display label: "Name, Region, Country", collapsing to "Name, Country" when Region is absent.</summary>
    public string Label => Region is null ? $"{Name}, {Country}" : $"{Name}, {Region}, {Country}";
}
