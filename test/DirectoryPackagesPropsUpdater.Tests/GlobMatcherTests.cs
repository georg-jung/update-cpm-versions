using DirectoryPackagesPropsUpdater;

namespace DirectoryPackagesPropsUpdater.Tests;

public class GlobMatcherTests
{
    [Test]
    [Arguments("*", "Anything", true)]
    [Arguments("*", "", true)]
    [Arguments("Microsoft.*", "Microsoft.Extensions.Logging", true)]
    [Arguments("Microsoft.*", "Serilog", false)]
    [Arguments("Microsoft.EntityFrameworkCore.*", "Microsoft.EntityFrameworkCore.SqlServer", true)]
    [Arguments("Microsoft.EntityFrameworkCore.*", "Microsoft.Extensions.Logging", false)]
    [Arguments("Rebex.*", "Rebex.Sftp", true)]
    [Arguments("Rebex.*", "rebex.sftp", true)]
    [Arguments("*.Abstractions", "Microsoft.Extensions.Logging.Abstractions", true)]
    [Arguments("*.Abstractions", "Microsoft.Extensions.Logging", false)]
    [Arguments("Exact.Match", "Exact.Match", true)]
    [Arguments("Exact.Match", "Exact.Match.Extra", false)]
    [Arguments("?oo", "Foo", true)]
    [Arguments("?oo", "Foobar", false)]
    public async Task Matches_ReturnsExpected(string pattern, string value, bool expected)
    {
        var result = GlobMatcher.Matches(pattern, value);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task MatchesAny_ReturnsTrueWhenAnyPatternMatches()
    {
        string[] patterns = ["Microsoft.*", "Serilog.*"];
        await Assert.That(GlobMatcher.MatchesAny(patterns, "Microsoft.Extensions.Logging")).IsTrue();
        await Assert.That(GlobMatcher.MatchesAny(patterns, "Serilog.Sinks.Console")).IsTrue();
        await Assert.That(GlobMatcher.MatchesAny(patterns, "Newtonsoft.Json")).IsFalse();
    }

    [Test]
    public async Task MatchesAny_EmptyPatterns_ReturnsFalse()
    {
        await Assert.That(GlobMatcher.MatchesAny([], "Anything")).IsFalse();
    }

    [Test]
    public async Task Matches_IsCaseInsensitive()
    {
        await Assert.That(GlobMatcher.Matches("microsoft.*", "Microsoft.Extensions")).IsTrue();
        await Assert.That(GlobMatcher.Matches("MICROSOFT.*", "microsoft.extensions")).IsTrue();
    }
}
