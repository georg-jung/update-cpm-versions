using UpdateCpmVersions;

namespace UpdateCpmVersions.Tests;

// Tests for the CLI layer (CliCommand / Program.cs): argument parsing, mutual-exclusivity
// validation, and that the README example commands are accepted and complete without error.
public class ProgramTests
{
    private static string WriteTempProps(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "Directory.Packages.props");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    // An empty props file causes PackageUpdater to return 0 immediately with no NuGet calls.
    private static string EmptyProps() => WriteTempProps("""
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
        </Project>
        """);

    // --- Mutual-exclusivity validation (returns 1 before any file I/O) ---

    [Test]
    public async Task MajorAndPatchOnly_ReturnsExitCode1()
    {
        var exitCode = await CliCommand.Build()
            .Parse(["--major", "--patch-only"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task IncludeAndExclude_ReturnsExitCode1()
    {
        var exitCode = await CliCommand.Build()
            .Parse(["--include", "Microsoft.*", "--exclude", "Contoso.*"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(1);
    }

    // --- README example commands (empty props file → no NuGet calls) ---

    [Test]
    public async Task DryRun_ReturnsExitCode0()
    {
        // update-cpm-versions --dry-run
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task DryRunShortAlias_ReturnsExitCode0()
    {
        // update-cpm-versions -n
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "-n"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task MajorDryRun_ReturnsExitCode0()
    {
        // update-cpm-versions --major --dry-run
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "--major", "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task PatchOnlyDryRun_ReturnsExitCode0()
    {
        // update-cpm-versions --patch-only --dry-run
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "--patch-only", "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task IncludeDryRun_ReturnsExitCode0()
    {
        // update-cpm-versions --include "Microsoft.*" --dry-run
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "--include", "Microsoft.*", "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task ExcludeDryRun_ReturnsExitCode0()
    {
        // update-cpm-versions --exclude "Contoso.*" --dry-run
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "--exclude", "Contoso.*", "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task MultipleExcludesDryRun_ReturnsExitCode0()
    {
        // update-cpm-versions --exclude "Contoso.*" --exclude "Legacy.*" --dry-run
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "--exclude", "Contoso.*", "--exclude", "Legacy.*", "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task MajorPinMajorDryRun_ReturnsExitCode0()
    {
        // update-cpm-versions --major --pin-major "Microsoft.EntityFrameworkCore.*" --dry-run
        var exitCode = await CliCommand.Build()
            .Parse([EmptyProps(), "--major", "--pin-major", "Microsoft.EntityFrameworkCore.*", "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
    }

    // --- Full round-trip with a real package (makes a NuGet API call) ---

    [Test]
    public async Task DryRun_WithRealPackage_DoesNotModifyFile()
    {
        // NETStandard.Library is frozen; 1.6.0 can update but --dry-run must not write.
        var path = WriteTempProps("""
            <Project>
              <ItemGroup>
                <PackageVersion Include="NETStandard.Library" Version="1.6.0" />
              </ItemGroup>
            </Project>
            """);
        var originalContent = File.ReadAllText(path);

        var exitCode = await CliCommand.Build()
            .Parse([path, "--dry-run"])
            .InvokeAsync();

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(File.ReadAllText(path)).IsEqualTo(originalContent);
    }
}
