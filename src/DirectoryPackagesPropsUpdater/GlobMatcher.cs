namespace DirectoryPackagesPropsUpdater;

static class GlobMatcher
{
    public static bool Matches(string pattern, string value)
    {
        int pi = 0, vi = 0;
        int starPi = -1, starVi = -1;

        while (vi < value.Length)
        {
            if (pi < pattern.Length &&
                (pattern[pi] == '?' ||
                 char.ToLowerInvariant(pattern[pi]) == char.ToLowerInvariant(value[vi])))
            {
                pi++;
                vi++;
            }
            else if (pi < pattern.Length && pattern[pi] == '*')
            {
                starPi = pi++;
                starVi = vi;
            }
            else if (starPi >= 0)
            {
                pi = starPi + 1;
                vi = ++starVi;
            }
            else
            {
                return false;
            }
        }

        while (pi < pattern.Length && pattern[pi] == '*')
        {
            pi++;
        }

        return pi == pattern.Length;
    }

    public static bool MatchesAny(IReadOnlyList<string> patterns, string value)
    {
        for (int i = 0; i < patterns.Count; i++)
        {
            if (Matches(patterns[i], value))
            {
                return true;
            }
        }

        return false;
    }
}
