# Trash Day Admin Menu

A host-only admin menu for [Trash Day](https://store.steampowered.com/app/3314580/), opened with F1. See [thunderstore/README.md](thunderstore/README.md) for what it does.

## Building

- Install BepInEx 5 into the game folder, then run `dotnet build src/AdminMenu/AdminMenu.csproj -c Release`. The build deploys the DLL to `BepInEx/plugins/darkharasho-TrashDayAdminMenu/`.
- Without the game installed, the build uses the reference assemblies in `lib/refs` (regenerate with `scripts/update-refs.sh`).
- Tests: `dotnet test tests/AdminMenu.Tests`.
- Package: `scripts/package.sh` writes `dist/darkharasho-TrashDayAdminMenu-<version>.zip`.

## Releasing

Bump the version in `src/AdminMenu/Plugin.cs`, `src/AdminMenu/AdminMenu.csproj`, `thunderstore/manifest.json` and `CHANGELOG.md`, then push the tag `v<version>`. The release workflow builds, attaches the zip to a GitHub release and publishes to Thunderstore.
