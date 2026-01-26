using System;
using Tomato.TimelineSystem;

namespace Tomato.ActionExecutionSystem.MotionGraph;

/// <summary>
/// モーション定義。
/// </summary>
public sealed class MotionDefinition
{
    /// <summary>モーションID。</summary>
    public string MotionId { get; }

    /// <summary>総フレーム数。</summary>
    public int TotalFrames { get; }

    /// <summary>タイムラインシーケンス。</summary>
    public Sequence Timeline { get; }

    /// <summary>モーションデータ。</summary>
    public IMotionData? MotionData { get; }

    public MotionDefinition(
        string motionId,
        int totalFrames,
        Sequence timeline,
        IMotionData? motionData = null)
    {
        MotionId = motionId ?? throw new ArgumentNullException(nameof(motionId));
        TotalFrames = totalFrames;
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        MotionData = motionData;
    }
}
