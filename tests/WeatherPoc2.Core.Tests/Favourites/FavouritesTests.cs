using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests.Favourites;

public class FavouritesTests
{
    private static Location Loc(int id) => new(id, id, $"Place {id}", id);

    [Fact]
    public void Mark_inserts_at_the_front_most_recently_marked_first()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        Assert.Equal(MarkResult.Marked, f.Mark(Loc(1)));
        Assert.Equal(MarkResult.Marked, f.Mark(Loc(2)));
        Assert.Equal(new int?[] { 2, 1 }, f.Entries.Select(e => e.OpenMeteoId));
    }

    [Fact]
    public void Mark_of_an_existing_favourite_returns_AlreadyFavourite_and_does_not_reorder()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        f.Mark(Loc(1));
        f.Mark(Loc(2));
        Assert.Equal(MarkResult.AlreadyFavourite, f.Mark(Loc(1))); // identity-present
        Assert.Equal(new int?[] { 2, 1 }, f.Entries.Select(e => e.OpenMeteoId)); // order unchanged
    }

    [Fact]
    public void Mark_at_capacity_five_returns_RefusedFull_and_changes_nothing()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        for (var i = 1; i <= 5; i++) f.Mark(Loc(i));
        var before = f.Entries.Select(e => e.OpenMeteoId).ToArray();

        Assert.Equal(MarkResult.RefusedFull, f.Mark(Loc(6))); // recency never evicts
        Assert.Equal(before, f.Entries.Select(e => e.OpenMeteoId)); // untouched
    }

    [Fact]
    public void Unmark_removes_by_identity_and_returns_true_false_appropriately()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        f.Mark(Loc(1));
        f.Mark(Loc(2));
        Assert.True(f.Unmark(Loc(1)));
        Assert.Equal(new int?[] { 2 }, f.Entries.Select(e => e.OpenMeteoId));
        Assert.False(f.Unmark(Loc(99))); // not present → no change
    }

    [Fact]
    public void IsFavourite_uses_identity_not_label_or_coordinates()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        f.Mark(new Location(51.5, -0.12, "London, GB", 2643743));
        Assert.True(f.IsFavourite(new Location(0, 0, "different label", 2643743)));
        Assert.False(f.IsFavourite(new Location(51.5, -0.12, "London, GB", 999)));
    }

    [Fact]
    public void Seed_normalises_dedupe_by_identity_keeping_front_most_then_caps_to_five()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        // 7 entries with a duplicate id (2) — parseable but invariant-violating on-disk shape.
        f.Seed(new[] { Loc(1), Loc(2), Loc(3), Loc(2), Loc(4), Loc(5), Loc(6) });
        // Dedupe keeps the front-most occurrence, then cap 5.
        Assert.Equal(new int?[] { 1, 2, 3, 4, 5 }, f.Entries.Select(e => e.OpenMeteoId));
    }

    [Fact]
    public void Changed_is_raised_on_real_change_and_not_on_a_no_op()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        var raised = 0;
        f.Changed += (_, _) => raised++;

        f.Mark(Loc(1));                 // change → raise
        f.Mark(Loc(1));                 // AlreadyFavourite → no raise
        f.Unmark(Loc(99));              // absent → no raise
        Assert.Equal(1, raised);

        for (var i = 2; i <= 6; i++) f.Mark(Loc(i)); // marks 2..5 (Loc(1) still present) then i=6 RefusedFull
        // one is present (Loc 1), so marks 2,3,4,5 fill to 5 = 4 raises; i=6 RefusedFull → no raise
        Assert.Equal(1 + 4, raised);
    }

    [Fact]
    public void Seed_raises_Changed()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        var raised = 0;
        f.Changed += (_, _) => raised++;

        f.Seed(new[] { Loc(1), Loc(2) });

        Assert.Equal(1, raised);
    }

    // --- Security: Seed is the normalise-don't-trust guard at the persistence trust boundary. It must
    // be TOTAL (never throw) and invariant-preserving (Entries always <=5 distinct) for ANY input,
    // including a hand-edited / hostile favourites.json with value-degenerate Locations. ---

    [Fact]
    public void Seed_is_total_and_invariant_preserving_for_value_degenerate_locations()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        var hostile = new List<Location>
        {
            new(double.NaN, double.NaN, "nan", null),
            new(double.PositiveInfinity, double.NegativeInfinity, "inf", null),
            new(1e308, -1e308, "huge", null),
            new(91.0, 181.0, "out-of-range lat/long", null),   // beyond WGS84 valid range
            new(-91.0, -181.0, "out-of-range 2", null),
            new(double.NaN, double.NaN, "nan again", null),
            new(0, 0, "null island", null),
        };

        var ex = Record.Exception(() => f.Seed(hostile)); // fail-closed: totally, never throws

        Assert.Null(ex);
        Assert.True(f.Entries.Count <= WeatherPoc2.Core.Weather.Favourites.Capacity); // <=5, invariant held
    }

    [Fact]
    public void Seed_is_total_for_a_null_collection()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();

        var ex = Record.Exception(() => f.Seed(null!)); // absent/null favourites document → no crash

        Assert.Null(ex);
        Assert.Empty(f.Entries);
    }

    [Fact]
    public void Seed_normalises_away_null_elements_without_throwing()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        // A parseable favourites.json array carrying null element(s): [null], [null, valid], [null, null, valid].
        // Each null is not a valid Location and must be dropped, never stored (else a later
        // Mark/IsFavourite/dedupe dereferences it) and never thrown out of Seed (persistence trust boundary).
        var hostile = new List<Location?> { null, Loc(1), null, null };

        var ex = Record.Exception(() => f.Seed(hostile!)); // fail-closed: totally, never throws

        Assert.Null(ex);
        Assert.DoesNotContain(f.Entries, e => e is null);   // nulls normalised away
        Assert.Equal(new int?[] { 1 }, f.Entries.Select(e => e.OpenMeteoId)); // only the valid entry survives
    }

    [Fact]
    public void Seed_dedupes_by_identity_even_when_coordinates_are_degenerate()
    {
        var f = new WeatherPoc2.Core.Weather.Favourites();
        // Two entries share an OpenMeteoId (identity), despite NaN / Infinity coordinates.
        f.Seed(new[]
        {
            new Location(double.NaN, double.NaN, "first", 7),
            new Location(double.PositiveInfinity, 0, "second", 7),
        });

        Assert.Single(f.Entries);              // one identity → deduped, no duplicate survives
        Assert.Equal(7, f.Entries[0].OpenMeteoId);
    }
}
