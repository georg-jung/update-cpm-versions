namespace DirectoryPackagesPropsUpdater;

enum VersionMode
{
    Minor,
    PatchOnly,
    Major,
}

sealed record UpdateOptions
{
    public VersionMode VersionMode { get; init; }
    public string[] IncludePatterns { get; init; } = [];
    public string[] ExcludePatterns { get; init; } = [];
    public string[] PinMajorPatterns { get; init; } = [];
    public bool DryRun { get; init; }
}
