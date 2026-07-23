using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class WeatherIconAssetsTests
{
    // Reverse of the mapper key-set closure (Spec Seam 2 (c-1)(ii)): every declared icon key MUST
    // have a source SVG asset, or Image.Source="{key}.png" renders blank (silent) at runtime.
    [Fact]
    public void Every_declared_icon_key_has_a_source_svg_asset()
    {
        var imagesDir = LocateAppImagesDir();
        foreach (var key in WeatherIconKeys.All)
        {
            var svg = Path.Combine(imagesDir, $"{key}.svg");
            Assert.True(File.Exists(svg), $"Missing source SVG for icon key '{key}': expected {svg}");
        }
    }

    // Climb from the test output dir to the repo and locate the App's image assets.
    private static string LocateAppImagesDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "WeatherPoc2.App", "Resources", "Images");
            if (Directory.Exists(candidate))
                return candidate;
        }
        throw new DirectoryNotFoundException(
            "Could not locate src/WeatherPoc2.App/Resources/Images by climbing from the test output directory.");
    }
}
