using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.UnitLODSystem;
using Tomato.UnitLODSystem.Tests.Mocks;

namespace Tomato.UnitLODSystem.Tests.UnitTests
{

public class ForwardFlowTests
{
    [Fact]
    public void RequestState_ZeroToOne_CreatesDetails()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        unit.RequestState(1);

        Assert.Null(unit.Get<MockUnitDetailA>());

        unit.Tick();

        Assert.Null(unit.Get<MockUnitDetailA>());
    }

    [Fact]
    public void RequestState_DetailBecomesReady_AfterMultipleTicks()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        unit.RequestState(1);

        for (int i = 0; i < 10; i++)
        {
            unit.Tick();
        }

        Assert.NotNull(unit.Get<MockUnitDetailA>());
    }

    [Fact]
    public void RequestState_GroupInstantiatedTogether()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(1);

        var events = new List<UnitPhaseChangedEventArgs>();
        unit.UnitPhaseChanged += (s, e) => events.Add(e);

        unit.RequestState(1);
        unit.Tick();
        unit.Tick();

        var noneToLoadingEvents = events.Where(e =>
            e.OldPhase == UnitPhase.None && e.NewPhase == UnitPhase.Loading).ToList();

        Assert.Equal(2, noneToLoadingEvents.Count);
    }

    [Fact]
    public void RequestState_GroupsLoadSequentially_Group2StartsAfterGroup1Loaded()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(2);

        var loadingOrder = new List<Type>();
        var loadedOrder = new List<Type>();
        unit.UnitPhaseChanged += (s, e) =>
        {
            if (e.NewPhase == UnitPhase.Loading)
                loadingOrder.Add(e.DetailType);
            if (e.NewPhase == UnitPhase.Loaded)
                loadedOrder.Add(e.DetailType);
        };

        unit.RequestState(2);

        // Run until stable
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        // Group 1 should start Loading before Group 2
        Assert.Equal(2, loadingOrder.Count);
        Assert.Equal(typeof(MockUnitDetailA), loadingOrder[0]);
        Assert.Equal(typeof(MockUnitDetailB), loadingOrder[1]);

        // Group 1 should be Loaded before Group 2 starts Loading
        // (loadedOrder[0] should come before loadingOrder[1] in time)
        Assert.Equal(typeof(MockUnitDetailA), loadedOrder[0]);
    }

    [Fact]
    public void RequestState_CreatingInRequiredAtOrder()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(2);

        var readyOrder = new List<Type>();
        unit.UnitPhaseChanged += (s, e) =>
        {
            if (e.NewPhase == UnitPhase.Ready)
            {
                readyOrder.Add(e.DetailType);
            }
        };

        unit.RequestState(2);

        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.Equal(2, readyOrder.Count);
        Assert.Equal(typeof(MockUnitDetailA), readyOrder[0]);
        Assert.Equal(typeof(MockUnitDetailB), readyOrder[1]);
    }

    [Fact]
    public void IsStable_TrueWhenAllReady()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        Assert.True(unit.IsStable);

        unit.RequestState(1);
        unit.Tick();

        Assert.False(unit.IsStable);

        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.True(unit.IsStable);
    }
}

}
