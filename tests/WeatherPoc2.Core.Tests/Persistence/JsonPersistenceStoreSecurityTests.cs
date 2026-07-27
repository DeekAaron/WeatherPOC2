using System.Text;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Persistence;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Units;
using Xunit;

namespace WeatherPoc2.Core.Tests.Persistence;

/// <summary>
/// Security guards folded into the Persistence Store seam (added by /check-security-design): a
/// path-traversal guard on the caller-supplied <c>key</c>, and fail-closed handling of an
/// adversarially-malformed (deeply-nested) persisted document. Both belong on the seam that owns path
/// construction and content deserialization, because Features 6/7 extend this generic store.
/// </summary>
public class JsonPersistenceStoreSecurityTests
{
    // Keys that would escape {basePath}/{key}.json — a separator, a traversal segment, or a rooted path.
    public static TheoryData<string> UnsafeKeys() => new()
    {
        "",
        "   ",
        "../units",
        "a/b",
        "/etc/passwd",
        "..\\units",
        "C:\\Windows\\System32\\evil",
    };

    [Theory]
    [MemberData(nameof(UnsafeKeys))]
    public async Task SaveAsync_rejects_an_unsafe_key_before_touching_the_filesystem(string key)
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, new CapturingLogger<JsonPersistenceStore>());

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(key, UnitPreferences.Default));

        // The guard runs before any file access: the base directory is never even created.
        Assert.False(Directory.Exists(paths.GetAppDataDirectory()));
    }

    [Theory]
    [MemberData(nameof(UnsafeKeys))]
    public async Task LoadAsync_rejects_an_unsafe_key_before_touching_the_filesystem(string key)
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, new CapturingLogger<JsonPersistenceStore>());

        await Assert.ThrowsAsync<ArgumentException>(() => store.LoadAsync<UnitPreferences>(key));

        Assert.False(Directory.Exists(paths.GetAppDataDirectory()));
    }

    [Theory]
    [InlineData("units")]
    [InlineData("search-history")]
    [InlineData("favourites")]
    public async Task A_valid_single_segment_key_is_accepted_and_resolves_under_the_base_directory(string key)
    {
        // Regression guard: the key guard must reject nothing legitimate — the keys Features 5/6/7 use.
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, new CapturingLogger<JsonPersistenceStore>());

        await store.SaveAsync(key, UnitPreferences.Default);

        var expected = Path.Combine(paths.GetAppDataDirectory(), key + ".json");
        Assert.True(File.Exists(expected)); // resolved to {basePath}/{key}.json, under the base dir
    }

    [Fact]
    public async Task LoadAsync_fails_closed_on_a_deeply_nested_document_without_throwing()
    {
        // A structurally hostile file — nesting well beyond System.Text.Json's default MaxDepth of 64 —
        // must fail closed to null + Warning and never crash or throw to the caller (ADR-0001 / D5).
        using var paths = new TempAppDataPathProvider();
        var log = new CapturingLogger<JsonPersistenceStore>();
        var store = new JsonPersistenceStore(paths, log);
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        var hostile = new StringBuilder();
        for (var i = 0; i < 200; i++) hostile.Append('[');
        for (var i = 0; i < 200; i++) hostile.Append(']');
        await File.WriteAllTextAsync(Path.Combine(paths.GetAppDataDirectory(), "units.json"), hostile.ToString());

        var loaded = await store.LoadAsync<UnitPreferences>("units"); // must not throw

        Assert.Null(loaded);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);
    }
}
