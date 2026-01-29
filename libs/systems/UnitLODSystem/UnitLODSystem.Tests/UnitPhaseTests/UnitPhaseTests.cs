using Xunit;
using Tomato.UnitLODSystem;

namespace Tomato.UnitLODSystem.Tests.UnitPhaseEnumTests
{

public class UnitPhaseTests
{
    [Fact]
    public void UnitPhase_HasExpectedValues()
    {
        Assert.Equal(0, (int)UnitPhase.None);
        Assert.Equal(1, (int)UnitPhase.Loading);
        Assert.Equal(2, (int)UnitPhase.Loaded);
        Assert.Equal(3, (int)UnitPhase.Creating);
        Assert.Equal(4, (int)UnitPhase.Ready);
        Assert.Equal(5, (int)UnitPhase.Unloading);
        Assert.Equal(6, (int)UnitPhase.Unloaded);
    }

    [Fact]
    public void UnitPhase_Ordering_LoadingPhasesBeforeReady()
    {
        Assert.True(UnitPhase.None < UnitPhase.Loading);
        Assert.True(UnitPhase.Loading < UnitPhase.Loaded);
        Assert.True(UnitPhase.Loaded < UnitPhase.Creating);
        Assert.True(UnitPhase.Creating < UnitPhase.Ready);
    }

    [Fact]
    public void UnitPhase_Ordering_UnloadingPhasesAfterReady()
    {
        Assert.True(UnitPhase.Ready < UnitPhase.Unloading);
        Assert.True(UnitPhase.Unloading < UnitPhase.Unloaded);
    }
}

}
