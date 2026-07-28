using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests.Favourites;

public class LocationIdentityTests
{
    [Fact]
    public void Same_id_different_label_and_coordinates_is_the_same_location()
    {
        var a = new Location(51.5, -0.12, "London, GB", 2643743);
        var b = new Location(48.8, 2.35, "Londres", 2643743); // absurd coords/label, same id
        Assert.True(LocationIdentity.Same(a, b));
    }

    [Fact]
    public void Different_non_null_ids_are_distinct_regardless_of_coordinates()
    {
        var a = new Location(51.5, -0.12, "London", 1);
        var b = new Location(51.5, -0.12, "London", 2); // identical coords, different id
        Assert.False(LocationIdentity.Same(a, b));
    }

    [Fact]
    public void Both_ids_null_falls_back_to_exact_coordinate_equality()
    {
        var a = new Location(51.5, -0.12, "A", null);
        var bSame = new Location(51.5, -0.12, "B", null);
        var bDiff = new Location(51.5, -0.13, "A", null);
        Assert.True(LocationIdentity.Same(a, bSame));   // Label ignored, coords equal
        Assert.False(LocationIdentity.Same(a, bDiff));  // coords differ
    }

    [Fact]
    public void One_id_null_one_non_null_falls_back_to_coordinates()
    {
        var a = new Location(51.5, -0.12, "A", 2643743);
        var b = new Location(51.5, -0.12, "A", null);
        Assert.True(LocationIdentity.Same(a, b));       // either id null → coordinate path
    }

    [Fact]
    public void Same_is_reflexive_and_symmetric()
    {
        var a = new Location(51.5, -0.12, "A", 2643743);
        var b = new Location(51.5, -0.12, "A", 2643743);
        Assert.True(LocationIdentity.Same(a, a));
        Assert.Equal(LocationIdentity.Same(a, b), LocationIdentity.Same(b, a));
    }

    [Fact]
    public void Comparer_matches_Same_and_is_usable_for_dedup()
    {
        var a = new Location(51.5, -0.12, "London, GB", 2643743);
        var b = new Location(0, 0, "elsewhere", 2643743);
        Assert.True(LocationIdentity.Comparer.Equals(a, b));
        Assert.Equal(LocationIdentity.Comparer.GetHashCode(a), LocationIdentity.Comparer.GetHashCode(b));
    }

    [Fact]
    public void Comparer_gives_equal_hashes_for_coordinate_identity_when_ids_are_null()
    {
        var a = new Location(51.5, -0.12, "A", null);
        var b = new Location(51.5, -0.12, "different label", null);
        Assert.True(LocationIdentity.Comparer.Equals(a, b));
        Assert.Equal(LocationIdentity.Comparer.GetHashCode(a), LocationIdentity.Comparer.GetHashCode(b));
    }

    [Fact]
    public void Comparer_gives_equal_hashes_when_one_id_null_one_non_null_and_coordinates_equal()
    {
        var a = new Location(51.5, -0.12, "X", 2643743);
        var b = new Location(51.5, -0.12, "Y", null);
        Assert.True(LocationIdentity.Comparer.Equals(a, b));    // either id null → coordinate path, equal
        Assert.Equal(LocationIdentity.Comparer.GetHashCode(a), LocationIdentity.Comparer.GetHashCode(b));
    }
}
