namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The Gateway's return shape. Feature 1 carries only the current temperature in canonical °C
/// (ADR-0001: weather is always held in canonical units). Extended — not reshaped — in F2/F4.
/// </summary>
public sealed record WeatherBundle(double CurrentTemperatureCelsius);
