using NuGet.Versioning;

namespace DirectoryPackagesPropsUpdater;

static class PackageUpdater
{
    public static async Task<int> RunAsync(
        string? path, UpdateOptions options, string? source, CancellationToken ct)
    {
        string filePath;
        try
        {
            filePath = PackagePropsParser.FindFile(path);
        }
        catch (FileNotFoundException ex)
        {
            ConsoleReporter.Error(ex.Message);
            return 1;
        }

        ConsoleReporter.Info(filePath);

        var (doc, packages) = PackagePropsParser.Parse(filePath);
        if (packages.Count == 0)
        {
            ConsoleReporter.Warning("No packages found.");
            return 0;
        }

        ConsoleReporter.Info($"Found {packages.Count} package(s). Checking for updates...");

        var filtered = FilterPackages(packages, options);
        if (filtered.Count == 0)
        {
            ConsoleReporter.Warning("No packages in scope after filtering.");
            return 0;
        }

        Uri? baseUrl = null;
        if (source is not null)
        {
            try
            {
                baseUrl = await NuGetClient.ResolveBaseUrlAsync(source, ct: ct);
            }
            catch (Exception ex)
            {
                ConsoleReporter.Error($"Failed to resolve NuGet source: {ex.Message}");
                return 1;
            }
        }

        using var client = new NuGetClient(baseUrl);
        var allVersions = await client.GetAllVersionsAsync(
            filtered.Select(p => p.Id).ToList(), ct: ct);

        var updates = new List<PackageUpdate>();
        var skipped = new List<SkippedUpdate>();

        foreach (var pkg in filtered)
        {
            if (!allVersions.TryGetValue(pkg.Id, out var versions) || versions.Count == 0)
            {
                continue;
            }

            var stableVersions = versions.Where(v => !v.IsPrerelease).ToList();
            if (stableVersions.Count == 0)
            {
                continue;
            }

            var effectiveMode = options.VersionMode;
            if (effectiveMode == VersionMode.Major
                && GlobMatcher.MatchesAny(options.PinMajorPatterns, pkg.Id))
            {
                effectiveMode = VersionMode.Minor;
            }

            var target = FindBestUpdate(pkg.Version, stableVersions, effectiveMode);
            if (target is not null)
            {
                updates.Add(new PackageUpdate(
                    pkg.Id, pkg.Version, target, ClassifyUpdate(pkg.Version, target)));
            }

            if (effectiveMode != VersionMode.Major)
            {
                var maxAvailable = stableVersions.Max()!;
                if (maxAvailable > (target ?? pkg.Version))
                {
                    var kind = ClassifyUpdate(pkg.Version, maxAvailable);
                    var reason = effectiveMode == VersionMode.PatchOnly
                        ? "minor/major update, use default or --major"
                        : "major update, use --major";
                    skipped.Add(new SkippedUpdate(
                        pkg.Id, pkg.Version, maxAvailable, kind, reason));
                }
            }
        }

        if (!options.DryRun && updates.Count > 0)
        {
            foreach (var update in updates)
            {
                var entry = filtered.First(p =>
                    string.Equals(p.Id, update.Id, StringComparison.OrdinalIgnoreCase));
                PackagePropsParser.UpdateVersion(entry, update.New);
            }

            PackagePropsParser.Save(doc, filePath);
        }

        ConsoleReporter.Report(updates, skipped, options.DryRun, filePath);
        return 0;
    }

    private static List<PackageEntry> FilterPackages(
        List<PackageEntry> packages, UpdateOptions options)
    {
        if (options.IncludePatterns.Length > 0)
        {
            return packages
                .Where(p => GlobMatcher.MatchesAny(options.IncludePatterns, p.Id))
                .ToList();
        }

        if (options.ExcludePatterns.Length > 0)
        {
            return packages
                .Where(p => !GlobMatcher.MatchesAny(options.ExcludePatterns, p.Id))
                .ToList();
        }

        return packages;
    }

    internal static NuGetVersion? FindBestUpdate(
        NuGetVersion current, List<NuGetVersion> available, VersionMode mode)
    {
        var candidates = mode switch
        {
            VersionMode.PatchOnly => available.Where(v =>
                v.Major == current.Major && v.Minor == current.Minor && v > current),
            VersionMode.Minor => available.Where(v =>
                v.Major == current.Major && v > current),
            VersionMode.Major => available.Where(v => v > current),
            _ => [],
        };

        return candidates.OrderByDescending(v => v).FirstOrDefault();
    }

    internal static UpdateKind ClassifyUpdate(NuGetVersion current, NuGetVersion updated)
    {
        if (updated.Major != current.Major)
        {
            return UpdateKind.Major;
        }

        if (updated.Minor != current.Minor)
        {
            return UpdateKind.Minor;
        }

        if (updated.Patch != current.Patch || (current.IsPrerelease && !updated.IsPrerelease))
        {
            return UpdateKind.Patch;
        }

        return UpdateKind.None;
    }
}
