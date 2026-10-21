---
name: update-packages
description: >-
  Brings CourtFlow NuGet packages under src/ to the latest stable version
  using Central Package Management. Use when the user asks to update NuGet
  packages, bring packages up to date, or run update-packages.
---

# Update packages

Bring every package declared in `src/Directory.Packages.props` to the latest stable version. Latest stable is the highest nuget.org version that NuGet does not mark prerelease.

## Steps

1. Read `src/Directory.Packages.props` and the projects under `src/`.
2. Run `dotnet list src/CourtFlow.slnx package --outdated`.
3. Run `dotnet list src/CourtFlow.slnx package --vulnerable --include-transitive`. Every severity counts. Direct and transitive packages both count.
4. Apply each newer stable version that is not a breaking change. A higher major version is a breaking change. So is a latest stable that does not support `net10.0`.
5. Ask about one package at a time when the latest stable is a breaking change. Name the package, the declared version, and the latest stable, then wait. Do not apply that update, skip it, or open a pull request before the answer. Then do what the developer said for that package. A skip is not stored. The next run asks again.
6. A same-major update that fails the build or the existing tests is a breaking change. Stop and ask how to proceed. Do not rewrite product behavior unless the developer says to.
7. When the latest stable, or a transitive package still present after direct packages are current, has an advisory, name the package, whether it is direct or transitive, and the advisory, then wait. Do not downgrade, add a package, turn on transitive pinning, or add `NoWarn` unless the developer chooses that.
8. When `Aspire.Hosting.AppHost` changes, set `Sdk Name="Aspire.AppHost.Sdk"` in `src/CourtFlow.AppHost/CourtFlow.AppHost.csproj` to that same version.

The skill does not add a package id. It does not edit `tools/logo/` or `TargetFramework`. It does not take a prerelease. When nothing is behind, say so and still report advisories.
