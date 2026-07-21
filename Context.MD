# Domain Glossary

## Chance of Rain
The percentage likelihood of precipitation for a given hour, shown against each entry in the Hourly Forecast. A probability, not a measured rainfall amount.

## Current Conditions
The weather for the Location right now: its Temperature, Chance of Rain, and Wind Speed, alongside the Weather Condition and Weather Icon. This is the default view when a Location is loaded. Distinct from the Hourly Forecast, which projects the same measures forward across the day rather than describing the present moment.

## Favourites
Locations the user has explicitly marked to keep. Unlike the Search History, Favourites are user-curated and are not evicted by newer searches.

## Hourly Forecast
The day's weather broken down hour by hour for the current Location, presented as a horizontally scrollable list beneath the Current Conditions. Each entry carries a Time, a Weather Icon, a Temperature, and a Chance of Rain. Distinct from Current Conditions, which describes only the present.

## Location
A named geographic place the app shows weather for, arrived at through a Location Search. Every view of Current Conditions and the Hourly Forecast is for exactly one Location at a time.

## Location Search
The action of finding a Location by name. Shown as the entry point when no Location is loaded, and the means by which a Location enters the Search History.

## Open-Meteo
The external weather service that supplies all Current Conditions and Hourly Forecast data. Keyless and free; the single source of weather truth for the app. Not a domain concept the user sees — the origin of the data behind every other weather term here.

## Search History
The four most recently searched Locations, ordered by recency and managed automatically — a new search adds a Location and drops the oldest past four. On launch the app loads the most recent of these. Distinct from Favourites, which the user curates deliberately and which recency never evicts.

## Temperature
How warm or cold it is, shown for both Current Conditions and each Hourly Forecast entry. Displayed in the user's chosen temperature Unit (default Celsius).

## Time
The hour a given Hourly Forecast entry describes. Distinguishes one forecast entry from the next; not part of Current Conditions, which is always "now".

## Units
The user's chosen measurement systems for display — temperature (default Celsius) and Wind Speed (default kilometres per hour). Selectable by the user; a display preference only, it does not change the underlying Open-Meteo data.

## Weather Condition
The qualitative state of the weather at a moment — clear, cloudy, rain, and the like — as opposed to the numeric measures (Temperature, Chance of Rain, Wind Speed). Represented visually by a Weather Icon.

## Weather Icon
The graphic that represents a Weather Condition for a time period, shown in Current Conditions and against each Hourly Forecast entry. The visual form of a Weather Condition, not a distinct concept from it.

## Wind Speed
How fast the wind is blowing, shown for Current Conditions and part of the weather measures. Displayed in the user's chosen Unit (default kilometres per hour).
