# Admin Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A host-only, F1-toggled admin/cheat menu for Trash Day (players, items and money, cart, level), packaged for Thunderstore as `darkharasho-AdminMenu`.

**Architecture:** A BepInEx 5 plugin creates one `DontDestroyOnLoad` `AdminMenuController` MonoBehaviour. It reads the hotkey, decides open/close through the pure `MenuGate`, locks and releases the game's input, and hosts a UI Toolkit window in its own `UIDocument` (panel settings cloned from a game document so fonts and theme carry over). Tabs call `GameActions`/`ItemCatalog`, the only files that touch game types, through `AdminMenuPanel.Run`, which turns every result or exception into the status line. Invincibility is two config flags read by Harmony prefixes.

**Tech Stack:** C# netstandard2.1, BepInEx 5.4 (Mono), HarmonyX, Unity 6000.3 UI Toolkit, Unity Input System, Netcode for GameObjects; xunit on net8.0 for pure logic.

**Spec:** `docs/superpowers/specs/2026-10-15-admin-menu-design.md`

## Global Constraints

- Package `darkharasho-AdminMenu`, GUID `com.darkharasho.trashday.adminmenu`, plugin name `AdminMenu`, version `0.1.0`.
- Dependency `BepInEx-BepInExPack-5.4.2305`. Thunderstore community slug `trash-day` is a placeholder; the publish step uses `continue-on-error: true`.
- Host-only: every action runs on the host with server authority. Non-hosts pressing the hotkey get "Admin menu: host only" and the menu doesn't open.
- Hotkey `General.ToggleKey` default F1; `General.CloseOnEscape` default true; `Cheats.InvinciblePlayers` and `Cheats.InvincibleCart` default false (persisted). Every entry carries `ConfigurationManagerAttributes`.
- Every button goes through `AdminMenuPanel.Run`, which shows "<action> failed: <message>" in the status line on error; one broken action never breaks the rest.
- If Harmony patching fails, the invincibility toggles are disabled with a note; the other tabs still work.
- Same layout, scripts and workflow as `~/Documents/GitHub/trashday-more-players`.
- The game install is `/var/mnt/data/SteamLibrary/steamapps/common/TrashDay` (BepInEx is already installed there). Decompiled source for reference: `/tmp/trashday-src` (regenerate with `DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec ~/.dotnet/tools/ilspycmd -p -o /tmp/trashday-src /var/mnt/data/SteamLibrary/steamapps/common/TrashDay/Trash_Data/Managed/Assembly-CSharp.dll`).
- Commits are signed through 1Password. Never bypass signing (`--no-gpg-sign`, `-c commit.gpgsign=false`); if a commit fails with a 1Password error, stop and ask the user to unlock it. Every commit message ends with a blank line and `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Don't create the GitHub repo, push, or tag. Those need the user's approval after the final review.
- Work happens on `main` in the new repo `~/Documents/GitHub/trashday-admin-menu` (it only holds the spec and this plan), as with `trashday-more-players`.

## Findings that refine the spec (rulings already made here)

- **Esc and the pause menu.** The pause menu opens from the `OpenMidgameMenu` action of the UI input module's own asset, not from UI Toolkit key events, so stopping event propagation can't block it. The game's console blocks it with `MenuSceneManager.MenusSuppressed = true` (see `Trash/ConsoleMenuHider.cs`), so the menu does the same while open, and after an Esc close it keeps suppression until Esc is released. Esc is read from `Keyboard.current` in the controller, which works whatever has UI focus.
- **Revive = FullyHeal.** `ReviveServerRpc` only calls `FullyHeal()`, and `Heal` clears the dead flag. The menu calls `FullyHeal()` directly on the server for both Revive and Heal (no RPC round trip). Revive is enabled for dead players, Heal for living ones.
- **Kill uses damage 1.** Player damage is a 0–1 fraction (dead at `>= 1`); the game's own `trash.die` deals 1. The menu deals 1 and bypasses its own invincibility patch for that call. The game ignores damage within 1 s of the last hit, so Kill reports when it had no effect.
- **Wheel impact damage.** `ShoppingCartWheel.TakeImpactDamage` is a second wheel damage path, so the invincible cart patches it as well as `TakeDamage`.
- **Custom money parsing** is pure (`Amounts.TryParseMoney`) and unit-tested: accepts `100`, `$1,000`, `12.5`; refuses empty, junk, zero, negative, NaN/infinity and anything above 1,000,000.
- **No "plugin enabled" config entry.** The spec's test list mentions it, but its config table doesn't define one; `MenuGate` therefore has no `pluginEnabled` input.

## Review Focus

1. **Esc closing the menu must not also open the pause menu**, and Esc in the pause menu must still work normally when the admin menu is closed (manual checklist, Task 6; code in Task 5).
2. **Leaving the level or the session with the menu open** (scene change, back to main menu, host quits): the menu closes, gameplay input and the look block are released, and the cursor isn't locked over the main menu (manual checklist; `CloseReason.Lost` in Task 5).
3. **Custom money input with garbage, negative, zero or huge values** is refused with a status message, never deposited (`AmountsTests`, Task 1).
4. **Kill while "Invincible (everyone)" is on, or within the game's 1 s damage cooldown**: the bypass lets Kill through, and a no-effect kill says so instead of claiming success (manual checklist; Task 3).
5. **Opening over a game menu or the console**: the hotkey does nothing while a game menu is open, and closing the admin menu doesn't clear a `MenusSuppressed` the console set (`MenuGateTests.DoesNotOpenOverAGameMenu`, Task 1; `_suppressedMenus` in Task 5).

---

### Task 1: Game-independent logic and its tests

**Files:**
- Create: `src/AdminMenu/MenuGate.cs`
- Create: `src/AdminMenu/ItemSearch.cs`
- Create: `src/AdminMenu/Amounts.cs`
- Create: `tests/AdminMenu.Tests/AdminMenu.Tests.csproj`
- Test: `tests/AdminMenu.Tests/MenuGateTests.cs`, `tests/AdminMenu.Tests/ItemSearchTests.cs`, `tests/AdminMenu.Tests/AmountsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum ToggleDecision { Open, Close, HostOnly, NotInLevel, GameMenuOpen }`
  - `MenuGate.Decide(bool menuOpen, bool isServer, bool hasLocalPlayer, bool gameMenuOpen) -> ToggleDecision`
  - `MenuGate.ShouldAutoClose(bool isServer, bool hasLocalPlayer) -> bool`
  - `MenuGate.ShouldEnableGameInput(bool gameMenuVisible) -> bool`
  - `MenuGate.ShouldLockCursor(bool gameMenuOpen) -> bool`
  - `MenuGate.Message(ToggleDecision) -> string` (null when nothing should be shown)
  - `sealed class ItemEntry { string Name; string Rarity; string PrefabName; }`
  - `ItemSearch.Filter(IEnumerable<ItemEntry> items, string query) -> List<ItemEntry>`
  - `Amounts.MaxMoney = 1_000_000f`, `Amounts.TryParseMoney(string text, out float amount) -> bool`

- [ ] **Step 1: Create the test project**

`tests/AdminMenu.Tests/AdminMenu.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <RollForward>Major</RollForward>
  </PropertyGroup>

  <!-- Only the game-independent logic is tested; it's compiled in directly so no game assemblies are needed. -->
  <ItemGroup>
    <Compile Include="../../src/AdminMenu/MenuGate.cs" Link="MenuGate.cs" />
    <Compile Include="../../src/AdminMenu/ItemSearch.cs" Link="ItemSearch.cs" />
    <Compile Include="../../src/AdminMenu/Amounts.cs" Link="Amounts.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing tests**

`tests/AdminMenu.Tests/MenuGateTests.cs`:

```csharp
using AdminMenu;
using Xunit;

public class MenuGateTests
{
    [Fact]
    public void ClosesWhenOpenWhateverTheState()
    {
        Assert.Equal(ToggleDecision.Close, MenuGate.Decide(menuOpen: true, isServer: false, hasLocalPlayer: false, gameMenuOpen: true));
    }

    [Fact]
    public void OpensForTheHostInALevel()
    {
        Assert.Equal(ToggleDecision.Open, MenuGate.Decide(menuOpen: false, isServer: true, hasLocalPlayer: true, gameMenuOpen: false));
    }

    [Fact]
    public void RefusesClientsInALevelAsHostOnly()
    {
        Assert.Equal(ToggleDecision.HostOnly, MenuGate.Decide(menuOpen: false, isServer: false, hasLocalPlayer: true, gameMenuOpen: false));
    }

    [Fact]
    public void StaysQuietOutsideALevelEvenForClients()
    {
        Assert.Equal(ToggleDecision.NotInLevel, MenuGate.Decide(menuOpen: false, isServer: false, hasLocalPlayer: false, gameMenuOpen: false));
        Assert.Equal(ToggleDecision.NotInLevel, MenuGate.Decide(menuOpen: false, isServer: true, hasLocalPlayer: false, gameMenuOpen: false));
    }

    [Fact]
    public void DoesNotOpenOverAGameMenu()
    {
        Assert.Equal(ToggleDecision.GameMenuOpen, MenuGate.Decide(menuOpen: false, isServer: true, hasLocalPlayer: true, gameMenuOpen: true));
    }

    [Fact]
    public void OnlyHostOnlyShowsAMessage()
    {
        Assert.Equal("Admin menu: host only", MenuGate.Message(ToggleDecision.HostOnly));
        Assert.Null(MenuGate.Message(ToggleDecision.Open));
        Assert.Null(MenuGate.Message(ToggleDecision.Close));
        Assert.Null(MenuGate.Message(ToggleDecision.NotInLevel));
        Assert.Null(MenuGate.Message(ToggleDecision.GameMenuOpen));
    }

    [Fact]
    public void AutoClosesWhenHostingEndsOrThePlayerIsGone()
    {
        Assert.False(MenuGate.ShouldAutoClose(isServer: true, hasLocalPlayer: true));
        Assert.True(MenuGate.ShouldAutoClose(isServer: false, hasLocalPlayer: true));
        Assert.True(MenuGate.ShouldAutoClose(isServer: true, hasLocalPlayer: false));
    }

    [Fact]
    public void HandsGameInputBackOnlyWhenNoGameMenuIsVisible()
    {
        Assert.True(MenuGate.ShouldEnableGameInput(gameMenuVisible: false));
        Assert.False(MenuGate.ShouldEnableGameInput(gameMenuVisible: true));
    }

    [Fact]
    public void LocksTheCursorOnlyWhenNoGameMenuIsOpen()
    {
        Assert.True(MenuGate.ShouldLockCursor(gameMenuOpen: false));
        Assert.False(MenuGate.ShouldLockCursor(gameMenuOpen: true));
    }
}
```

`tests/AdminMenu.Tests/ItemSearchTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using AdminMenu;
using Xunit;

public class ItemSearchTests
{
    private static readonly List<ItemEntry> Items = new List<ItemEntry>
    {
        new ItemEntry { Name = "Wrench", Rarity = "Common", PrefabName = "Item_Wrench" },
        new ItemEntry { Name = "Duct Tape", Rarity = "Uncommon", PrefabName = "Item_DuctTape" },
        new ItemEntry { Name = "Golden Wrench", Rarity = "Rare", PrefabName = "Item_GoldWrench" },
    };

    [Fact]
    public void EmptyOrNullQueryReturnsEverythingSortedByName()
    {
        var expected = new[] { "Duct Tape", "Golden Wrench", "Wrench" };
        Assert.Equal(expected, ItemSearch.Filter(Items, "").Select(i => i.Name));
        Assert.Equal(expected, ItemSearch.Filter(Items, null).Select(i => i.Name));
        Assert.Equal(expected, ItemSearch.Filter(Items, "   ").Select(i => i.Name));
    }

    [Fact]
    public void MatchesSubstringsOfTheNameCaseInsensitively()
    {
        Assert.Equal(new[] { "Golden Wrench", "Wrench" }, ItemSearch.Filter(Items, "WRENCH").Select(i => i.Name));
        Assert.Equal(new[] { "Duct Tape" }, ItemSearch.Filter(Items, "t ta").Select(i => i.Name));
    }

    [Fact]
    public void TrimsTheQuery()
    {
        Assert.Single(ItemSearch.Filter(Items, "  duct  "));
    }

    [Fact]
    public void MatchesTheDisplayNameOnlyNotThePrefabName()
    {
        Assert.Empty(ItemSearch.Filter(Items, "Item_"));
    }

    [Fact]
    public void KeepsDuplicateNames()
    {
        var items = new List<ItemEntry>
        {
            new ItemEntry { Name = "Bolt", PrefabName = "Bolt_B" },
            new ItemEntry { Name = "Bolt", PrefabName = "Bolt_A" },
        };
        var result = ItemSearch.Filter(items, "bolt");
        Assert.Equal(new[] { "Bolt_A", "Bolt_B" }, result.Select(i => i.PrefabName));
    }

    [Fact]
    public void IgnoresNullEntriesAndNames()
    {
        var items = new List<ItemEntry> { null, new ItemEntry { Name = null, PrefabName = "x" }, new ItemEntry { Name = "Saw" } };
        Assert.Equal(new[] { "Saw" }, ItemSearch.Filter(items, "saw").Select(i => i.Name));
        Assert.Equal(2, ItemSearch.Filter(items, "").Count);
        Assert.Empty(ItemSearch.Filter(null, "saw"));
    }
}
```

`tests/AdminMenu.Tests/AmountsTests.cs`:

```csharp
using AdminMenu;
using Xunit;

public class AmountsTests
{
    [Theory]
    [InlineData("100", 100f)]
    [InlineData(" 250 ", 250f)]
    [InlineData("$1,000", 1000f)]
    [InlineData("12.5", 12.5f)]
    [InlineData("1000000", 1000000f)]
    public void ParsesPositiveAmounts(string text, float expected)
    {
        Assert.True(Amounts.TryParseMoney(text, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1000001")]
    [InlineData("1e9")]
    public void RefusesEmptyJunkNonPositiveAndHugeAmounts(string text)
    {
        Assert.False(Amounts.TryParseMoney(text, out _));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd ~/Documents/GitHub/trashday-admin-menu && dotnet test tests/AdminMenu.Tests 2>&1 | tail -15`
Expected: build FAILS with an error that `MenuGate.cs`/`ItemSearch.cs`/`Amounts.cs` can't be found (CS2001 "Source file ... could not be found").

- [ ] **Step 4: Write the implementation**

`src/AdminMenu/MenuGate.cs`:

```csharp
namespace AdminMenu
{
    /// <summary>What pressing the toggle key should do.</summary>
    internal enum ToggleDecision
    {
        Open,
        Close,
        /// <summary>A client in a level: tell them the menu is for the host.</summary>
        HostOnly,
        /// <summary>No local player (main menu, loading): do nothing.</summary>
        NotInLevel,
        /// <summary>A game menu such as pause is open: do nothing.</summary>
        GameMenuOpen,
    }

    /// <summary>
    /// When the menu may open, when it must close itself, and what to hand back to the game on close. Closing is
    /// always allowed, so a scene change or a lost host can never leave the menu stuck open.
    /// </summary>
    internal static class MenuGate
    {
        public static ToggleDecision Decide(bool menuOpen, bool isServer, bool hasLocalPlayer, bool gameMenuOpen)
        {
            if (menuOpen)
                return ToggleDecision.Close;
            if (!hasLocalPlayer)
                return ToggleDecision.NotInLevel;
            if (!isServer)
                return ToggleDecision.HostOnly;
            if (gameMenuOpen)
                return ToggleDecision.GameMenuOpen;
            return ToggleDecision.Open;
        }

        public static string Message(ToggleDecision decision) =>
            decision == ToggleDecision.HostOnly ? "Admin menu: host only" : null;

        public static bool ShouldAutoClose(bool isServer, bool hasLocalPlayer) => !isServer || !hasLocalPlayer;

        /// <summary>Mirrors the game's console: gameplay input comes back only if no game menu wants it off.</summary>
        public static bool ShouldEnableGameInput(bool gameMenuVisible) => !gameMenuVisible;

        /// <summary>A game menu that opened over ours needs the free cursor it already has.</summary>
        public static bool ShouldLockCursor(bool gameMenuOpen) => !gameMenuOpen;
    }
}
```

`src/AdminMenu/ItemSearch.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminMenu
{
    /// <summary>One spawnable item, kept free of game types so the search can be tested without the game.</summary>
    internal sealed class ItemEntry
    {
        public string Name;
        public string Rarity;
        /// <summary>The network prefab's name; a stable tiebreak for items that share a display name.</summary>
        public string PrefabName;
    }

    /// <summary>Filters the item list by display name.</summary>
    internal static class ItemSearch
    {
        public static List<ItemEntry> Filter(IEnumerable<ItemEntry> items, string query)
        {
            var term = (query ?? string.Empty).Trim();
            return (items ?? Enumerable.Empty<ItemEntry>())
                .Where(i => i != null && (term.Length == 0 || Contains(i.Name, term)))
                .OrderBy(i => i.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.PrefabName ?? string.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private static bool Contains(string text, string term) =>
            text != null && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
```

`src/AdminMenu/Amounts.cs`:

```csharp
using System.Globalization;

namespace AdminMenu
{
    /// <summary>Parses the custom money field.</summary>
    internal static class Amounts
    {
        public const float MaxMoney = 1_000_000f;

        /// <summary>Accepts "100", "$1,000" or "12.5"; refuses anything that isn't a positive amount up to <see cref="MaxMoney"/>.</summary>
        public static bool TryParseMoney(string text, out float amount)
        {
            amount = 0f;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var cleaned = text.Trim().TrimStart('$').Replace(",", string.Empty);
            if (!float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return false;
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f || value > MaxMoney)
                return false;
            amount = value;
            return true;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd ~/Documents/GitHub/trashday-admin-menu && dotnet test tests/AdminMenu.Tests 2>&1 | tail -5`
Expected: `Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30` (9 MenuGate + 6 ItemSearch + 15 Amounts).

- [ ] **Step 6: Commit**

```bash
cd ~/Documents/GitHub/trashday-admin-menu
git add src/AdminMenu/MenuGate.cs src/AdminMenu/ItemSearch.cs src/AdminMenu/Amounts.cs tests/AdminMenu.Tests
git commit -m "feat: menu gate, item search and money parsing with tests

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Plugin project, config and invincibility patches

**Files:**
- Create: `src/AdminMenu/AdminMenu.csproj`
- Create: `src/AdminMenu/Plugin.cs`
- Create: `src/AdminMenu/ConfigurationManagerAttributes.cs`
- Create: `src/AdminMenu/Patches/InvinciblePatches.cs`

**Interfaces:**
- Consumes: nothing from Task 1 at runtime (the pure files compile into the plugin too).
- Produces:
  - `Plugin.Log : ManualLogSource`
  - `Plugin.ToggleKey : ConfigEntry<KeyboardShortcut>`, `Plugin.CloseOnEscape : ConfigEntry<bool>`, `Plugin.InvinciblePlayers : ConfigEntry<bool>`, `Plugin.InvincibleCart : ConfigEntry<bool>`
  - `Plugin.PatchesApplied : bool` (static, false if `PatchAll` threw)
  - `Invincibility.Bypass : bool` (static field; while true, the players patch lets damage through)

- [ ] **Step 1: Create the project file**

`src/AdminMenu/AdminMenu.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>AdminMenu</AssemblyName>
    <RootNamespace>AdminMenu</RootNamespace>
    <Version>0.1.0</Version>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <DebugType>embedded</DebugType>
  </PropertyGroup>

  <!-- Machine-specific paths. Override in Local.props (gitignored) or with -p:GamePath=... -->
  <Import Project="$(MSBuildThisFileDirectory)../../Local.props" Condition="Exists('$(MSBuildThisFileDirectory)../../Local.props')" />

  <!-- There's no r2modman community for Trash Day yet, so BepInEx is installed straight into the game folder. -->
  <PropertyGroup>
    <GamePath Condition="'$(GamePath)' == ''">/var/mnt/data/SteamLibrary/steamapps/common/TrashDay</GamePath>
    <ProfilePath Condition="'$(ProfilePath)' == ''">$(GamePath)</ProfilePath>
    <ManagedPath>$(GamePath)/Trash_Data/Managed</ManagedPath>
    <BepInExCorePath>$(ProfilePath)/BepInEx/core</BepInExCorePath>
    <DeployPath>$(ProfilePath)/BepInEx/plugins/darkharasho-AdminMenu</DeployPath>
  </PropertyGroup>

  <!-- Without the game installed (CI), or with -p:UseRefs=true, build against the reference
       assemblies from scripts/update-refs.sh. -->
  <PropertyGroup Condition="'$(UseRefs)' == 'true' Or !Exists('$(ManagedPath)/Assembly-CSharp.dll')">
    <ManagedPath>$(MSBuildThisFileDirectory)../../lib/refs</ManagedPath>
  </PropertyGroup>
  <PropertyGroup Condition="'$(UseRefs)' == 'true' Or !Exists('$(BepInExCorePath)/BepInEx.dll')">
    <BepInExCorePath>$(MSBuildThisFileDirectory)../../lib/refs</BepInExCorePath>
  </PropertyGroup>

  <!-- Keep in sync with game_dlls/bepinex_dlls in scripts/update-refs.sh. -->
  <ItemGroup>
    <Reference Include="BepInEx" HintPath="$(BepInExCorePath)/BepInEx.dll" Private="false" />
    <Reference Include="0Harmony" HintPath="$(BepInExCorePath)/0Harmony.dll" Private="false" />
    <Reference Include="Assembly-CSharp" HintPath="$(ManagedPath)/Assembly-CSharp.dll" Private="false" />
    <Reference Include="UnityEngine" HintPath="$(ManagedPath)/UnityEngine.dll" Private="false" />
    <Reference Include="UnityEngine.CoreModule" HintPath="$(ManagedPath)/UnityEngine.CoreModule.dll" Private="false" />
    <Reference Include="UnityEngine.TextRenderingModule" HintPath="$(ManagedPath)/UnityEngine.TextRenderingModule.dll" Private="false" />
    <Reference Include="UnityEngine.UIElementsModule" HintPath="$(ManagedPath)/UnityEngine.UIElementsModule.dll" Private="false" />
    <Reference Include="UnityEngine.PhysicsModule" HintPath="$(ManagedPath)/UnityEngine.PhysicsModule.dll" Private="false" />
    <Reference Include="Unity.InputSystem" HintPath="$(ManagedPath)/Unity.InputSystem.dll" Private="false" />
    <Reference Include="Unity.Netcode.Runtime" HintPath="$(ManagedPath)/Unity.Netcode.Runtime.dll" Private="false" />
  </ItemGroup>

  <Target Name="DeployToProfile" AfterTargets="Build" Condition="Exists('$(ProfilePath)/BepInEx/plugins')">
    <MakeDir Directories="$(DeployPath)" />
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(DeployPath)" />
    <Message Importance="high" Text="Deployed $(TargetFileName) to $(DeployPath)" />
  </Target>

</Project>
```

If a later build fails with CS0012 ("The type 'X' is defined in an assembly that is not referenced. You must add a reference to assembly 'Y'"), add `<Reference Include="Y" HintPath="$(ManagedPath)/Y.dll" Private="false" />` here and add `Y` to `game_dlls` in `scripts/update-refs.sh` (Task 6). That's expected, not a plan deviation, but ledger it as a ruling.

- [ ] **Step 2: Write `ConfigurationManagerAttributes.cs`**

```csharp
namespace AdminMenu
{
    /// <summary>
    /// Tag for a setting's <see cref="BepInEx.Configuration.ConfigDescription"/> that tells a settings menu
    /// (BepInEx.ConfigurationManager, or the planned darkharasho Trash Day ModSettings) how to show it. Read by
    /// field name, so no reference to that mod is needed and the mod works the same without it.
    /// </summary>
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public int? Order;
    }
}
```

- [ ] **Step 3: Write `Patches/InvinciblePatches.cs`**

```csharp
using HarmonyLib;
using Trash;

namespace AdminMenu.Patches
{
    /// <summary>
    /// The menu's own invincibility. The game has equivalent flags on TrailerCommands, but that component is
    /// editor-only and may be missing from release scenes, so the menu doesn't rely on it.
    /// </summary>
    internal static class Invincibility
    {
        /// <summary>Set around the menu's own Kill so it still works while players are invincible.</summary>
        internal static bool Bypass;

        internal static bool PlayersBlocked => Plugin.InvinciblePlayers.Value && !Bypass;
        internal static bool CartBlocked => Plugin.InvincibleCart.Value;
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.TakeDamage))]
    internal static class InvinciblePlayersPatch
    {
        private static bool Prefix() => !Invincibility.PlayersBlocked;
    }

    // Breaks cart panels and, with none left, ends the run.
    [HarmonyPatch(typeof(ShoppingCartController), nameof(ShoppingCartController.TakeDamage))]
    internal static class InvincibleCartPatch
    {
        private static bool Prefix() => !Invincibility.CartBlocked;
    }

    // Loosens wheel bolts from hazards.
    [HarmonyPatch(typeof(ShoppingCartWheel), nameof(ShoppingCartWheel.TakeDamage))]
    internal static class InvincibleWheelPatch
    {
        private static bool Prefix() => !Invincibility.CartBlocked;
    }

    // Loosens wheel bolts from hard landings and collisions.
    [HarmonyPatch(typeof(ShoppingCartWheel), nameof(ShoppingCartWheel.TakeImpactDamage))]
    internal static class InvincibleWheelImpactPatch
    {
        private static bool Prefix() => !Invincibility.CartBlocked;
    }
}
```

- [ ] **Step 4: Write `Plugin.cs`**

```csharp
using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.trashday.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> CloseOnEscape;
        internal static ConfigEntry<bool> InvinciblePlayers;
        internal static ConfigEntry<bool> InvincibleCart;

        /// <summary>False when patching failed; the invincibility toggles are then disabled.</summary>
        internal static bool PatchesApplied { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F1), new ConfigDescription(
                "Opens and closes the admin menu. Only the host can open it.",
                null,
                new ConfigurationManagerAttributes { DispName = "Toggle key", Order = 4 }));
            CloseOnEscape = Config.Bind("General", "CloseOnEscape", true, new ConfigDescription(
                "Esc closes the admin menu (without opening the pause menu).",
                null,
                new ConfigurationManagerAttributes { DispName = "Close on Esc", Order = 3 }));
            InvinciblePlayers = Config.Bind("Cheats", "InvinciblePlayers", false, new ConfigDescription(
                "Players take no damage. Also on the menu's Players tab.",
                null,
                new ConfigurationManagerAttributes { DispName = "Invincible players", Order = 2 }));
            InvincibleCart = Config.Bind("Cheats", "InvincibleCart", false, new ConfigDescription(
                "The cart, its panels and its wheels take no damage. Also on the menu's Cart tab.",
                null,
                new ConfigurationManagerAttributes { DispName = "Invincible cart", Order = 1 }));

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
                PatchesApplied = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Patching failed, likely a game update; invincibility is unavailable, the rest of the menu still works. {e}");
                _harmony.UnpatchSelf();
            }

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {ToggleKey.Value} while hosting to open the admin menu");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
```

- [ ] **Step 5: Build against the game**

Run: `cd ~/Documents/GitHub/trashday-admin-menu && dotnet build src/AdminMenu/AdminMenu.csproj -c Release 2>&1 | grep -E "error|warn|Deployed|Build succeeded" | head -20`
Expected: `Deployed AdminMenu.dll to /var/mnt/data/SteamLibrary/steamapps/common/TrashDay/BepInEx/plugins/darkharasho-AdminMenu` and `Build succeeded`, 0 errors. (Fix any CS0012 per Step 1's note.)

- [ ] **Step 6: Confirm the tests still pass and commit**

Run: `dotnet test tests/AdminMenu.Tests 2>&1 | tail -1`
Expected: `Passed! ... Passed:    30`

```bash
git add src/AdminMenu/AdminMenu.csproj src/AdminMenu/Plugin.cs src/AdminMenu/ConfigurationManagerAttributes.cs src/AdminMenu/Patches
git commit -m "feat: plugin config and invincibility patches

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Game actions and the item catalog

**Files:**
- Create: `src/AdminMenu/GameActions.cs`
- Create: `src/AdminMenu/ItemCatalog.cs`

**Interfaces:**
- Consumes: `ItemEntry` (Task 1), `Amounts.MaxMoney` (Task 1), `Invincibility.Bypass` (Task 2), `Plugin.Log` (Task 2).
- Produces (every action returns a status message and throws `InvalidOperationException` with a user-facing message when it can't run):
  - `GameActions.IsServer : bool`; `GameActions.LocalPlayer() -> PlayerController` (null if none)
  - `sealed class PlayerInfo { ulong ClientId; string Name; bool HasBody; bool IsDead; bool IsLocal; }`; `GameActions.Players() -> List<PlayerInfo>`
  - `GameActions.Revive(ulong)`, `Heal(ulong)`, `Kill(ulong)`, `BringToMe(ulong)`, `GoTo(ulong)` -> `string`
  - `GameActions.Money() -> float?`; `GameActions.AddMoney(float) -> string`
  - `GameActions.CartSummary() -> string`; `BringCart()`, `FlipCart()`, `RepairCart()` -> `string`
  - `GameActions.NextCheckpoint()`, `CompleteLevel()` -> `string`; `GameActions.TeleportPoints() -> List<string>`; `GameActions.TeleportTo(string) -> string`
  - `GameActions.Toast(string)` (shows a game toast if the HUD has one, always logs)
  - `GameActions.SpawnPoint(float distance) -> Vector3`
  - `ItemCatalog.All() -> IReadOnlyList<ItemEntry>`; `ItemCatalog.Spawn(ItemEntry, int count) -> string`

There's no unit test for this task: it's all game calls. Its gate is a clean build against the game, and the manual checklist in Task 6 covers each action.

- [ ] **Step 1: Write `GameActions.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AdminMenu.Patches;
using Ox1;
using Trash;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AdminMenu
{
    /// <summary>One connected player as the Players tab shows it.</summary>
    internal sealed class PlayerInfo
    {
        public ulong ClientId;
        public string Name;
        public bool HasBody;
        public bool IsDead;
        public bool IsLocal;
    }

    /// <summary>
    /// Every call into the game, in one place. Each action returns the message for the status line, or throws
    /// <see cref="InvalidOperationException"/> with a message the player can act on. All of them assume the host.
    /// </summary>
    internal static class GameActions
    {
        public static bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        public static PlayerController LocalPlayer()
        {
            foreach (var player in PlayerController.All)
            {
                if (player != null && player.IsOwner)
                    return player;
            }
            return null;
        }

        public static void Toast(string message)
        {
            Plugin.Log.LogInfo(message);
            if (ToastUI.Instance.TryGet(out var toast) && toast != null)
                toast.ShowToast(message);
        }

        // ---- Players ----

        public static List<PlayerInfo> Players()
        {
            var local = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            var result = new List<PlayerInfo>();
            foreach (var online in OnlinePlayer.All)
            {
                if (online == null)
                    continue;
                var id = online.OwnerClientId;
                var name = online.PlayerName.Value;
                var info = new PlayerInfo
                {
                    ClientId = id,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Player {id}" : name,
                    IsLocal = id == local,
                };
                if (PlayerSpawner.TryGetPlayerOwnedBy(id, out var body) && body != null)
                {
                    info.HasBody = true;
                    info.IsDead = body.IsDead;
                }
                result.Add(info);
            }
            return result.OrderBy(p => p.IsLocal ? 0 : 1).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ReviveServerRpc only calls FullyHeal, and healing clears the dead flag, so both buttons use FullyHeal.
        public static string Revive(ulong clientId)
        {
            var player = RequireBody(clientId, out var name);
            if (!player.IsDead)
                throw new InvalidOperationException($"{name} isn't dead");
            player.Health.FullyHeal();
            return $"Revived {name}";
        }

        public static string Heal(ulong clientId)
        {
            var player = RequireBody(clientId, out var name);
            if (player.IsDead)
                throw new InvalidOperationException($"{name} is dead; use Revive");
            player.Health.FullyHeal();
            return $"Healed {name}";
        }

        // Damage is a 0-1 fraction; 1 kills, as the game's own trash.die does.
        public static string Kill(ulong clientId)
        {
            var player = RequireBody(clientId, out var name);
            if (player.IsDead)
                throw new InvalidOperationException($"{name} is already dead");
            Invincibility.Bypass = true;
            try
            {
                player.Health.TakeDamage(1f, player.transform.position + Vector3.up);
            }
            finally
            {
                Invincibility.Bypass = false;
            }
            return player.IsDead
                ? $"Killed {name}"
                : $"{name} took no damage (the game ignores hits within a second of the last one); try again";
        }

        public static string BringToMe(ulong clientId)
        {
            var host = RequireLocalPlayer();
            var player = RequireBody(clientId, out var name);
            if (player == host)
                throw new InvalidOperationException("that's you");
            var forward = ViewForward();
            player.Teleport(host.transform.position + forward * 1.5f + Vector3.up * 0.5f, Quaternion.LookRotation(-forward));
            return $"Brought {name} to you";
        }

        public static string GoTo(ulong clientId)
        {
            var host = RequireLocalPlayer();
            var player = RequireBody(clientId, out var name);
            if (player == host)
                throw new InvalidOperationException("that's you");
            var forward = Flat(player.transform.forward);
            host.Teleport(player.transform.position - forward * 1.5f + Vector3.up * 0.5f, Quaternion.LookRotation(forward));
            return $"Went to {name}";
        }

        // ---- Money ----

        public static float? Money() =>
            TeamInventory.Instance.TryGet(out var inventory) && inventory != null ? inventory.MoneyVar.Value : (float?)null;

        public static string AddMoney(float amount)
        {
            if (amount <= 0f || amount > Amounts.MaxMoney)
                throw new InvalidOperationException($"amount must be between 1 and {Amounts.MaxMoney:N0}");
            if (!TeamInventory.Instance.TryGet(out var inventory) || inventory == null)
                throw new InvalidOperationException("no team money in this level");
            inventory.Deposit(amount);
            return $"Added ${amount:N0} (team now has ${inventory.MoneyVar.Value:N0})";
        }

        // ---- Cart ----

        public static string CartSummary()
        {
            if (!TryGetCart(out var cart))
                return "No cart in this level.";
            var panels = cart.Panels ?? Array.Empty<WagonPanel>();
            var intact = panels.Count(p => p != null && !p.IsBroken);
            var tipped = cart.IsTippedOver() ? ", tipped over" : string.Empty;
            return $"Cart: {intact}/{panels.Length} panels intact, damage {cart.AccumDamage:P0}{tipped}";
        }

        public static string BringCart()
        {
            var host = RequireLocalPlayer();
            var cart = RequireCart();
            var forward = ViewForward();
            cart.Teleport(host.transform.position + forward * 3f + Vector3.up * 0.5f, Quaternion.LookRotation(forward));
            return "Brought the cart to you";
        }

        public static string FlipCart()
        {
            var cart = RequireCart();
            var yaw = cart.transform.eulerAngles.y;
            cart.Teleport(cart.transform.position + Vector3.up * 0.5f, Quaternion.Euler(0f, yaw, 0f));
            return "Flipped the cart upright";
        }

        public static string RepairCart()
        {
            var cart = RequireCart();
            var repaired = 0;
            foreach (var panel in cart.Panels ?? Array.Empty<WagonPanel>())
            {
                if (panel == null || !panel.IsBroken)
                    continue;
                panel.Repair(1);
                repaired++;
            }
            cart.AccumDamage = 0f;
            return repaired == 0 ? "No broken panels; cleared cart damage" : $"Repaired {repaired} panel(s) and cleared cart damage";
        }

        // ---- Level ----

        public static string NextCheckpoint()
        {
            var checkpoints = Object.FindObjectsByType<CheckpointTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (checkpoints.Length == 0)
                throw new InvalidOperationException("no checkpoints in this level");
            if (checkpoints.All(c => c == null || c.IsReached))
                throw new InvalidOperationException("every checkpoint is already reached");
            CheckpointTrigger.TeleportToNextCheckpoint();
            return "Teleported everyone to the next checkpoint";
        }

        public static string CompleteLevel()
        {
            if (!CampaignManager.Instance.TryGet(out var campaign) || campaign == null)
                throw new InvalidOperationException("no campaign running in this scene");
            campaign.CompleteLevel();
            return "Completing the level";
        }

        public static List<string> TeleportPoints() =>
            Object.FindObjectsByType<TeleportHere>(FindObjectsSortMode.None)
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name))
                .Select(t => t.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public static string TeleportTo(string name)
        {
            var point = Object.FindObjectsByType<TeleportHere>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t != null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (point == null)
                throw new InvalidOperationException($"teleport point {name} is gone");
            point.Execute();
            return $"Teleported everyone and the cart to {name}";
        }

        // ---- Placement ----

        /// <summary>A point <paramref name="distance"/> metres ahead of the host's view, level with the camera.</summary>
        public static Vector3 SpawnPoint(float distance)
        {
            if (LocalCamera.Instance.TryGet(out var camera) && camera != null)
                return camera.Position + ViewForward() * distance;
            var host = RequireLocalPlayer();
            return host.transform.position + ViewForward() * distance + Vector3.up;
        }

        /// <summary>Where the host is looking, flattened to the ground plane.</summary>
        private static Vector3 ViewForward()
        {
            if (LocalCamera.Instance.TryGet(out var camera) && camera != null)
                return Flat(camera.transform.forward);
            var host = LocalPlayer();
            return host != null ? Flat(host.transform.forward) : Vector3.forward;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }

        private static PlayerController RequireLocalPlayer() =>
            LocalPlayer() ?? throw new InvalidOperationException("you have no player right now");

        private static PlayerController RequireBody(ulong clientId, out string name)
        {
            var online = OnlinePlayer.All.FirstOrDefault(p => p != null && p.OwnerClientId == clientId);
            name = online != null && !string.IsNullOrWhiteSpace(online.PlayerName.Value) ? online.PlayerName.Value : $"Player {clientId}";
            if (!PlayerSpawner.TryGetPlayerOwnedBy(clientId, out var player) || player == null)
                throw new InvalidOperationException($"{name} has no character right now");
            return player;
        }

        private static bool TryGetCart(out ShoppingCartController cart) =>
            ShoppingCartController.Instance.TryGet(out cart) && cart != null;

        private static ShoppingCartController RequireCart() =>
            TryGetCart(out var cart) ? cart : throw new InvalidOperationException("no cart in this level");
    }
}
```

Note: `OnlinePlayer.All` is a `RuntimeList<T>` that implements `IEnumerable<T>`, so `FirstOrDefault` works. If it doesn't compile because LINQ can't see `IEnumerable<OnlinePlayer>`, iterate `OnlinePlayer.All.Items` (a `List<T>`) instead and ledger the ruling.

- [ ] **Step 2: Write `ItemCatalog.cs`**

```csharp
using System;
using System.Collections.Generic;
using Ox1;
using Trash;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AdminMenu
{
    /// <summary>
    /// Every network prefab with an <see cref="Item"/> component, named the way the game names it when you look
    /// at it. Built on first use and rebuilt when the network manager or its prefab list changes.
    /// </summary>
    internal static class ItemCatalog
    {
        private static readonly Dictionary<ItemEntry, GameObject> Prefabs = new Dictionary<ItemEntry, GameObject>();
        private static List<ItemEntry> _entries = new List<ItemEntry>();
        private static NetworkManager _builtFor;
        private static int _builtCount = -1;

        public static IReadOnlyList<ItemEntry> All()
        {
            var manager = NetworkManager.Singleton;
            var prefabs = manager != null ? manager.NetworkConfig?.Prefabs?.Prefabs : null;
            if (prefabs == null)
                return Array.Empty<ItemEntry>();
            if (manager == _builtFor && prefabs.Count == _builtCount)
                return _entries;

            Prefabs.Clear();
            var entries = new List<ItemEntry>();
            foreach (var networkPrefab in prefabs)
            {
                var prefab = networkPrefab?.Prefab;
                if (prefab == null)
                    continue;
                try
                {
                    if (prefab.GetComponent<Item>() == null)
                        continue;
                    var entry = new ItemEntry { Name = DisplayName(prefab), Rarity = Rarity(prefab), PrefabName = prefab.name };
                    entries.Add(entry);
                    Prefabs[entry] = prefab;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Skipping item prefab {prefab.name}: {e.Message}");
                }
            }

            _entries = entries;
            _builtFor = manager;
            _builtCount = prefabs.Count;
            Plugin.Log.LogInfo($"Item catalog: {entries.Count} items");
            return _entries;
        }

        /// <summary>Spawns <paramref name="count"/> copies a couple of metres in front of the host, slightly scattered.</summary>
        public static string Spawn(ItemEntry entry, int count)
        {
            if (entry == null || !Prefabs.TryGetValue(entry, out var prefab) || prefab == null)
                throw new InvalidOperationException("that item is no longer available; reopen the menu");
            if (!GameActions.IsServer)
                throw new InvalidOperationException("only the host can spawn items");

            var origin = GameActions.SpawnPoint(2f) + Vector3.up * 0.3f;
            for (var i = 0; i < count; i++)
            {
                var offset = new Vector3(Random.Range(-0.3f, 0.3f), i * 0.15f, Random.Range(-0.3f, 0.3f));
                ItemSpawnManager.InstantiateRuntimeItem(prefab, origin + offset, Quaternion.identity);
            }
            return count == 1 ? $"Spawned {entry.Name}" : $"Spawned {count} x {entry.Name}";
        }

        /// <summary>The localized name the pickup prompt shows, or the prefab's name when it has none.</summary>
        private static string DisplayName(GameObject prefab)
        {
            var text = prefab.GetComponentInChildren<PickupInteractionText>(true);
            var key = text != null ? text.NameKey : null;
            if (string.IsNullOrEmpty(key))
                return prefab.name;
            var localized = Loc.Localize(key).Text;
            return string.IsNullOrWhiteSpace(localized) || localized == key ? prefab.name : localized;
        }

        private static string Rarity(GameObject prefab)
        {
            var loot = prefab.GetComponent<Loot>();
            return loot != null ? loot.Rarity.ToString() : string.Empty;
        }
    }
}
```

- [ ] **Step 3: Build against the game**

Run: `cd ~/Documents/GitHub/trashday-admin-menu && dotnet build src/AdminMenu/AdminMenu.csproj -c Release 2>&1 | grep -E "error|warn|Build succeeded" | head -20`
Expected: `Build succeeded`, 0 errors. Fix compile errors against the real signatures in `/tmp/trashday-src` (e.g. `grep -n "public" /tmp/trashday-src/Trash/<Class>.cs`) and ledger each change as a ruling.

- [ ] **Step 4: Confirm the tests still pass and commit**

Run: `dotnet test tests/AdminMenu.Tests 2>&1 | tail -1`
Expected: `Passed! ... Passed:    30`

```bash
git add src/AdminMenu/GameActions.cs src/AdminMenu/ItemCatalog.cs
git commit -m "feat: game actions and item catalog

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The window and its four tabs

**Files:**
- Create: `src/AdminMenu/Theme.cs` (copied verbatim from `stonewards-admin-menu`)
- Create: `src/AdminMenu/Ui.cs`
- Create: `src/AdminMenu/AdminMenuPanel.cs`
- Create: `src/AdminMenu/PlayersTab.cs`, `src/AdminMenu/ItemsTab.cs`, `src/AdminMenu/CartTab.cs`, `src/AdminMenu/LevelTab.cs`

**Interfaces:**
- Consumes: everything `GameActions`/`ItemCatalog` produce (Task 3); `ItemSearch.Filter`, `Amounts.TryParseMoney` (Task 1); `Plugin.InvinciblePlayers`, `Plugin.InvincibleCart`, `Plugin.PatchesApplied`, `Plugin.Log` (Task 2).
- Produces:
  - `AdminMenuPanel` with `Root : VisualElement`, `IsOpen : bool`, `event Action CloseRequested`, `AddTab(string title, VisualElement content, Action onShown)`, `Open()`, `Close()`, `SetStatus(string text, bool error)`, `Run(string action, Func<string> work)`
  - `PlayersTab.Build(AdminMenuPanel, out Action refresh)`, `ItemsTab.Build(...)`, `CartTab.Build(...)`, `LevelTab.Build(...)`, each returning `VisualElement`
  - `Ui.ActionButton`, `Ui.ConfirmButton`, `Ui.Row`, `Ui.Heading`, `Ui.Muted`, `Ui.InvincibleToggle`, `Ui.Error`

No unit tests (UI Toolkit needs the engine); the gate is a clean build and the manual checklist in Task 6.

- [ ] **Step 1: Copy the theme**

Run: `cp ~/Documents/GitHub/stonewards-admin-menu/src/AdminMenu/Theme.cs ~/Documents/GitHub/trashday-admin-menu/src/AdminMenu/Theme.cs && head -20 ~/Documents/GitHub/trashday-admin-menu/src/AdminMenu/Theme.cs | grep -n "namespace AdminMenu"`
Expected: `namespace AdminMenu` is printed (same namespace, no edits needed). Then change the first line of its summary comment from "Inline styles matching the game's menus" to "Inline styles shared with darkharasho's other admin menus" so it doesn't claim to match Trash Day's own look.

- [ ] **Step 2: Write `Ui.cs`**

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>Small building blocks the tabs share.</summary>
    internal static class Ui
    {
        public static readonly Color Error = new Color(1f, 0.45f, 0.4f);

        /// <summary>A themed button that runs <paramref name="work"/> through the panel's status line, then <paramref name="after"/>.</summary>
        public static Button ActionButton(AdminMenuPanel panel, string text, Func<string> work, Action after = null)
        {
            var button = new Button(() =>
            {
                panel.Run(text, work);
                after?.Invoke();
            }) { text = text };
            Theme.StyleButton(button);
            button.style.marginRight = 6f;
            return button;
        }

        /// <summary>Like <see cref="ActionButton"/>, but the first click only arms it for three seconds.</summary>
        public static Button ConfirmButton(AdminMenuPanel panel, string text, Func<string> work)
        {
            Button button = null;
            var armed = false;
            button = new Button(() =>
            {
                if (!armed)
                {
                    armed = true;
                    button.text = $"Click again: {text}";
                    button.schedule.Execute(() => { armed = false; button.text = text; }).ExecuteLater(3000L);
                    return;
                }
                armed = false;
                button.text = text;
                panel.Run(text, work);
            }) { text = text };
            Theme.StyleButton(button);
            button.style.marginRight = 6f;
            return button;
        }

        /// <summary>
        /// A toggle bound to a cheat config entry. Disabled with a note when patching failed, because the flag would
        /// do nothing.
        /// </summary>
        public static Toggle InvincibleToggle(AdminMenuPanel panel, string label, BepInEx.Configuration.ConfigEntry<bool> entry, string onText, string offText)
        {
            var toggle = new Toggle(label) { value = entry.Value };
            toggle.style.marginBottom = 12f;
            if (!Plugin.PatchesApplied)
            {
                toggle.label = $"{label} (unavailable: patching failed, see the BepInEx log)";
                toggle.SetEnabled(false);
                return toggle;
            }
            toggle.RegisterValueChangedCallback(evt =>
            {
                entry.Value = evt.newValue;
                panel.SetStatus(evt.newValue ? onText : offText, false);
            });
            return toggle;
        }

        public static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 10f;
            return row;
        }

        public static Label Heading(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 20f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Theme.Gold;
            label.style.marginTop = 8f;
            label.style.marginBottom = 8f;
            return label;
        }

        public static Label Muted(string text)
        {
            var label = new Label(text);
            label.style.color = Theme.Muted;
            label.style.marginBottom = 8f;
            return label;
        }
    }
}
```

- [ ] **Step 3: Write `AdminMenuPanel.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// The admin menu window: a rail of tab buttons on the left, the selected tab on the right, and a status line
    /// at the bottom that shows how the last action went. Keyboard handling (the hotkey and Esc) lives in
    /// <see cref="AdminMenuController"/>.
    /// </summary>
    internal sealed class AdminMenuPanel
    {
        private readonly VisualElement _overlay;
        private readonly VisualElement _rail;
        private readonly VisualElement _content;
        private readonly Label _status;
        private readonly List<Tab> _tabs = new List<Tab>();

        private Tab _selected;

        public event Action CloseRequested;

        public VisualElement Root => _overlay;
        public bool IsOpen { get; private set; }

        public AdminMenuPanel()
        {
            _overlay = new VisualElement { name = "AdminMenuOverlay" };
            var o = _overlay.style;
            o.position = Position.Absolute;
            o.left = o.right = o.top = o.bottom = 0f;
            o.alignItems = Align.Center;
            o.justifyContent = Justify.Center;
            o.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            o.color = Theme.Text;
            o.display = DisplayStyle.None;

            var window = new VisualElement { name = "AdminMenuWindow" };
            var w = window.style;
            w.width = Length.Percent(72f);
            w.height = Length.Percent(78f);
            w.maxWidth = 1200f;
            w.backgroundColor = Theme.Window;
            Theme.SetBorder(window, 3f, Theme.WindowBorder, 0f);
            Theme.SetPadding(window, 20f, 24f);
            _overlay.Add(window);

            var header = Row();
            header.style.marginBottom = 16f;
            var title = new Label("ADMIN MENU");
            title.style.fontSize = 34f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Theme.Text;
            title.style.textShadow = new TextShadow { offset = new Vector2(2f, 2f), color = Color.black };
            title.style.flexGrow = 1f;
            header.Add(title);

            var close = new Button(() => CloseRequested?.Invoke()) { text = "Close" };
            close.style.marginLeft = 20f;
            close.style.minWidth = 110f;
            Theme.StyleButton(close);
            header.Add(close);
            window.Add(header);

            var body = Row();
            body.style.flexGrow = 1f;
            body.style.flexShrink = 1f;
            body.style.alignItems = Align.Stretch;
            window.Add(body);

            _rail = new VisualElement { name = "AdminMenuTabs" };
            _rail.style.width = 180f;
            _rail.style.flexShrink = 0f;
            _rail.style.marginRight = 20f;
            _rail.style.paddingRight = 16f;
            _rail.style.borderRightWidth = 2f;
            _rail.style.borderRightColor = Theme.Line;
            body.Add(_rail);

            _content = new VisualElement { name = "AdminMenuContent" };
            _content.style.flexGrow = 1f;
            _content.style.flexShrink = 1f;
            body.Add(_content);

            _status = new Label(" ") { name = "AdminMenuStatus" };
            _status.style.marginTop = 12f;
            _status.style.paddingTop = 10f;
            _status.style.borderTopWidth = 2f;
            _status.style.borderTopColor = Theme.Line;
            _status.style.color = Theme.Muted;
            _status.style.whiteSpace = WhiteSpace.Normal;
            window.Add(_status);

            Theme.StyleControls(_overlay);
        }

        /// <summary>
        /// Adds a tab and its content. The first tab added becomes the selected one. The content is styled once,
        /// here: a tab that rebuilds its own children later must call <see cref="Theme.StyleControls"/> on what it
        /// rebuilt, because styling registers hover callbacks and re-running it would stack them up.
        /// </summary>
        public void AddTab(string title, VisualElement content, Action onShown)
        {
            var button = new Button { text = title };
            Theme.StyleButton(button, outlined: false);
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.marginLeft = button.style.marginRight = 0f;
            button.style.marginBottom = 8f;
            button.style.paddingTop = button.style.paddingBottom = 10f;
            _rail.Add(button);

            content.style.display = DisplayStyle.None;
            content.style.flexGrow = 1f;
            _content.Add(content);
            Theme.StyleControls(content);

            var tab = new Tab { Button = button, Content = content, OnShown = onShown };
            _tabs.Add(tab);
            button.clicked += () => Select(tab);

            if (_selected == null)
                Select(tab);
            else
                Highlight();
        }

        public void Open()
        {
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.BringToFront();
            IsOpen = true;
            SetStatus(" ", false);
            // Refresh the visible tab so it never shows data from the last time the menu was open.
            if (_selected != null)
                Show(_selected);
        }

        public void Close()
        {
            IsOpen = false;
            _overlay.style.display = DisplayStyle.None;
        }

        public void SetStatus(string text, bool error)
        {
            _status.text = string.IsNullOrEmpty(text) ? " " : text;
            _status.style.color = error ? Ui.Error : Theme.Muted;
        }

        /// <summary>
        /// Runs one button's work. Its message, or "action failed: reason", goes to the status line, so a game update
        /// that breaks one action leaves the rest of the menu working.
        /// </summary>
        public void Run(string action, Func<string> work)
        {
            try
            {
                SetStatus(work() ?? action, false);
            }
            catch (InvalidOperationException e)
            {
                SetStatus($"{action} failed: {e.Message}", true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"{action} failed: {e}");
                SetStatus($"{action} failed: {e.Message}", true);
            }
        }

        private void Select(Tab tab)
        {
            _selected = tab;
            foreach (var other in _tabs)
                other.Content.style.display = other == tab ? DisplayStyle.Flex : DisplayStyle.None;
            Highlight();
            Show(tab);
        }

        /// <summary>Gold outlines the tab in view; the rest keep the plain line border.</summary>
        private void Highlight()
        {
            foreach (var tab in _tabs)
                Theme.SetBorder(tab.Button, 2f, tab == _selected ? Theme.Gold : Theme.Line, 4f);
        }

        private void Show(Tab tab)
        {
            if (tab.OnShown == null)
                return;
            Run(tab.Button.text, () =>
            {
                tab.OnShown();
                return _status.text;
            });
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private sealed class Tab
        {
            public Button Button;
            public VisualElement Content;
            public Action OnShown;
        }
    }
}
```

- [ ] **Step 4: Write `PlayersTab.cs`**

```csharp
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Players tab: the invincibility toggle and one row per connected player. Polls once a second while the menu
    /// is open and rebuilds only when someone joins, leaves, dies or respawns.
    /// </summary>
    internal static class PlayersTab
    {
        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var root = new VisualElement();

            var invincible = Ui.InvincibleToggle(panel, "Invincible (everyone)", Plugin.InvinciblePlayers,
                "Players are invincible", "Players take damage again");
            root.Add(invincible);

            var list = new ScrollView();
            list.style.flexGrow = 1f;
            root.Add(list);

            string shown = null;

            void Render(bool force)
            {
                var players = GameActions.Players();
                var signature = string.Join("|", players.Select(p => $"{p.ClientId}:{p.Name}:{p.HasBody}:{p.IsDead}"));
                if (!force && signature == shown)
                    return;
                shown = signature;
                list.Clear();
                if (players.Count == 0)
                {
                    list.Add(Ui.Muted("No players."));
                    return;
                }
                foreach (var player in players)
                {
                    var row = BuildRow(panel, player, () => Render(true));
                    // Rows are new elements, so styling them here can't stack hover callbacks.
                    Theme.StyleControls(row);
                    list.Add(row);
                }
            }

            root.schedule.Execute(() =>
            {
                if (!panel.IsOpen)
                    return;
                try { Render(false); }
                catch (Exception e) { Plugin.Log.LogWarning($"Players tab refresh failed: {e.Message}"); }
            }).Every(1000L);

            refresh = () =>
            {
                invincible.SetValueWithoutNotify(Plugin.InvinciblePlayers.Value);
                Render(true);
            };
            return root;
        }

        private static VisualElement BuildRow(AdminMenuPanel panel, PlayerInfo player, Action rerender)
        {
            var row = Ui.Row();
            row.style.paddingBottom = 10f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var name = new Label(player.IsLocal ? $"{player.Name} (you)" : player.Name);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.width = 200f;
            row.Add(name);

            var state = new Label(!player.HasBody ? "No character" : player.IsDead ? "Dead" : "Alive");
            state.style.color = player.IsDead ? Ui.Error : Theme.Muted;
            state.style.width = 110f;
            row.Add(state);

            var id = player.ClientId;
            var revive = Ui.ActionButton(panel, "Revive", () => GameActions.Revive(id), rerender);
            revive.SetEnabled(player.HasBody && player.IsDead);
            row.Add(revive);

            var heal = Ui.ActionButton(panel, "Heal", () => GameActions.Heal(id), rerender);
            heal.SetEnabled(player.HasBody && !player.IsDead);
            row.Add(heal);

            var kill = Ui.ActionButton(panel, "Kill", () => GameActions.Kill(id), rerender);
            kill.SetEnabled(player.HasBody && !player.IsDead);
            row.Add(kill);

            var bring = Ui.ActionButton(panel, "Bring to me", () => GameActions.BringToMe(id));
            bring.SetEnabled(player.HasBody && !player.IsLocal);
            row.Add(bring);

            var goTo = Ui.ActionButton(panel, "Go to", () => GameActions.GoTo(id));
            goTo.SetEnabled(player.HasBody && !player.IsLocal);
            row.Add(goTo);

            return row;
        }
    }
}
```

- [ ] **Step 5: Write `ItemsTab.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Items tab: team money at the top, then a searchable item list with Spawn 1/5/10 on each row. The list is
    /// rebuilt on every keystroke, so each row is styled as it's created.
    /// </summary>
    internal static class ItemsTab
    {
        private const int MaxResults = 200;

        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var all = new List<ItemEntry>();
            var root = new VisualElement();

            root.Add(Ui.Heading("Money"));
            var moneyRow = Ui.Row();
            var money = new Label();
            money.style.color = Theme.Text;
            money.style.width = 200f;
            moneyRow.Add(money);

            void UpdateMoney()
            {
                var value = GameActions.Money();
                money.text = value.HasValue ? $"Team money: ${value.Value:N0}" : "Team money: n/a";
            }

            foreach (var amount in new[] { 10f, 100f, 1000f })
            {
                var add = amount;
                moneyRow.Add(Ui.ActionButton(panel, $"+${add:N0}", () => GameActions.AddMoney(add), UpdateMoney));
            }

            var custom = new TextField { value = string.Empty };
            custom.style.width = 120f;
            custom.style.marginLeft = 12f;
            moneyRow.Add(custom);
            moneyRow.Add(Ui.ActionButton(panel, "Add", () =>
            {
                if (!Amounts.TryParseMoney(custom.value, out var amount))
                    throw new InvalidOperationException($"enter an amount between 1 and {Amounts.MaxMoney:N0}");
                return GameActions.AddMoney(amount);
            }, UpdateMoney));
            root.Add(moneyRow);

            root.Add(Ui.Heading("Items"));
            var searchContainer = new VisualElement();
            searchContainer.style.marginBottom = 12f;
            var search = new TextField { value = string.Empty };
            searchContainer.Add(search);
            var placeholder = new Label("Search items…");
            placeholder.style.color = Theme.Muted;
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 8f;
            placeholder.style.top = 0f;
            placeholder.style.bottom = 0f;
            placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
            placeholder.pickingMode = PickingMode.Ignore;
            searchContainer.Add(placeholder);
            root.Add(searchContainer);

            var results = new ScrollView();
            results.style.flexGrow = 1f;
            root.Add(results);

            void RenderResults()
            {
                results.Clear();
                var matches = ItemSearch.Filter(all, search.value);
                if (matches.Count == 0)
                {
                    results.Add(Ui.Muted(all.Count == 0 ? "No items found in this session." : "No items match."));
                    return;
                }
                var shown = 0;
                foreach (var item in matches)
                {
                    if (shown++ >= MaxResults)
                    {
                        results.Add(Ui.Muted("… more results; refine the search"));
                        break;
                    }
                    var row = BuildRow(panel, item);
                    Theme.StyleControls(row);
                    results.Add(row);
                }
            }

            search.RegisterValueChangedCallback(evt =>
            {
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                RenderResults();
            });

            refresh = () =>
            {
                UpdateMoney();
                all.Clear();
                all.AddRange(ItemCatalog.All());
                RenderResults();
            };
            return root;
        }

        private static VisualElement BuildRow(AdminMenuPanel panel, ItemEntry item)
        {
            var row = Ui.Row();
            row.style.paddingBottom = 8f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var name = new Label(item.Name);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1f;
            name.style.width = 220f;
            row.Add(name);

            var rarity = new Label(item.Rarity);
            rarity.style.color = Theme.Muted;
            rarity.style.width = 100f;
            row.Add(rarity);

            foreach (var count in new[] { 1, 5, 10 })
            {
                var n = count;
                row.Add(Ui.ActionButton(panel, $"Spawn {n}", () => ItemCatalog.Spawn(item, n)));
            }
            return row;
        }
    }
}
```

- [ ] **Step 6: Write `CartTab.cs`**

```csharp
using System;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>Cart tab: where the cart is and what state it's in, plus the fixes for a stuck or broken cart.</summary>
    internal static class CartTab
    {
        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var root = new VisualElement();

            var invincible = Ui.InvincibleToggle(panel, "Invincible cart", Plugin.InvincibleCart,
                "The cart is invincible", "The cart takes damage again");
            root.Add(invincible);

            var summary = Ui.Muted(string.Empty);
            root.Add(summary);

            void UpdateSummary() => summary.text = GameActions.CartSummary();

            var buttons = Ui.Row();
            buttons.Add(Ui.ActionButton(panel, "Bring cart to me", GameActions.BringCart, UpdateSummary));
            buttons.Add(Ui.ActionButton(panel, "Flip upright", GameActions.FlipCart, UpdateSummary));
            buttons.Add(Ui.ActionButton(panel, "Repair", GameActions.RepairCart, UpdateSummary));
            root.Add(buttons);

            root.Add(Ui.Muted("Repair puts every broken panel back at tier 1 and clears cart damage. It doesn't tighten wheel bolts."));

            refresh = () =>
            {
                invincible.SetValueWithoutNotify(Plugin.InvincibleCart.Value);
                UpdateSummary();
            };
            return root;
        }
    }
}
```

- [ ] **Step 7: Write `LevelTab.cs`**

```csharp
using System;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>Level tab: skip ahead, finish the level, or jump to one of the level's named teleport points.</summary>
    internal static class LevelTab
    {
        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var root = new VisualElement();

            var flow = Ui.Row();
            flow.Add(Ui.ActionButton(panel, "Next checkpoint", GameActions.NextCheckpoint));
            flow.Add(Ui.ConfirmButton(panel, "Complete level", GameActions.CompleteLevel));
            root.Add(flow);

            root.Add(Ui.Heading("Teleport points"));
            root.Add(Ui.Muted("Moves every player and the cart."));
            var points = new ScrollView();
            points.style.flexGrow = 1f;
            root.Add(points);

            refresh = () =>
            {
                points.Clear();
                var names = GameActions.TeleportPoints();
                if (names.Count == 0)
                {
                    points.Add(Ui.Muted("No teleport points in this level."));
                    return;
                }
                var row = Ui.Row();
                foreach (var name in names)
                {
                    var target = name;
                    var button = Ui.ActionButton(panel, target, () => GameActions.TeleportTo(target));
                    button.style.marginBottom = 6f;
                    row.Add(button);
                }
                Theme.StyleControls(row);
                points.Add(row);
            };
            return root;
        }
    }
}
```

- [ ] **Step 8: Build against the game**

Run: `cd ~/Documents/GitHub/trashday-admin-menu && dotnet build src/AdminMenu/AdminMenu.csproj -c Release 2>&1 | grep -E "error|warn|Build succeeded" | head -20`
Expected: `Build succeeded`, 0 errors. (A CS0649 warning on `ConfigurationManagerAttributes` would mean `Order` isn't set somewhere; Task 2 sets it on every entry, so none is expected.)

- [ ] **Step 9: Confirm the tests still pass and commit**

Run: `dotnet test tests/AdminMenu.Tests 2>&1 | tail -1`
Expected: `Passed! ... Passed:    30`

```bash
git add src/AdminMenu/Theme.cs src/AdminMenu/Ui.cs src/AdminMenu/AdminMenuPanel.cs src/AdminMenu/PlayersTab.cs src/AdminMenu/ItemsTab.cs src/AdminMenu/CartTab.cs src/AdminMenu/LevelTab.cs
git commit -m "feat: admin menu window with players, items, cart and level tabs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Hotkey, controller and input lock

**Files:**
- Create: `src/AdminMenu/Hotkey.cs`
- Create: `src/AdminMenu/AdminMenuController.cs`
- Modify: `src/AdminMenu/Plugin.cs` (create the controller at the end of `Awake`)

**Interfaces:**
- Consumes: `MenuGate` (Task 1); `Plugin.ToggleKey`, `Plugin.CloseOnEscape` (Task 2); `GameActions.IsServer`, `GameActions.LocalPlayer()`, `GameActions.Toast` (Task 3); `AdminMenuPanel` and the four tabs' `Build` (Task 4).
- Produces: `Hotkey.WasPressed(KeyboardShortcut) -> bool`; `AdminMenuController : MonoBehaviour`.

- [ ] **Step 1: Write `Hotkey.cs`**

Trash Day uses only the Input System, so this is the Input System half of the stonewards `Hotkey`.

```csharp
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdminMenu
{
    /// <summary>
    /// Reads a <see cref="KeyboardShortcut"/> straight from the keyboard through the Input System, so it works
    /// while the game's own input actions are disabled.
    /// </summary>
    internal static class Hotkey
    {
        private static readonly Dictionary<KeyCode, Key> Renamed = new Dictionary<KeyCode, Key>
        {
            [KeyCode.Return] = Key.Enter,
            [KeyCode.KeypadEnter] = Key.NumpadEnter,
            [KeyCode.LeftControl] = Key.LeftCtrl,
            [KeyCode.RightControl] = Key.RightCtrl,
            [KeyCode.LeftCommand] = Key.LeftMeta,
            [KeyCode.RightCommand] = Key.RightMeta,
            [KeyCode.BackQuote] = Key.Backquote,
            [KeyCode.KeypadPlus] = Key.NumpadPlus,
            [KeyCode.KeypadMinus] = Key.NumpadMinus,
            [KeyCode.KeypadMultiply] = Key.NumpadMultiply,
            [KeyCode.KeypadDivide] = Key.NumpadDivide,
            [KeyCode.KeypadPeriod] = Key.NumpadPeriod,
        };

        public static bool WasPressed(KeyboardShortcut shortcut)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || shortcut.MainKey == KeyCode.None)
                return false;
            if (!TryGetKey(shortcut.MainKey, out var main) || !keyboard[main].wasPressedThisFrame)
                return false;
            foreach (var modifier in shortcut.Modifiers)
            {
                if (!TryGetKey(modifier, out var key) || !keyboard[key].isPressed)
                    return false;
            }
            return true;
        }

        public static bool EscapePressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        public static bool EscapeHeld() => Keyboard.current != null && Keyboard.current.escapeKey.isPressed;

        private static bool TryGetKey(KeyCode code, out Key key)
        {
            if (Renamed.TryGetValue(code, out key))
                return true;

            var name = code.ToString();
            if (name.StartsWith("Alpha"))
                name = "Digit" + name.Substring("Alpha".Length);
            else if (name.StartsWith("Keypad"))
                name = "Numpad" + name.Substring("Keypad".Length);

            return Enum.TryParse(name, true, out key) && key != Key.None;
        }
    }
}
```

- [ ] **Step 2: Write `AdminMenuController.cs`**

```csharp
using System;
using Ox1;
using Trash;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Owns the menu: reads the hotkey, opens and closes the window, and takes the game's input while it's open
    /// the same way the game's own console does (see Trash.ConsoleMenuHider): gameplay actions off, game menus
    /// suppressed so Esc can't open the pause menu underneath, plus a look block and a free cursor.
    /// </summary>
    internal sealed class AdminMenuController : MonoBehaviour
    {
        private enum CloseReason
        {
            /// <summary>Close button or hotkey.</summary>
            User,
            /// <summary>Esc: keep game menus suppressed until the key is released.</summary>
            Escape,
            /// <summary>Hosting ended or the player is gone: hand input back but leave the cursor to the game.</summary>
            Lost,
        }

        private UIDocument _document;
        private AdminMenuPanel _panel;
        private bool _open;
        /// <summary>True when we turned MenusSuppressed on, so we never clear one the console set.</summary>
        private bool _suppressedMenus;
        private bool _holdSuppressionUntilEscReleased;
        private bool _loggedTickError;

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                if (!_loggedTickError)
                {
                    _loggedTickError = true;
                    Plugin.Log.LogError($"Admin menu update failed; closing the menu. {e}");
                }
                if (_open)
                    Close(CloseReason.Lost);
            }
        }

        private void OnDestroy()
        {
            if (_open)
                Close(CloseReason.Lost);
            ReleaseSuppression();
        }

        private void Tick()
        {
            if (_holdSuppressionUntilEscReleased && !Hotkey.EscapeHeld())
                ReleaseSuppression();

            if (_open)
            {
                if (_panel.Root.panel == null || MenuGate.ShouldAutoClose(GameActions.IsServer, GameActions.LocalPlayer() != null))
                {
                    Close(CloseReason.Lost);
                    return;
                }
                if (Plugin.CloseOnEscape.Value && Hotkey.EscapePressed())
                {
                    Close(CloseReason.Escape);
                    return;
                }
                KeepInputLocked();
            }

            if (Hotkey.WasPressed(Plugin.ToggleKey.Value))
                Toggle();
        }

        private void Toggle()
        {
            var decision = MenuGate.Decide(_open, GameActions.IsServer, GameActions.LocalPlayer() != null, MenuSceneManager.IsMenuOpen);
            switch (decision)
            {
                case ToggleDecision.Open:
                    Open();
                    break;
                case ToggleDecision.Close:
                    Close(CloseReason.User);
                    break;
                default:
                    var message = MenuGate.Message(decision);
                    if (message != null)
                        GameActions.Toast(message);
                    break;
            }
        }

        private void Open()
        {
            if (!EnsurePanel())
            {
                Plugin.Log.LogWarning("No game UI to borrow panel settings from yet; the admin menu can't open here.");
                return;
            }

            _open = true;
            _holdSuppressionUntilEscReleased = false;
            if (!MenuSceneManager.MenusSuppressed)
            {
                MenuSceneManager.MenusSuppressed = true;
                _suppressedMenus = true;
            }
            PlayerCamera.LookInputBlocks.SetRequest(this, true);
            KeepInputLocked();
            _panel.Open();
        }

        private void Close(CloseReason reason)
        {
            if (!_open)
                return;
            _open = false;
            _panel?.Close();

            PlayerCamera.LookInputBlocks.SetRequest(this, false);
            if (MenuGate.ShouldEnableGameInput(MenuSceneManager.AnyMenuVisible.Value))
                InputSystem.actions?.Enable();
            if (reason != CloseReason.Lost && MenuGate.ShouldLockCursor(MenuSceneManager.IsMenuOpen))
                Cursor.lockState = CursorLockMode.Locked;

            if (reason == CloseReason.Escape)
                _holdSuppressionUntilEscReleased = true;
            else
                ReleaseSuppression();
        }

        /// <summary>Re-asserted every frame: a game menu closing under ours re-enables actions and relocks the cursor.</summary>
        private static void KeepInputLocked()
        {
            var actions = InputSystem.actions;
            if (actions != null && actions.enabled)
                actions.Disable();
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
        }

        private void ReleaseSuppression()
        {
            _holdSuppressionUntilEscReleased = false;
            if (!_suppressedMenus)
                return;
            _suppressedMenus = false;
            MenuSceneManager.MenusSuppressed = false;
        }

        /// <summary>
        /// Builds the window once, in its own UIDocument that survives scene loads. Its PanelSettings are a copy of a
        /// game document's, so the game's theme and fonts carry over, drawn above everything else.
        /// </summary>
        private bool EnsurePanel()
        {
            if (_document == null)
            {
                var settings = CreatePanelSettings();
                if (settings == null)
                    return false;
                var go = new GameObject("AdminMenuUI");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                _document = go.AddComponent<UIDocument>();
                _document.panelSettings = settings;
                go.SetActive(true);
            }

            if (_panel == null)
            {
                _panel = new AdminMenuPanel();
                _panel.CloseRequested += () => Close(CloseReason.User);
                _panel.AddTab("Players", PlayersTab.Build(_panel, out var refreshPlayers), refreshPlayers);
                _panel.AddTab("Items", ItemsTab.Build(_panel, out var refreshItems), refreshItems);
                _panel.AddTab("Cart", CartTab.Build(_panel, out var refreshCart), refreshCart);
                _panel.AddTab("Level", LevelTab.Build(_panel, out var refreshLevel), refreshLevel);
            }

            var root = _document.rootVisualElement;
            if (root == null)
                return false;
            if (_panel.Root.parent != root)
            {
                _panel.Root.RemoveFromHierarchy();
                root.Add(_panel.Root);
            }
            return _panel.Root.panel != null;
        }

        private static PanelSettings CreatePanelSettings()
        {
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var source = document != null ? document.panelSettings : null;
                if (source == null || source.targetTexture != null || source.themeStyleSheet == null)
                    continue;
                var settings = Instantiate(source);
                settings.name = "AdminMenuPanelSettings";
                settings.sortingOrder = 10000f;
                return settings;
            }
            return null;
        }
    }
}
```

- [ ] **Step 3: Create the controller from `Plugin.Awake`**

In `src/AdminMenu/Plugin.cs`, replace:

```csharp
            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {ToggleKey.Value} while hosting to open the admin menu");
```

with:

```csharp
            // Its own object, so it outlives scene loads and doesn't depend on BepInEx's manager object staying alive.
            var host = new GameObject("AdminMenu");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<AdminMenuController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {ToggleKey.Value} while hosting to open the admin menu");
```

- [ ] **Step 4: Build against the game**

Run: `cd ~/Documents/GitHub/trashday-admin-menu && dotnet build src/AdminMenu/AdminMenu.csproj -c Release 2>&1 | grep -E "error|warn|Deployed|Build succeeded" | head -20`
Expected: `Deployed AdminMenu.dll ...` and `Build succeeded`, 0 errors.

- [ ] **Step 5: Confirm the tests still pass and commit**

Run: `dotnet test tests/AdminMenu.Tests 2>&1 | tail -1`
Expected: `Passed! ... Passed:    30`

```bash
git add src/AdminMenu/Hotkey.cs src/AdminMenu/AdminMenuController.cs src/AdminMenu/Plugin.cs
git commit -m "feat: hotkey, input lock and menu controller

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Reference assemblies, packaging, release workflow and docs

**Files:**
- Create: `scripts/update-refs.sh`, `scripts/package.sh`, `scripts/thunderstore-publish.sh`
- Create: `lib/refs/*.dll` (generated)
- Create: `.github/workflows/release.yml`
- Create: `thunderstore/manifest.json`, `thunderstore/README.md`, `thunderstore/icon.svg`, `thunderstore/icon.png`
- Create: `README.md`, `CHANGELOG.md`, `CLAUDE.md`, `docs/manual-verification-0.1.0.md`
- Modify: `~/Documents/GitHub/trashday-more-players/CLAUDE.md` (sibling note: admin menu no longer "planned")

**Interfaces:**
- Consumes: the built plugin (Tasks 2–5).
- Produces: `dist/darkharasho-AdminMenu-0.1.0.zip` containing `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, `plugins/AdminMenu.dll`.

- [ ] **Step 1: Copy and adapt the scripts**

```bash
cd ~/Documents/GitHub/trashday-admin-menu
mkdir -p scripts .github/workflows thunderstore
cp ~/.claude/templates/thunderstore/thunderstore-publish.sh scripts/
cp ~/Documents/GitHub/trashday-more-players/scripts/update-refs.sh ~/Documents/GitHub/trashday-more-players/scripts/package.sh scripts/
cp ~/Documents/GitHub/trashday-more-players/.github/workflows/release.yml .github/workflows/
sed -i 's/MorePlayers/AdminMenu/g' scripts/package.sh scripts/update-refs.sh .github/workflows/release.yml
sed -i 's/^game_dlls=(.*)$/game_dlls=(Assembly-CSharp UnityEngine UnityEngine.CoreModule UnityEngine.TextRenderingModule UnityEngine.UIElementsModule UnityEngine.PhysicsModule Unity.InputSystem Unity.Netcode.Runtime)/' scripts/update-refs.sh
sed -i 's/Requires BepInExPack 5.4.2305. Only the host needs it./Requires BepInExPack 5.4.2305. Only the host needs it; press F1 in a level you host./' .github/workflows/release.yml
chmod +x scripts/*.sh
grep -n "AdminMenu\|game_dlls\|MorePlayers" scripts/*.sh .github/workflows/release.yml
```

Expected: every former `MorePlayers` now reads `AdminMenu`, no `MorePlayers` left, and `game_dlls` lists the eight game assemblies. If Task 2–5 added references for CS0012, add those names to `game_dlls` too.

- [ ] **Step 2: Generate the reference assemblies and build against them**

Run: `scripts/update-refs.sh && dotnet build src/AdminMenu/AdminMenu.csproj -c Release -p:UseRefs=true 2>&1 | grep -E "error|Build succeeded"`
Expected: `lib/refs` lists 10 DLLs (8 game + `BepInEx`, `0Harmony`), then `Build succeeded` with 0 errors.

- [ ] **Step 3: Write the Thunderstore files**

`thunderstore/manifest.json`:

```json
{
    "name": "AdminMenu",
    "version_number": "0.1.0",
    "website_url": "https://github.com/darkharasho/trashday-admin-menu",
    "description": "Host-only admin menu for Trash Day (F1): revive, heal, kill and teleport players, spawn items and money, fix the cart, skip checkpoints, invincibility.",
    "dependencies": [
        "BepInEx-BepInExPack-5.4.2305"
    ]
}
```

`thunderstore/icon.svg` (256×256: dark background, the green trash can from MorePlayers' icon, gold wrench across it):

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#3a3f44"/>
      <stop offset="1" stop-color="#1c1f22"/>
    </linearGradient>
    <linearGradient id="can" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#3f8f4a"/>
      <stop offset="0.5" stop-color="#5cb866"/>
      <stop offset="1" stop-color="#2f6e38"/>
    </linearGradient>
    <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#ffe08a"/>
      <stop offset="1" stop-color="#d9a92e"/>
    </linearGradient>
  </defs>
  <rect width="256" height="256" rx="28" fill="url(#bg)"/>
  <rect x="62" y="58" width="132" height="20" rx="6" fill="#2f6e38"/>
  <rect x="108" y="44" width="40" height="16" rx="5" fill="#2f6e38"/>
  <path d="M72 84 H184 L172 214 Q170 224 160 224 H96 Q86 224 84 214 Z" fill="url(#can)"/>
  <g stroke="#2a5f31" stroke-width="6" stroke-linecap="round">
    <line x1="104" y1="104" x2="108" y2="204"/>
    <line x1="128" y1="104" x2="128" y2="204"/>
    <line x1="152" y1="104" x2="148" y2="204"/>
  </g>
  <g transform="rotate(-40 128 140)">
    <rect x="120" y="70" width="16" height="130" rx="8" fill="url(#gold)" stroke="#5a4310" stroke-width="3"/>
    <path d="M128 40 a26 26 0 1 0 0.1 0 Z M118 40 h20 v24 h-20 Z" fill="url(#gold)" fill-rule="evenodd" stroke="#5a4310" stroke-width="3"/>
  </g>
</svg>
```

Run: `rsvg-convert -w 256 -h 256 thunderstore/icon.svg -o thunderstore/icon.png && file thunderstore/icon.png`
Expected: `PNG image data, 256 x 256`. Look at it with the Read tool; if the wrench reads poorly, adjust the SVG and re-render (a ruling only if the concept changes).

`thunderstore/README.md`:

```markdown
# Admin Menu

A host-only admin menu for Trash Day. Press **F1** in a level you're hosting.

## Players
- Revive, heal or kill anyone; bring them to you or go to them.
- **Invincible (everyone)**: nobody takes damage.

## Items
- Search every item in the game and spawn 1, 5 or 10 in front of you.
- Add team money: +$10, +$100, +$1000 or any amount up to $1,000,000.

## Cart
- Bring the cart to you, flip it upright, or repair every broken panel.
- **Invincible cart**: no panel, body or wheel damage.

## Level
- Teleport everyone to the next checkpoint, complete the level, or jump to any of the level's teleport points.

## Notes
- Only the host needs the mod; it does nothing for clients ("Admin menu: host only").
- Esc or F1 closes the menu. The key and the Esc behaviour are in `BepInEx/config/com.darkharasho.trashday.adminmenu.cfg`.
- Invincibility settings are saved and stay on after a restart.
```

- [ ] **Step 4: Write the repo docs**

`CHANGELOG.md`:

```markdown
# Changelog

## 0.1.0

- First release: a host-only admin menu on F1 with Players (revive, heal, kill, bring, go to, invincibility), Items (search and spawn, team money), Cart (bring, flip, repair, invincibility) and Level (next checkpoint, complete level, teleport points) tabs.
```

`README.md`:

```markdown
# Trash Day Admin Menu

A host-only admin menu for [Trash Day](https://store.steampowered.com/app/3314580/), opened with F1. See [thunderstore/README.md](thunderstore/README.md) for what it does.

## Building

- Install BepInEx 5 into the game folder, then run `dotnet build src/AdminMenu/AdminMenu.csproj -c Release`. The build deploys the DLL to `BepInEx/plugins/darkharasho-AdminMenu/`.
- Without the game installed, the build uses the reference assemblies in `lib/refs` (regenerate with `scripts/update-refs.sh`).
- Tests: `dotnet test tests/AdminMenu.Tests`.
- Package: `scripts/package.sh` writes `dist/darkharasho-AdminMenu-<version>.zip`.

## Releasing

Bump the version in `src/AdminMenu/Plugin.cs`, `src/AdminMenu/AdminMenu.csproj`, `thunderstore/manifest.json` and `CHANGELOG.md`, then push the tag `v<version>`. The release workflow builds, attaches the zip to a GitHub release and publishes to Thunderstore.
```

`CLAUDE.md`:

```markdown
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
```

In `~/Documents/GitHub/trashday-more-players/CLAUDE.md`, change `` `trashday-admin-menu` and `trashday-mod-settings` (planned) `` to `` `trashday-admin-menu`, `trashday-mod-settings` (planned) ``. Commit that in the more-players repo separately (Step 7).

- [ ] **Step 5: Write the manual checklist**

`docs/manual-verification-0.1.0.md`:

```markdown
# Manual verification, 0.1.0

Host a lobby (ideally with a second client) with the mod deployed. Tick each line.

## Open and close
- [ ] F1 in the main menu does nothing.
- [ ] F1 in a level you host opens the menu; the cursor is free; moving the mouse doesn't turn the camera; WASD doesn't move you.
- [ ] F1 again closes it; the cursor locks and movement and look work again.
- [ ] Esc closes it and the pause menu does NOT open. Esc again (menu closed) opens the pause menu as normal.
- [ ] With the pause menu open, F1 does nothing.
- [ ] The Close button closes it.
- [ ] As a client (second instance), F1 shows "Admin menu: host only" and nothing opens.
- [ ] Open the menu, then leave to the main menu: the menu is gone and the main menu cursor works.

## Players
- [ ] Both players are listed; the host is first, marked "(you)".
- [ ] Kill the client: they die, the row shows Dead within a second.
- [ ] Revive them: they're alive and at full health.
- [ ] Heal after taking damage restores health.
- [ ] Bring to me puts them just in front of you, facing you; Go to puts you behind them.
- [ ] Invincible (everyone) on: hazards and falls do no damage. Kill still works. Off: damage again.
- [ ] Invincible stays on after a game restart (config persisted).

## Items
- [ ] The list shows readable names (not raw keys) and rarities.
- [ ] Typing filters it; clearing the search shows everything.
- [ ] Spawn 1 / 5 / 10 drops that many in front of you; the client sees them and can pick them up.
- [ ] +$10 / +$100 / +$1000 add to team money; the label updates.
- [ ] Custom amount "250" adds 250; "abc", "-5" and "0" show an error and add nothing.

## Cart
- [ ] Summary shows panels intact and damage.
- [ ] Bring cart to me puts it about 3 m ahead, upright.
- [ ] Tip the cart over; Flip upright rights it.
- [ ] Break panels; Repair restores them and the summary shows all intact.
- [ ] Invincible cart on: crashes break no panels and loosen no wheels.

## Level
- [ ] Next checkpoint moves everyone and the cart; after the last one it reports that all are reached.
- [ ] Complete level needs two clicks and then ends the level.
- [ ] Teleport point buttons move everyone and the cart.

## Robustness
- [ ] `BepInEx/LogOutput.log` shows "AdminMenu 0.1.0 loaded" and "Item catalog: N items", and no errors.
```

- [ ] **Step 6: Package and check the zip**

Run: `scripts/package.sh 2>&1 | tail -12`
Expected: the `unzip -l` listing shows `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, `plugins/AdminMenu.dll`, then `Package: .../dist/darkharasho-AdminMenu-0.1.0.zip`.

- [ ] **Step 7: Run the full test suite and commit**

Run: `dotnet test tests/AdminMenu.Tests 2>&1 | tail -1`
Expected: `Passed! ... Passed:    30`

```bash
cd ~/Documents/GitHub/trashday-admin-menu
git add scripts lib/refs .github thunderstore README.md CHANGELOG.md CLAUDE.md docs/manual-verification-0.1.0.md
git commit -m "build: reference assemblies, Thunderstore package, release workflow and docs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
cd ~/Documents/GitHub/trashday-more-players
git add CLAUDE.md
git commit -m "docs: admin menu sibling exists now

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Don't push either repo; creating the GitHub repo, setting its secret and tagging wait for the user.
