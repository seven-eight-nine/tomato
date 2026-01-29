using System;
using Tomato.UnitLODSystem;

namespace Tomato.UnitLODSystem.Tests.Mocks
{

public class MockUnitDetail : IUnitDetail<Unit>
{
    public UnitPhase Phase { get; private set; } = UnitPhase.None;
    public int TicksToLoad { get; set; } = 1;
    public int TicksToCreate { get; set; } = 1;
    public int TicksToUnload { get; set; } = 1;
    public bool IsDisposed { get; private set; }
    public Unit? LastOwner { get; private set; }

    private int _tickCount;

    public void OnUpdatePhase(Unit owner, UnitPhase phase)
    {
        LastOwner = owner;
        switch (Phase)
        {
            case UnitPhase.None:
                // Waiting for OnChangePhase to start loading
                break;
            case UnitPhase.Loading:
                if (++_tickCount >= TicksToLoad)
                {
                    Phase = UnitPhase.Loaded;
                    _tickCount = 0;
                }
                break;
            case UnitPhase.Loaded:
                // Waiting for OnChangePhase to start creating
                break;
            case UnitPhase.Creating:
                if (++_tickCount >= TicksToCreate)
                {
                    Phase = UnitPhase.Ready;
                    _tickCount = 0;
                }
                break;
            case UnitPhase.Ready:
                // Stable state
                break;
            case UnitPhase.Unloading:
                if (++_tickCount >= TicksToUnload)
                {
                    Phase = UnitPhase.Unloaded;
                }
                break;
        }
    }

    public void OnChangePhase(Unit owner, UnitPhase prev, UnitPhase next)
    {
        LastOwner = owner;
        Phase = next;
        _tickCount = 0;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public class MockUnitDetailA : MockUnitDetail { }
public class MockUnitDetailB : MockUnitDetail { }
public class MockUnitDetailC : MockUnitDetail { }
public class MockUnitDetailD : MockUnitDetail { }

}
