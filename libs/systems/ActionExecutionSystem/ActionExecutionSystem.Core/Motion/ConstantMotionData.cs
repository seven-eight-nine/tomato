using System.Numerics;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// 一定値のモーションデータ。
/// 常に同じ位置を返す。
/// </summary>
public sealed class ConstantMotionData : IMotionData
{
    private readonly MotionFrame _frame;

    public float Duration { get; }

    public ConstantMotionData(float duration, Vector3 position)
    {
        Duration = duration;
        _frame = new MotionFrame(position, Quaternion.Identity);
    }

    public ConstantMotionData(float duration, MotionFrame frame)
    {
        Duration = duration;
        _frame = frame;
    }

    public MotionFrame Evaluate(float time) => _frame;
}
