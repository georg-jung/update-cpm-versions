# <img src="https://raw.githubusercontent.com/georg-jung/DirectoryPackagesPropsUpdater/master/logo.svg" alt="DirectoryPackagesPropsUpdater logo" width="36" /> DirectoryPackagesPropsUpdater

[![Build Status](https://github.com/georg-jung/DirectoryPackagesPropsUpdater/actions/workflows/ci.yml/badge.svg)](https://github.com/georg-jung/DirectoryPackagesPropsUpdater/actions/workflows/ci.yml) [![NuGet version (DirectoryPackagesPropsUpdater)](https://img.shields.io/nuget/v/DirectoryPackagesPropsUpdater.svg?style=flat)](https://www.nuget.org/packages/DirectoryPackagesPropsUpdater/) [![codecov](https://codecov.io/gh/georg-jung/DirectoryPackagesPropsUpdater/graph/badge.svg)](https://app.codecov.io/gh/georg-jung/DirectoryPackagesPropsUpdater)

A .NET tool that checks for outdated NuGet packages in `Directory.Packages.props` and updates them. Like [dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated), but designed for [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) -- it just works directly on the props file instead of applying dependency resolution approaches.

## Installation

```shell
dotnet tool install -g DirectoryPackagesPropsUpdater
```

## Usage

```shell
# Update packages in the current directory (finds Directory.Packages.props automatically)
dotnet update-cpm-versions

# Preview changes without modifying the file
dotnet update-cpm-versions --dry-run

# Specify a path
dotnet update-cpm-versions ./path/to/directory
dotnet update-cpm-versions ./path/to/Directory.Packages.props

# Allow major version updates
dotnet update-cpm-versions --major

# Only update patch versions
dotnet update-cpm-versions --patch-only
```

## Filtering

Use `--include` or `--exclude` (mutually exclusive) with glob patterns to control which packages are in scope:

```shell
# Only update Microsoft packages
dotnet update-cpm-versions --include "Microsoft.*"

# Update everything except Contoso packages
dotnet update-cpm-versions --exclude "Contoso.*"

# Multiple patterns
dotnet update-cpm-versions --exclude "Contoso.*" --exclude "Legacy.*"
```

Use `--pin-major` with `--major` to allow major updates globally but keep specific packages on their current major version:

```shell
# Update all packages including major, but keep EF Core on its current major
dotnet update-cpm-versions --major --pin-major "Microsoft.EntityFrameworkCore.*"
```

## Options

| Option | Description |
| --- | --- |
| `[path]` | Path to `Directory.Packages.props` or its parent directory. Default: current directory (searches parent directories). |
| `--major` | Allow major version updates. Default: only minor/patch updates. |
| `--patch-only` | Only update patch versions. |
| `--include <glob>` | Only update packages matching this glob pattern. Repeatable. |
| `--exclude <glob>` | Skip packages matching this glob pattern. Repeatable. |
| `--pin-major <glob>` | With `--major`, keep matching packages on their current major version. Repeatable. |
| `--dry-run`, `-n` | Preview what would be updated without modifying the file. |
| `--source <url>` | NuGet V3 feed URL. Default: nuget.org. |

## How It Works

1. Finds and parses `Directory.Packages.props` (supports `<PackageVersion>` and `<GlobalPackageReference>` items)
2. Queries the [NuGet V3 flat container API](https://learn.microsoft.com/nuget/api/package-base-address-resource) for each package's available versions (parallelized, up to 16 concurrent requests)
3. Applies SemVer 2 version band logic to determine the best update for each package
4. Writes updated versions back to the file, preserving XML structure

## License

[MIT](LICENSE)
