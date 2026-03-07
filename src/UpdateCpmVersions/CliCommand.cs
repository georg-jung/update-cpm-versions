using System.CommandLine;

namespace UpdateCpmVersions;

internal static class CliCommand
{
    internal static RootCommand Build()
    {
        Argument<string?> pathArg = new("path")
        {
            Description = "Path to Directory.Packages.props or a directory containing it",
            Arity = ArgumentArity.ZeroOrOne,
        };

        Option<bool> majorOpt = new("--major")
        {
            Description = "Allow major version updates",
        };

        Option<bool> patchOnlyOpt = new("--patch-only")
        {
            Description = "Only update patch versions",
        };

        Option<string[]> includeOpt = new("--include")
        {
            Description = "Only update packages matching glob pattern (repeatable)",
            AllowMultipleArgumentsPerToken = true,
        };

        Option<string[]> excludeOpt = new("--exclude")
        {
            Description = "Skip packages matching glob pattern (repeatable)",
            AllowMultipleArgumentsPerToken = true,
        };

        Option<string[]> pinMajorOpt = new("--pin-major")
        {
            Description = "Keep major version for matching packages even with --major (repeatable)",
            AllowMultipleArgumentsPerToken = true,
        };

        Option<bool> dryRunOpt = new("--dry-run", "-n")
        {
            Description = "Preview changes without modifying the file",
        };

        Option<string?> sourceOpt = new("--source")
        {
            Description = "NuGet V3 feed URL (default: nuget.org)",
        };

        RootCommand root = new("Update NuGet packages in Directory.Packages.props")
        {
            pathArg,
            majorOpt,
            patchOnlyOpt,
            includeOpt,
            excludeOpt,
            pinMajorOpt,
            dryRunOpt,
            sourceOpt,
        };

        root.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var path = parseResult.GetValue(pathArg);
            var major = parseResult.GetValue(majorOpt);
            var patchOnly = parseResult.GetValue(patchOnlyOpt);
            var include = parseResult.GetValue(includeOpt) ?? [];
            var exclude = parseResult.GetValue(excludeOpt) ?? [];
            var pinMajor = parseResult.GetValue(pinMajorOpt) ?? [];
            var dryRun = parseResult.GetValue(dryRunOpt);
            var source = parseResult.GetValue(sourceOpt);

            if (major && patchOnly)
            {
                ConsoleReporter.Error("Cannot use --major and --patch-only together.");
                return 1;
            }

            if (include.Length > 0 && exclude.Length > 0)
            {
                ConsoleReporter.Error("Cannot use --include and --exclude together.");
                return 1;
            }

            var options = new UpdateOptions
            {
                VersionMode = patchOnly ? VersionMode.PatchOnly
                    : major ? VersionMode.Major
                    : VersionMode.Minor,
                IncludePatterns = include,
                ExcludePatterns = exclude,
                PinMajorPatterns = pinMajor,
                DryRun = dryRun,
            };

            return await PackageUpdater.RunAsync(path, options, source, ct);
        });

        return root;
    }
}
