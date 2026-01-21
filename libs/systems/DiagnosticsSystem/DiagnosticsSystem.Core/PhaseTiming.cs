namespace Tomato.DiagnosticsSystem;

/// <summary>
/// フェーズの計測結果。
/// </summary>
public readonly struct PhaseTiming
{
    /// <summary>フェーズ名</summary>
    public readonly string PhaseName;

    /// <summary>経過時間（ミリ秒）</summary>
    public readonly double ElapsedMs;

    public PhaseTiming(string phaseName, double elapsedMs)
    {
        PhaseName = phaseName;
        ElapsedMs = elapsedMs;
    }

    public override string ToString() => $"{PhaseName}: {ElapsedMs:F3}ms";
}
