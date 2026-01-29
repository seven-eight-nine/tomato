using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.UnitLODSystem;
using Tomato.UnitLODSystem.Tests.Mocks;

namespace Tomato.UnitLODSystem.Tests.UnitTests
{

public class BackwardFlowTests
{
    [Fact]
    public void RequestState_TwoToZero_UnloadsInDescendingOrder()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(2);

        unit.RequestState(2);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.True(unit.IsStable);
        Assert.NotNull(unit.Get<MockUnitDetailA>());
        Assert.NotNull(unit.Get<MockUnitDetailB>());

        var unloadOrder = new List<Type>();
        unit.UnitPhaseChanged += (s, e) =>
        {
            if (e.NewPhase == UnitPhase.Unloading)
            {
                unloadOrder.Add(e.DetailType);
            }
        };

        unit.RequestState(0);

        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.Equal(2, unloadOrder.Count);
        Assert.Equal(typeof(MockUnitDetailB), unloadOrder[0]);
        Assert.Equal(typeof(MockUnitDetailA), unloadOrder[1]);
    }

    [Fact]
    public void RequestState_TwoToOne_UnloadsOnlyHigherGroup()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(2);

        unit.RequestState(2);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        unit.RequestState(1);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.NotNull(unit.Get<MockUnitDetailA>());
        Assert.Null(unit.Get<MockUnitDetailB>());
    }

    [Fact]
    public void RequestState_GroupUnloadsAndDisposeTogether()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);
        unit.Register<MockUnitDetailB>(1);

        unit.RequestState(1);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        var unloadingEvents = new List<Type>();
        unit.UnitPhaseChanged += (s, e) =>
        {
            if (e.NewPhase == UnitPhase.Unloading)
            {
                unloadingEvents.Add(e.DetailType);
            }
        };

        unit.RequestState(0);
        unit.Tick();

        Assert.Equal(2, unloadingEvents.Count);
    }
}

}
