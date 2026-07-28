namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The outcome of <see cref="Favourites.Mark"/>. The pure state machine returns this; the ViewModel
/// maps <see cref="RefusedFull"/> to the friendly copy "Favourites are full — remove one first"
/// (Technical-Context: user-facing copy lives in the presentation layer, and block-on-overflow is an
/// expected domain outcome, not an exception — Principle 1).
/// </summary>
public enum MarkResult
{
    /// <summary>The Location was added at the front.</summary>
    Marked,

    /// <summary>The Location was already a Favourite (by identity); no change, no reorder.</summary>
    AlreadyFavourite,

    /// <summary>The list was already at capacity five; the mark was refused, nothing changed.</summary>
    RefusedFull,
}
