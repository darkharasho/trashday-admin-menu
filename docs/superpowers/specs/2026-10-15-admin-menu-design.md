# Trash Day Admin Menu: design

## Goal

A host-only in-game admin/cheat menu for Trash Day, opened with a hotkey (F1 by default). Its window is polished and matches `stonewards-admin-menu`. Its buttons call the game's own systems: players, items, money, the cart and level flow. Packaged for Thunderstore under `darkharasho` as `AdminMenu`.

## Users and access

- Only the host installs and uses it. Every action runs with server authority on the host.
- Pressing the hotkey when you aren't the server shows a short toast or status message, "Admin menu: host only", and the menu doesn't open.
- Opening is allowed only in a level, meaning when a local `PlayerController` exists. Closing is always allowed.

## Research findings (Trash Day build of 2026-10-13, Unity 6000.3.9f1 Mono; decompiled to /tmp/trashday-src)

### Input and UI

- **Input:** the Input System package. `InputSystem.actions` is the global action asset.
- **Menu state:** `Ox1.MenuSceneManager` owns menu state:
  - `IsMenuOpen` and `AnyMenuVisible` (`ValueNotify<bool>`)
  - `UpdateShowCursor`, which sets `Cursor.lockState`
- **Input blocking:** `PlayerInput` disables `InputSystem.actions` while any menu is visible.
- **Look blocking:** `PlayerCamera.LookInputBlocks` is a request list that blocks look.
- **Precedent:** `ConsoleMenuHider` shows how the game's own console grabs and returns input.

### Cheat code the menu can't rely on

`Trash.TrailerCommands` implements `IEditorOnlyComponent` and may be missing in release scenes, so the menu never relies on it. It holds the game's own cheats:
- invincibility
- money-please
- spawn-item
- time scale

Its static `PlayersInvincible` and `CartInvincible` flags are read by `PlayerHealth.TakeDamage` and `ShoppingCartController.TakeDamage`.

### Players

- `Ox1.OnlinePlayer.All`: `PlayerName` and `OwnerClientId`
- `Trash.PlayerSpawner.TryGetPlayerOwnedBy(clientId, out PlayerController)`
- `PlayerController.Health` (`PlayerHealth`):
  - `IsDead`
  - `TakeDamage(float, Vector3, DamageType)`, server only
  - `FullyHeal()`, server only
  - `ReviveServerRpc()`
- `PlayerController.Teleport(Vector3, Quaternion, float)`. The server sends `TeleportOwnerRpc` for players it doesn't own.

### Money

- `TeamInventory.Instance` is a `RefNotify` singleton.
- `Deposit(float)` is server only.
- `MoneyVar` is a `NetworkVariable<float>`.

### Items

- **Candidates:** `NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs`, filtered to prefabs with an `Item` component.
- **Display name:** `PickupInteractionText.NameKey`, passed through `Loc.Localize(key).Text`. Fall back to the prefab name.
- **Rarity:** `Loot.Rarity`, where present.
- **Spawning:** `ItemSpawnManager.InstantiateRuntimeItem(GameObject prefab, Vector3? pos, Quaternion? rot, ...)`, on the server.

### Cart

- `ShoppingCartController.Instance` (`RefNotify`).
- Moving it:
  - `Teleport(Vector3, Quaternion, float)`
  - `IsTippedOver()`
- Damage and repair:
  - `TakeDamage(float)` (server) breaks random `WagonPanel`s; with no panel left, the cart dies and the game is over.
  - `AccumDamage` has a setter.
  - `Panels` (`WagonPanel[]`) each have `Tier`, `IsBroken` and `Repair(int tier)` (server).
- Wheels: `ShoppingCartWheel.TakeDamage(float)` loosens wheel bolts.

### Level

- `CampaignManager.Instance.CompleteLevel()`, server only
- `CheckpointTrigger.TeleportToNextCheckpoint()`, static and server only
- `TeleportHere` objects (`Name`, `Execute()`), found with `FindObjectsByType`. Each one moves every player and the cart.

## Design

### Window

- A UI Toolkit overlay is added to its own `UIDocument`, with `PanelSettings` created at runtime and a high sort order.
- Styling is inline through a `Theme` class adapted from `stonewards-admin-menu`: dark window, slate controls, gold outline.
- There's a tab rail on the left and the tab's content on the right.
- A status line at the bottom shows the result of the last action, success or error.
- Esc closes it. Handling Esc stops propagation, so the pause menu doesn't open as well.

### Input while open

On open:
- Disable `InputSystem.actions`.
- Add a request to `PlayerCamera.LookInputBlocks`.
- Unlock and show the cursor.
- Re-assert the cursor each frame while open, because `MenuSceneManager.UpdateShowCursor` can reset it.

On close:
- Remove the look block.
- Re-enable actions only if `!MenuSceneManager.AnyMenuVisible`. This mirrors `ConsoleMenuHider`.
- Re-lock the cursor only if `!MenuSceneManager.IsMenuOpen`.

The menu also closes itself when the scene changes or the local player is gone.

### Hotkey

- A `KeyboardShortcut` config entry, default F1.
- It's read through the Input System. The `Hotkey` helper is ported from `stonewards-admin-menu`, which supports both input paths.

### Tabs

1. **Players.** One row per `OnlinePlayer`:
   - name, plus status (alive or dead)
   - **Revive** (`ReviveServerRpc`)
   - **Heal** (`FullyHeal`)
   - **Kill** (`TakeDamage(1000, position)`)
   - **Bring to me**: teleport them to just in front of the host's player
   - **Go to**: teleport the host to them

   The rows refresh when players join, leave or die.

   Above the list is an **Invincible (everyone)** toggle. It's a mod-owned flag; a Harmony prefix on `PlayerHealth.TakeDamage` skips damage while it's on.
2. **Items.**
   - **Search:** a search box over the item catalog, matching case-insensitive substrings of the display name.
   - **List:** shows the name and its rarity.
   - **Spawn:** each row has **Spawn 1 / 5 / 10**. Items appear about 2 m in front of the host's camera, raised slightly, with small random offsets.
   - **Money:** **+$10 / +$100 / +$1000** buttons, plus a custom amount field with **Add** (`TeamInventory.Deposit`).
3. **Cart.**
   - **Bring cart to me:** teleports the cart 3 m in front of the host, upright.
   - **Flip upright:** keeps its position, resets the rotation to yaw only, and lifts it 0.5 m.
   - **Repair:** puts every broken panel back to tier 1 and sets `AccumDamage` to 0.
   - **Invincible cart** toggle: a mod-owned flag, with Harmony prefixes on `ShoppingCartController.TakeDamage` and `ShoppingCartWheel.TakeDamage`.
4. **Level.**
   - **Next checkpoint** and **Complete level**.
   - One button per `TeleportHere` in the current scene, labelled with its `Name`.

### Config (BepInEx)

| Entry | Default |
| --- | --- |
| `General.ToggleKey` | F1 |
| `General.CloseOnEscape` | true |
| `Cheats.InvinciblePlayers` | false (persisted) |
| `Cheats.InvincibleCart` | false (persisted) |

Each entry carries `ConfigurationManagerAttributes`, so the future `trashday-mod-settings` can show it.

### Code layout (mirrors stonewards-admin-menu)

- `Plugin.cs`: config and Harmony
- `AdminMenuController.cs`: a MonoBehaviour that handles the hotkey, open and close, input lock, and auto-close
- `AdminMenuPanel.cs`: the shell (rail, content, status line)
- `Theme.cs`
- `Hotkey.cs`
- `MenuGate.cs`: pure open/close rules
- `ItemCatalog.cs`: builds the catalog from network prefabs
- `ItemSearch.cs`: pure filtering and sorting
- `GameActions.cs`: every game call in one place, each returning a result message
- `PlayersTab.cs`, `ItemsTab.cs`, `CartTab.cs`, `LevelTab.cs`
- `Patches/InvinciblePatches.cs`

### Error handling

- Every button calls `GameActions` through a wrapper that catches exceptions. It logs them, then shows "<action> failed: <message>" in the status line. A game update that breaks one action leaves the rest working.
- If Harmony patching fails, the cause is logged and the invincibility toggles are disabled with a note; the other tabs still work.

## Testing

- An xunit project, `tests/AdminMenu.Tests`, compiles the game-independent files directly, as `stonewards-admin-menu` does:
  - `MenuGate`: open only when the plugin is enabled, you're the server and the player exists; close always.
  - `ItemSearch`: case-insensitive matching, empty query returns all, results sorted by name, duplicate names kept.
- The plugin builds against the game and against `lib/refs`.
- `scripts/package.sh` produces a valid zip.
- In-game manual checklist, kept in `docs/manual-verification-0.1.0.md`: open and close, input lock and release, then each button.

## Out of scope

- Client use, admin delegation, and any networking of the mod's own.
- Time scale, freecam, HUD toggles, changing character, and wheel bolt repair.

## Packaging and release

- Same layout, scripts and workflow as `trashday-more-players`.
- Package `darkharasho-AdminMenu`, GUID `com.darkharasho.trashday.adminmenu`, version 0.1.0.
- Dependency `BepInEx-BepInExPack-5.4.2305`.
- Thunderstore community slug `trash-day` is a placeholder; its publish step uses `continue-on-error` until the community exists.
