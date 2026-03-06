using NuGet.Versioning;

namespace DirectoryPackagesPropsUpdater;

enum UpdateKind
{
    None,
    Patch,
    Minor,
    Major,
}

record PackageUpdate(string Id, NuGetVersion Current, NuGetVersion New, UpdateKind Kind);

record SkippedUpdate(string Id, NuGetVersion Current, NuGetVersion Available, UpdateKind Kind, string Reason);

static class ConsoleReporter
{
    public static void Info(string message) => Console.WriteLine(message);

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    public static void Report(
        List<PackageUpdate> updates,
        List<SkippedUpdate> skipped,
        bool dryRun,
        string filePath)
    {
        Console.WriteLine();

        if (updates.Count == 0 && skipped.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  All packages are up to date.");
            Console.ResetColor();
            return;
        }

        if (updates.Count > 0)
        {
            var grouped = updates
                .OrderBy(u => u.Kind)
                .ThenBy(u => u.Id, StringComparer.OrdinalIgnoreCase)
                .GroupBy(u => u.Kind);

            var maxIdLen = updates.Max(u => u.Id.Length);

            foreach (var group in grouped)
            {
                var color = KindColor(group.Key);
                Console.ForegroundColor = color;
                Console.WriteLine($"  {group.Key} updates:");
                Console.ResetColor();

                foreach (var u in group)
                {
                    Console.Write($"    {u.Id.PadRight(maxIdLen)}  ");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(u.Current);
                    Console.ResetColor();
                    Console.Write(" \u2192 ");
                    Console.ForegroundColor = color;
                    Console.WriteLine(u.New);
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }

        if (skipped.Count > 0)
        {
            var maxIdLen = skipped.Max(s => s.Id.Length);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Skipped (out of scope):");
            foreach (var s in skipped.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
            {
                Console.Write($"    {s.Id.PadRight(maxIdLen)}  ");
                Console.Write($"{s.Current} \u2192 {s.Available}");
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  ({s.Reason})");
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }

            Console.ResetColor();
            Console.WriteLine();
        }

        if (updates.Count > 0)
        {
            if (dryRun)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(
                    $"  {updates.Count} update(s) available. Run without --dry-run to apply.");
                Console.ResetColor();
            }
            else
            {
                var fileName = Path.GetFileName(filePath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  \u2713 Updated {updates.Count} package(s) in {fileName}");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  Next steps:");
                Console.WriteLine("    dotnet restore");
                Console.WriteLine(
                    $"    git add {fileName} && git commit -m \"chore(deps): update NuGet packages\"");
                Console.ResetColor();
            }
        }
    }

    private static ConsoleColor KindColor(UpdateKind kind) => kind switch
    {
        UpdateKind.Major => ConsoleColor.Red,
        UpdateKind.Minor => ConsoleColor.Yellow,
        UpdateKind.Patch => ConsoleColor.Green,
        _ => ConsoleColor.White,
    };
}
