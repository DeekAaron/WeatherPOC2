using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class SearchHistoryTests
{
    private static Location Loc(int id, string label = "x", double lat = 0, double lon = 0) =>
        new(lat, lon, label, id);

    private static string[] Labels(SearchHistory h) => h.Entries.Select(e => e.Label).ToArray();

    [Fact]
    public void Record_puts_the_newest_load_at_the_front()
    {
        var h = new SearchHistory();
        h.Record(Loc(1, "a"));
        h.Record(Loc(2, "b"));

        Assert.Equal(new[] { "b", "a" }, Labels(h));
    }

    [Fact]
    public void Record_dedupes_by_identity_and_moves_to_front_without_growing()
    {
        var h = new SearchHistory();
        h.Record(Loc(1, "a"));
        h.Record(Loc(2, "b"));
        h.Record(Loc(1, "a-again")); // same OpenMeteoId as the first -> move to front, no duplicate

        Assert.Equal(2, h.Entries.Count);
        Assert.Equal(new[] { "a-again", "b" }, Labels(h)); // Label is not identity; the re-loaded label wins its slot
    }

    [Fact]
    public void Record_evicts_the_oldest_when_a_new_load_arrives_at_capacity_four()
    {
        var h = new SearchHistory();
        h.Record(Loc(1, "a"));
        h.Record(Loc(2, "b"));
        h.Record(Loc(3, "c"));
        h.Record(Loc(4, "d"));
        h.Record(Loc(5, "e")); // genuinely new at capacity -> drop the oldest (tail = "a")

        Assert.Equal(new[] { "e", "d", "c", "b" }, Labels(h));
    }

    [Fact]
    public void Identity_matches_on_open_meteo_id_ignoring_label_and_coordinates()
    {
        var h = new SearchHistory();
        h.Record(new Location(51.5, -0.1, "London", 2643743));
        h.Record(new Location(1.0, 2.0, "London (typed differently)", 2643743)); // same id

        Assert.Single(h.Entries);
    }

    [Fact]
    public void Identity_falls_back_to_exact_coordinates_when_an_id_is_null()
    {
        var h = new SearchHistory();
        h.Record(new Location(51.5, -0.1, "A", OpenMeteoId: null));
        h.Record(new Location(51.5, -0.1, "B", OpenMeteoId: null)); // same coords, no id -> same
        h.Record(new Location(52.0, -0.1, "C", OpenMeteoId: null)); // different lat -> distinct

        Assert.Equal(2, h.Entries.Count);
        Assert.Equal(new[] { "C", "B" }, Labels(h));
    }

    [Fact]
    public void Different_ids_are_distinct_regardless_of_label()
    {
        var h = new SearchHistory();
        h.Record(new Location(0, 0, "same-label", 1));
        h.Record(new Location(0, 0, "same-label", 2)); // different id -> distinct even with identical label/coords

        Assert.Equal(2, h.Entries.Count);
    }

    [Fact]
    public void Seed_normalises_a_bad_list_to_at_most_four_distinct_most_recent_first()
    {
        var h = new SearchHistory();
        h.Seed(new[]
        {
            Loc(1, "a"), Loc(1, "a-dup"), Loc(2, "b"), Loc(3, "c"), Loc(4, "d"), Loc(5, "e"),
        });

        // dedupe keeps the front-most occurrence of id 1, then cap to the first four
        Assert.Equal(new[] { "a", "b", "c", "d" }, Labels(h));
    }

    [Fact]
    public void Record_raises_Changed_when_entries_change()
    {
        var h = new SearchHistory();
        var raised = 0;
        h.Changed += (_, _) => raised++;

        h.Record(Loc(1));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Record_of_the_current_front_is_a_noop_and_does_not_raise_Changed()
    {
        var h = new SearchHistory();
        h.Record(Loc(1, "a"));
        var raised = 0;
        h.Changed += (_, _) => raised++;

        h.Record(Loc(1, "a")); // already front, same identity and same label -> no observable change

        Assert.Single(h.Entries);
        Assert.Equal(0, raised);
    }
}
