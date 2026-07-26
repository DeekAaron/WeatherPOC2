namespace WeatherPoc2.Core.Weather;

/// <summary>
/// One hour of the Hourly Forecast, in canonical units (ADR-0001). LocalTime is the Location's
/// wall clock (Kind=Unspecified — see the Gateway's timestamp parse). Every measure is nullable:
/// Open-Meteo may null an individual hourly value; a null flows through to a "—" placeholder + a
/// logged Warning in the ViewModel (Spec D3). LocalTime itself is non-null once produced — a
/// mismatched/absent hourly series is the Gateway's fail-closed path, never a null LocalTime.
/// </summary>
public sealed record HourlyForecastPoint(
    DateTime LocalTime,
    double? TemperatureCelsius,
    int? WeatherCode,
    bool? IsDay,
    int? ChanceOfRainPercent);
