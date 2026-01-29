using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.UnitLODSystem;
using Tomato.UnitLODSystem.Tests.Mocks;

namespace Tomato.UnitLODSystem.Tests.UnitTests
{

public class EventTests
{
    [Fact]
    public void UnitPhaseChanged_FiresOnPhaseTransition()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        var events = new List<UnitPhaseChangedEventArgs>();
        unit.UnitPhaseChanged += (s, e) => events.Add(e);

        unit.RequestState(1);

        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        Assert.True(events.Count >= 4);

        var phases = events.Where(e => e.DetailType == typeof(MockUnitDetailA))
            .Select(e => e.NewPhase).ToList();

        Assert.Contains(UnitPhase.Loading, phases);
        Assert.Contains(UnitPhase.Loaded, phases);
        Assert.Contains(UnitPhase.Creating, phases);
        Assert.Contains(UnitPhase.Ready, phases);
    }

    [Fact]
    public void UnitPhaseChanged_ContainsCorrectOldAndNewPhase()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        var events = new List<UnitPhaseChangedEventArgs>();
        unit.UnitPhaseChanged += (s, e) => events.Add(e);

        unit.RequestState(1);
        unit.Tick();
        unit.Tick();

        var firstEvent = events.FirstOrDefault(e => e.DetailType == typeof(MockUnitDetailA));
        Assert.NotNull(firstEvent);
        Assert.Equal(UnitPhase.None, firstEvent.OldPhase);
        Assert.Equal(UnitPhase.Loading, firstEvent.NewPhase);
    }

    [Fact]
    public void UnitPhaseChanged_FiresForUnloading()
    {
        var unit = new Unit();
        unit.Register<MockUnitDetailA>(1);

        unit.RequestState(1);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        var events = new List<UnitPhaseChangedEventArgs>();
        unit.UnitPhaseChanged += (s, e) => events.Add(e);

        unit.RequestState(0);
        for (int i = 0; i < 20; i++)
        {
            unit.Tick();
        }

        var phases = events.Where(e => e.DetailType == typeof(MockUnitDetailA))
            .Select(e => e.NewPhase).ToList();

        Assert.Contains(UnitPhase.Unloading, phases);
        Assert.Contains(UnitPhase.Unloaded, phases);
    }
}

}
