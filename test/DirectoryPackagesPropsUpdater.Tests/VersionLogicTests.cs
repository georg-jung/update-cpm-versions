using NuGet.Versioning;

using DirectoryPackagesPropsUpdater;

namespace DirectoryPackagesPropsUpdater.Tests;

public class VersionLogicTests
{
    private static List<NuGetVersion> V(params string[] versions)
        => versions.Select(NuGetVersion.Parse).ToList();

    [Test]
    public async Task FindBestUpdate_Minor_StaysInMajorBand()
    {
        var current = NuGetVersion.Parse("2.1.0");
        var available = V("1.0.0", "2.0.0", "2.1.0", "2.2.0", "2.3.0", "3.0.0");

        var result = PackageUpdater.FindBestUpdate(current, available, VersionMode.Minor);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!).IsEqualTo(NuGetVersion.Parse("2.3.0"));
    }

    [Test]
    public async Task FindBestUpdate_Minor_DoesNotCrossMajor()
    {
        var current = NuGetVersion.Parse("2.3.0");
        var available = V("2.3.0", "3.0.0", "4.0.0");

        var result = PackageUpdater.FindBestUpdate(current, available, VersionMode.Minor);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindBestUpdate_PatchOnly_StaysInMinorBand()
    {
        var current = NuGetVersion.Parse("2.1.0");
        var available = V("2.1.0", "2.1.1", "2.1.5", "2.2.0", "3.0.0");

        var result = PackageUpdater.FindBestUpdate(current, available, VersionMode.PatchOnly);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!).IsEqualTo(NuGetVersion.Parse("2.1.5"));
    }

    [Test]
    public async Task FindBestUpdate_PatchOnly_DoesNotCrossMinor()
    {
        var current = NuGetVersion.Parse("2.1.5");
        var available = V("2.1.5", "2.2.0", "3.0.0");

        var result = PackageUpdater.FindBestUpdate(current, available, VersionMode.PatchOnly);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindBestUpdate_Major_ReturnsHighest()
    {
        var current = NuGetVersion.Parse("2.1.0");
        var available = V("2.1.0", "2.2.0", "3.0.0", "4.0.0-beta", "4.0.0");

        var result = PackageUpdater.FindBestUpdate(current, available, VersionMode.Major);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!).IsEqualTo(NuGetVersion.Parse("4.0.0"));
    }

    [Test]
    public async Task FindBestUpdate_ReturnsNull_WhenAlreadyLatest()
    {
        var current = NuGetVersion.Parse("3.0.0");
        var available = V("1.0.0", "2.0.0", "3.0.0");

        var result = PackageUpdater.FindBestUpdate(current, available, VersionMode.Major);

        await Assert.That(result).IsNull();
    }

    [Test]
    [Arguments("1.0.0", "2.0.0", "Major")]
    [Arguments("1.0.0", "1.1.0", "Minor")]
    [Arguments("1.0.0", "1.0.1", "Patch")]
    [Arguments("1.0.0", "1.0.0", "None")]
    public async Task ClassifyUpdate_CategorizesCorrectly(
        string current, string updated, string expectedKind)
    {
        var expected = Enum.Parse<UpdateKind>(expectedKind);
        var result = PackageUpdater.ClassifyUpdate(
            NuGetVersion.Parse(current), NuGetVersion.Parse(updated));

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ClassifyUpdate_PrereleaseToStable_IsPatch()
    {
        var result = PackageUpdater.ClassifyUpdate(
            NuGetVersion.Parse("2.0.0-rc.1"), NuGetVersion.Parse("2.0.0"));

        await Assert.That(result).IsEqualTo(UpdateKind.Patch);
    }
}
