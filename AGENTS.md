# Agents Guide

This document captures the project's conventions and style for AI agents and contributors.

## Project Overview

A .NET CLI tool (`dotnet package-update`) that updates NuGet packages in `Directory.Packages.props` files. Queries the NuGet API directly -- no `dotnet restore` needed.

## Tech Stack

- .NET 10, C# with file-scoped namespaces
- System.CommandLine 2.x (stable) for CLI
- NuGet.Versioning for SemVer handling
- TUnit for testing
- Central Package Management (Directory.Packages.props) for our own dependencies

## Code Style

- 4-space indentation for C#, 2-space for XML/JSON (see `.editorconfig`)
- File-scoped namespaces (`namespace Foo;`)
- Prefer `var` when the type is apparent from the right side
- Private fields: `_camelCase`, private static fields: `s_camelCase`
- Keep files focused and small -- flat structure over deep folder hierarchies
- Minimal abstractions: no interfaces or DI unless actually needed
- No unnecessary comments, docstrings, or annotations
- Lean code: prefer fewer files with clear names over many tiny files

## Architecture

- `Program.cs` -- CLI entry point (System.CommandLine setup)
- `PackagePropsParser.cs` -- XML parsing for Directory.Packages.props
- `NuGetClient.cs` -- NuGet API client (flat container endpoint)
- `PackageUpdater.cs` -- orchestration and version logic
- `ConsoleReporter.cs` -- formatted terminal output
- `GlobMatcher.cs` -- simple glob pattern matching (*, ?)

## Testing

- TUnit-based tests in `test/DirectoryPackagesPropsUpdater.Tests/`
- Run via `dotnet run --project test/DirectoryPackagesPropsUpdater.Tests`
- Tests use temp files for parser tests, pure functions for version logic
- `[Arguments]` attribute for parameterized tests
- Async assertions: `await Assert.That(x).IsEqualTo(y)`

## Key Decisions

- No `dotnet restore` dependency -- queries NuGet API directly
- Default: minor-only updates (stay in major band, per SemVer 2)
- `--include`/`--exclude` are mutually exclusive
- `--pin-major` only meaningful with `--major`
- Pre-release versions are filtered out of update candidates
- Parallel NuGet API calls (16 concurrent) since there's no batch API

## Commit Style

- Conventional commits: `feat:`, `fix:`, `chore:`, `test:`, `docs:`
- Keep commits focused and atomic
