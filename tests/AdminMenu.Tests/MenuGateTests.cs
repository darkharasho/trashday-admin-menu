using AdminMenu;
using Xunit;

public class MenuGateTests
{
    [Fact]
    public void ClosesWhenOpenWhateverTheState()
    {
        Assert.Equal(ToggleDecision.Close, MenuGate.Decide(menuOpen: true, isServer: false, hasLocalPlayer: false, gameMenuOpen: true, consoleOpen: false));
    }

    [Fact]
    public void OpensForTheHostInALevel()
    {
        Assert.Equal(ToggleDecision.Open, MenuGate.Decide(menuOpen: false, isServer: true, hasLocalPlayer: true, gameMenuOpen: false, consoleOpen: false));
    }

    [Fact]
    public void RefusesClientsInALevelAsHostOnly()
    {
        Assert.Equal(ToggleDecision.HostOnly, MenuGate.Decide(menuOpen: false, isServer: false, hasLocalPlayer: true, gameMenuOpen: false, consoleOpen: false));
    }

    [Fact]
    public void StaysQuietOutsideALevelEvenForClients()
    {
        Assert.Equal(ToggleDecision.NotInLevel, MenuGate.Decide(menuOpen: false, isServer: false, hasLocalPlayer: false, gameMenuOpen: false, consoleOpen: false));
        Assert.Equal(ToggleDecision.NotInLevel, MenuGate.Decide(menuOpen: false, isServer: true, hasLocalPlayer: false, gameMenuOpen: false, consoleOpen: false));
    }

    [Fact]
    public void DoesNotOpenOverAGameMenu()
    {
        Assert.Equal(ToggleDecision.GameMenuOpen, MenuGate.Decide(menuOpen: false, isServer: true, hasLocalPlayer: true, gameMenuOpen: true, consoleOpen: false));
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
        Assert.True(MenuGate.ShouldEnableGameInput(gameMenuVisible: false, consoleOpen: false));
        Assert.False(MenuGate.ShouldEnableGameInput(gameMenuVisible: true, consoleOpen: false));
    }

    [Fact]
    public void LocksTheCursorOnlyWhenNoGameMenuIsOpen()
    {
        Assert.True(MenuGate.ShouldLockCursor(gameMenuOpen: false));
        Assert.False(MenuGate.ShouldLockCursor(gameMenuOpen: true));
    }

    [Fact]
    public void DoesNotOpenOverTheConsole()
    {
        Assert.Equal(ToggleDecision.GameMenuOpen, MenuGate.Decide(menuOpen: false, isServer: true, hasLocalPlayer: true, gameMenuOpen: false, consoleOpen: true));
    }

    [Fact]
    public void KeepsGameInputOffWhileTheConsoleIsOpen()
    {
        Assert.False(MenuGate.ShouldEnableGameInput(gameMenuVisible: false, consoleOpen: true));
    }

    [Fact]
    public void ClearsMenuSuppressionOnlyWhenItSetItAndTheConsoleIsClosed()
    {
        Assert.True(MenuGate.ShouldClearMenuSuppression(suppressedByUs: true, consoleOpen: false));
        Assert.False(MenuGate.ShouldClearMenuSuppression(suppressedByUs: true, consoleOpen: true));
        Assert.False(MenuGate.ShouldClearMenuSuppression(suppressedByUs: false, consoleOpen: false));
    }
}
