using System.Collections.Generic;
using System.Text;

namespace Tomato.DiagnosticsSystem;

/// <summary>
/// 診断レポート。複数フレームの統計情報。
/// </summary>
public sealed class DiagnosticsReport
{
    /// <summary>計測フレーム数</summary>
    public int FrameCount { get; init; }

    /// <summary>平均フレーム時間（ミリ秒）</summary>
    public double AverageFrameTimeMs { get; init; }

    /// <summary>最大フレーム時間（ミリ秒）</summary>
    public double MaxFrameTimeMs { get; init; }

    /// <summary>最小フレーム時間（ミリ秒）</summary>
    public double MinFrameTimeMs { get; init; }

    /// <summary>フェーズ別の平均時間</summary>
    public IReadOnlyDictionary<string, double> PhaseAveragesMs { get; init; } = new Dictionary<string, double>();

    /// <summary>フェーズ別の最大時間</summary>
    public IReadOnlyDictionary<string, double> PhaseMaxMs { get; init; } = new Dictionary<string, double>();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Diagnostics Report ({FrameCount} frames) ===");
        sb.AppendLine($"Frame Time: avg={AverageFrameTimeMs:F3}ms, max={MaxFrameTimeMs:F3}ms, min={MinFrameTimeMs:F3}ms");
        sb.AppendLine("Phase Breakdown:");
        foreach (var (phase, avg) in PhaseAveragesMs)
        {
            var max = PhaseMaxMs.TryGetValue(phase, out var m) ? m : 0;
            sb.AppendLine($"  {phase}: avg={avg:F3}ms, max={max:F3}ms");
        }
        return sb.ToString();
    }
}
