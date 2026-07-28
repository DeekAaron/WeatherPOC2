using System.Collections.Generic;

namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single, shared definition of Location identity (Context.MD: a Location is identified by its
/// resolved place, not the query that found it). Two Locations are the same place when both carry a
/// non-null <see cref="Location.OpenMeteoId"/> and the ids are equal; when either id is null, they are
/// the same when latitude AND longitude are exactly equal. <see cref="Location.Label"/> is never part
/// of identity. Total over all pairs — never throws. The single predicate both Favourites and Search
/// History key on (Spec D2).
/// </summary>
public static class LocationIdentity
{
    /// <summary>True iff <paramref name="a"/> and <paramref name="b"/> resolve to the same place.</summary>
    public static bool Same(Location a, Location b)
    {
        if (a.OpenMeteoId is int idA && b.OpenMeteoId is int idB)
            return idA == idB;
        return a.Latitude == b.Latitude && a.Longitude == b.Longitude;
    }

    /// <summary>The same rule as an <see cref="IEqualityComparer{T}"/> for de-dup / set keys.</summary>
    public static IEqualityComparer<Location> Comparer { get; } = new IdentityComparer();

    private sealed class IdentityComparer : IEqualityComparer<Location>
    {
        public bool Equals(Location? x, Location? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return Same(x, y);
        }

        // Hash on the id when present, else on the coordinate pair — consistent with Same
        // (equal-by-Same items hash equal; Label is never mixed in).
        public int GetHashCode(Location obj)
            => obj.OpenMeteoId is int id
                ? HashCode.Combine(id)
                : HashCode.Combine(obj.Latitude, obj.Longitude);
    }
}
