using System;
using System.Numerics;
using MathF = Tomato.Math.MathF;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// 線形補間によるモーションデータ。
/// 開始位置から終了位置まで線形に移動する。
/// </summary>
public sealed class LinearMotionData : IMotionData
{
    private readonly Vector3 _startPosition;
    private readonly Vector3 _endPosition;

    public float Duration { get; }

    public LinearMotionData(float duration, Vector3 startPosition, Vector3 endPosition)
    {
        Duration = duration;
        _startPosition = startPosition;
        _endPosition = endPosition;
    }

    public MotionFrame Evaluate(float time)
    {
        if (Duration <= 0)
        {
            return new MotionFrame(_startPosition, Quaternion.Identity);
        }

        float t = MathF.Max(0f, MathF.Min(1f, time / Duration));
        var position = Vector3.Lerp(_startPosition, _endPosition, t);
        return new MotionFrame(position, Quaternion.Identity);
    }
}
