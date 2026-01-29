namespace Tomato.UnitLODSystem
{

public static class UnitHelper
{
    public static bool IsLoading(UnitPhase phase)
    {
        return phase == UnitPhase.Loading;
    }

    public static bool IsStable(UnitPhase phase)
    {
        return phase == UnitPhase.Ready;
    }

    public static bool IsUnloading(UnitPhase phase)
    {
        return phase == UnitPhase.Unloading;
    }

    public static bool CanDispose(UnitPhase phase)
    {
        return phase == UnitPhase.Unloaded;
    }
}

}
