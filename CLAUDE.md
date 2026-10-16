## Project Context

A BepInEx/Harmony mod for Trash Day (Off by One Games, Steam app 3314580, Unity 6000.3 Mono, Netcode for GameObjects, Input System) that adds a host-only admin menu on F1. The window is UI Toolkit in its own `UIDocument`, with panel settings cloned from a game document. While open it takes input the way the game's console does (`Trash.ConsoleMenuHider`): `InputSystem.actions` off, `MenuSceneManager.MenusSuppressed` on (that's what keeps Esc from opening the pause menu), plus a `PlayerCamera.LookInputBlocks` request and a free cursor. Packaged for Thunderstore under `darkharasho`.

## Layout

- `src/AdminMenu/Plugin.cs`: config, Harmony, creates the controller
- `src/AdminMenu/AdminMenuController.cs`: hotkey, open/close, input lock, auto-close
- `src/AdminMenu/AdminMenuPanel.cs`, `Theme.cs`, `Ui.cs`: the window; `*Tab.cs`: the four tabs
- `src/AdminMenu/GameActions.cs`, `ItemCatalog.cs`: every call into the game
- `src/AdminMenu/MenuGate.cs`, `ItemSearch.cs`, `Amounts.cs`: pure logic, tested by `tests/AdminMenu.Tests`
- `src/AdminMenu/Patches/InvinciblePatches.cs`: invincibility prefixes
- `scripts/package.sh` builds the Thunderstore zip; `scripts/update-refs.sh` regenerates `lib/refs` for CI
- Release: bump the version in `Plugin.cs`, the csproj, `thunderstore/manifest.json` and `CHANGELOG.md`, then push tag `v<version>`

## Notes

- Decompile with `DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec ~/.dotnet/tools/ilspycmd -p -o /tmp/trashday-src .../Trash_Data/Managed/Assembly-CSharp.dll`.
- `Trash.TrailerCommands` and `DebugSpawnable` are editor-only components and may be stripped from release builds; don't rely on them.
- Thunderstore had no Trash Day community at first release. The workflow's `--community trash-day` is a placeholder; set the real slug and `--categories` once it exists, and remove the step's `continue-on-error`.
- Sibling mods: `trashday-more-players`, `trashday-mod-settings` (planned). Settings carry `ConfigurationManagerAttributes` so the settings menu can show them.
