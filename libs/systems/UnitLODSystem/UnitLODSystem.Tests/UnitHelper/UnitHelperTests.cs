using Xunit;
using Tomato.UnitLODSystem;

namespace Tomato.UnitLODSystem.Tests.UnitHelperTests
{

public class UnitHelperTests
{
    [Theory]
    [InlineData(UnitPhase.None, false)]
    [InlineData(UnitPhase.Loading, true)]
    [InlineData(UnitPhase.Loaded, false)]
    [InlineData(UnitPhase.Creating, false)]
    [InlineData(UnitPhase.Ready, false)]
    [InlineData(UnitPhase.Unloading, false)]
    [InlineData(UnitPhase.Unloaded, false)]
    public void IsLoading_ReturnsCorrectValue(UnitPhase phase, bool expected)
    {
        Assert.Equal(expected, UnitHelper.IsLoading(phase));
    }

    [Theory]
    [InlineData(UnitPhase.None, false)]
    [InlineData(UnitPhase.Loading, false)]
    [InlineData(UnitPhase.Loaded, false)]
    [InlineData(UnitPhase.Creating, false)]
    [InlineData(UnitPhase.Ready, true)]
    [InlineData(UnitPhase.Unloading, false)]
    [InlineData(UnitPhase.Unloaded, false)]
    public void IsStable_ReturnsCorrectValue(UnitPhase phase, bool expected)
    {
        Assert.Equal(expected, UnitHelper.IsStable(phase));
    }

    [Theory]
    [InlineData(UnitPhase.None, false)]
    [InlineData(UnitPhase.Loading, false)]
    [InlineData(UnitPhase.Loaded, false)]
    [InlineData(UnitPhase.Creating, false)]
    [InlineData(UnitPhase.Ready, false)]
    [InlineData(UnitPhase.Unloading, true)]
    [InlineData(UnitPhase.Unloaded, false)]
    public void IsUnloading_ReturnsCorrectValue(UnitPhase phase, bool expected)
    {
        Assert.Equal(expected, UnitHelper.IsUnloading(phase));
    }

    [Theory]
    [InlineData(UnitPhase.None, false)]
    [InlineData(UnitPhase.Loading, false)]
    [InlineData(UnitPhase.Loaded, false)]
    [InlineData(UnitPhase.Creating, false)]
    [InlineData(UnitPhase.Ready, false)]
    [InlineData(UnitPhase.Unloading, false)]
    [InlineData(UnitPhase.Unloaded, true)]
    public void CanDispose_ReturnsCorrectValue(UnitPhase phase, bool expected)
    {
        Assert.Equal(expected, UnitHelper.CanDispose(phase));
    }
}

}
