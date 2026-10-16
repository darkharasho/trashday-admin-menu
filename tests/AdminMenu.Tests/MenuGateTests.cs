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
