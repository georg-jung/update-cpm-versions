using NuGet.Versioning;

using DirectoryPackagesPropsUpdater;

namespace DirectoryPackagesPropsUpdater.Tests;

public class IntegrationTests
{
    private static string WriteTempProps(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "Directory.Packages.props");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    // NETStandard.Library is frozen. 2.0.3 is the latest and will not change.
    // 1.6.0 can update minor (1.6.1) or major (2.0.3).
    // 2.0.1 can update patch (2.0.3).
    // 2.0.3 is already latest.

    [Test]
    public async Task DryRun_DefaultMinor_NETStandard160_UpdatesToLatest1x()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="1.6.0" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions { DryRun = true };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        // File should be unchanged in dry-run mode
        var (_, packages) = PackagePropsParser.Parse(path);
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("1.6.0"));
    }

    [Test]
    public async Task DefaultMinor_NETStandard160_UpdatesWithinMajor1()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="1.6.0" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions { VersionMode = VersionMode.Minor };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        var updated = packages[0].Version;
        // Must stay in major 1.x but be higher than 1.6.0
        await Assert.That(updated.Major).IsEqualTo(1);
        await Assert.That(updated).IsGreaterThan(NuGetVersion.Parse("1.6.0"));
    }

    [Test]
    public async Task Major_NETStandard160_UpdatesTo2x()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="1.6.0" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions { VersionMode = VersionMode.Major };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("2.0.3"));
    }

    [Test]
    public async Task PatchOnly_NETStandard201_UpdatesTo203()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="2.0.1" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions { VersionMode = VersionMode.PatchOnly };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("2.0.3"));
    }

    [Test]
    public async Task AlreadyLatest_NETStandard203_NoChanges()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="2.0.3" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions { VersionMode = VersionMode.Major };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("2.0.3"));
    }

    [Test]
    public async Task MultiplePackages_UpdatesCorrectly()
    {
        // NETStandard.Library 2.0.1 -> 2.0.3 (patch)
        // Microsoft.NETCore.Platforms 1.0.0 -> 1.x.x (minor, stays in 1.x)
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="2.0.1" />
                <PackageVersion Include="Microsoft.NETCore.Platforms" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions { VersionMode = VersionMode.Minor };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("2.0.3"));
        await Assert.That(packages[1].Version.Major).IsEqualTo(1);
        await Assert.That(packages[1].Version).IsGreaterThan(NuGetVersion.Parse("1.0.0"));
    }

    [Test]
    public async Task ExcludeFilter_SkipsMatchingPackages()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="2.0.1" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions
        {
            ExcludePatterns = ["NETStandard.*"],
        };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        // Should be unchanged since it was excluded
        var (_, packages) = PackagePropsParser.Parse(path);
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("2.0.1"));
    }

    [Test]
    public async Task IncludeFilter_OnlyUpdatesMatching()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="2.0.1" />
                <PackageVersion Include="Microsoft.NETCore.Platforms" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions
        {
            IncludePatterns = ["NETStandard.*"],
        };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        // NETStandard.Library was included, should be updated
        await Assert.That(packages[0].Version).IsEqualTo(NuGetVersion.Parse("2.0.3"));
        // Microsoft.NETCore.Platforms was NOT included, should be unchanged
        await Assert.That(packages[1].Version).IsEqualTo(NuGetVersion.Parse("1.0.0"));
    }

    [Test]
    public async Task PinMajor_WithMajorMode_KeepsPinnedInBand()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="1.6.0" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions
        {
            VersionMode = VersionMode.Major,
            PinMajorPatterns = ["NETStandard.*"],
        };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);

        var (_, packages) = PackagePropsParser.Parse(path);
        // Pinned to major 1, so should update within 1.x but not to 2.x
        await Assert.That(packages[0].Version.Major).IsEqualTo(1);
        await Assert.That(packages[0].Version).IsGreaterThan(NuGetVersion.Parse("1.6.0"));
    }

    [Test]
    public async Task EmptyFile_ReturnsZero()
    {
        var path = WriteTempProps("""
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>
            """);

        var options = new UpdateOptions();
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task NonexistentPath_ReturnsOne()
    {
        var bogusPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var options = new UpdateOptions();
        var exitCode = await PackageUpdater.RunAsync(bogusPath, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task AllExcluded_ReturnsZero()
    {
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="2.0.1" />
              </ItemGroup>
            </Project>
            """);

        var options = new UpdateOptions
        {
            ExcludePatterns = ["*"],
        };
        var exitCode = await PackageUpdater.RunAsync(path, options, null, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
    }
}
