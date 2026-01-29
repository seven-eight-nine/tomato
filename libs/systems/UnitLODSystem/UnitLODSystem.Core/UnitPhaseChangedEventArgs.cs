using System;

namespace Tomato.UnitLODSystem
{

public class UnitPhaseChangedEventArgs : EventArgs
{
    public Type DetailType { get; }
    public UnitPhase OldPhase { get; }
    public UnitPhase NewPhase { get; }

    public UnitPhaseChangedEventArgs(Type detailType, UnitPhase oldPhase, UnitPhase newPhase)
    {
        DetailType = detailType;
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }
}

public delegate void UnitPhaseChangedEventHandler(object sender, UnitPhaseChangedEventArgs e);

}
