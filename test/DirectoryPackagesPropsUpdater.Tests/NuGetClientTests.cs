using NuGet.Versioning;

using DirectoryPackagesPropsUpdater;

namespace DirectoryPackagesPropsUpdater.Tests;

public class NuGetClientTests
{
    [Test]
    public async Task GetVersionsAsync_ReturnsVersionsForKnownPackage()
    {
        using var client = new NuGetClient();
        var versions = await client.GetVersionsAsync("NETStandard.Library");

        await Assert.That(versions.Count).IsGreaterThan(0);
        await Assert.That(versions).Contains(NuGetVersion.Parse("1.6.0"));
        await Assert.That(versions).Contains(NuGetVersion.Parse("2.0.3"));
    }

    [Test]
    public async Task GetVersionsAsync_ReturnsEmptyForNonexistentPackage()
    {
        using var client = new NuGetClient();
        var versions = await client.GetVersionsAsync(
            "This.Package.Should.Never.Exist.On.NuGet.12345");

        await Assert.That(versions).IsEmpty();
    }

    [Test]
    public async Task GetAllVersionsAsync_FetchesMultiplePackages()
    {
        using var client = new NuGetClient();
        var result = await client.GetAllVersionsAsync(
            ["NETStandard.Library", "Microsoft.NETCore.Platforms"]);

        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result["NETStandard.Library"].Count).IsGreaterThan(0);
        await Assert.That(result["Microsoft.NETCore.Platforms"].Count).IsGreaterThan(0);
    }
}
