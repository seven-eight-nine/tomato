using System.Numerics;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// モーションの1フレーム分のデータ。
/// </summary>
public readonly struct MotionFrame
{
    /// <summary>前フレームからの位置変化（ローカル座標）。</summary>
    public readonly Vector3 DeltaPosition;

    /// <summary>前フレームからの回転変化。</summary>
    public readonly Quaternion DeltaRotation;

    /// <summary>ポーズデータ（ボーン変換等）へのオフセット。</summary>
    public readonly int PoseIndex;

    public MotionFrame(Vector3 deltaPos, Quaternion deltaRot, int poseIndex = 0)
    {
        DeltaPosition = deltaPos;
        DeltaRotation = deltaRot;
        PoseIndex = poseIndex;
    }

    public static MotionFrame Zero => new(Vector3.Zero, Quaternion.Identity, 0);
}
