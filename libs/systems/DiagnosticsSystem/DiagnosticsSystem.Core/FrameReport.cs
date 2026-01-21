using System.Collections.Generic;
using System.Linq;

namespace Tomato.DiagnosticsSystem;

/// <summary>
/// 1フレームの計測レポート。
/// </summary>
public sealed class FrameReport
{
    /// <summary>フレーム番号</summary>
    public int FrameNumber { get; }

    /// <summary>各フェーズの計測結果</summary>
    public IReadOnlyList<PhaseTiming> PhaseTimings { get; }

    /// <summary>フレーム全体の処理時間（ミリ秒）</summary>
    public double TotalTimeMs { get; }

    public FrameReport(int frameNumber, IReadOnlyList<PhaseTiming> phaseTimings)
    {
        FrameNumber = frameNumber;
        PhaseTimings = phaseTimings;
        TotalTimeMs = phaseTimings.Sum(t => t.ElapsedMs);
    }

    public override string ToString()
    {
        return $"Frame {FrameNumber}: {TotalTimeMs:F3}ms ({PhaseTimings.Count} phases)";
    }
}
