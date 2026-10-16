using AdminMenu;
using Xunit;

public class PanelSourceTests
{
    private static PanelCandidate Hud => new PanelCandidate { HasTheme = true };
    private static PanelCandidate Toast => new PanelCandidate { HasTheme = true, IsToast = true };

    [Fact]
    public void PrefersTheToastDocumentBecauseItIsAScreenOverlay()
    {
        Assert.Equal(1, PanelSource.Pick(new[] { Hud, Toast, Hud }));
    }

    [Fact]
    public void FallsBackToTheFirstThemedScreenPanel()
    {
        var noTheme = new PanelCandidate { IsToast = true };
        var rendersToTexture = new PanelCandidate { HasTheme = true, HasTargetTexture = true };
        Assert.Equal(2, PanelSource.Pick(new[] { noTheme, rendersToTexture, Hud }));
    }

    [Fact]
    public void ReturnsMinusOneWhenNothingQualifies()
    {
        Assert.Equal(-1, PanelSource.Pick(new[] { new PanelCandidate(), new PanelCandidate { HasTheme = true, HasTargetTexture = true } }));
        Assert.Equal(-1, PanelSource.Pick(new PanelCandidate[0]));
        Assert.Equal(-1, PanelSource.Pick(null));
    }
}
