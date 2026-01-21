using System;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// アクションの定義データ。
/// </summary>
public sealed class ActionDefinition<TCategory> where TCategory : struct, Enum
{
    /// <summary>アクションID。</summary>
    public readonly string ActionId;

    /// <summary>カテゴリ。</summary>
    public readonly TCategory Category;

    /// <summary>総フレーム数。</summary>
    public readonly int TotalFrames;

    /// <summary>キャンセルウィンドウ。</summary>
    public readonly FrameWindow CancelWindow;

    /// <summary>ヒットボックス発生ウィンドウ（攻撃アクションの場合）。</summary>
    public readonly FrameWindow? HitboxWindow;

    /// <summary>無敵ウィンドウ（回避アクションの場合）。</summary>
    public readonly FrameWindow? InvincibleWindow;

    /// <summary>モーションデータ。</summary>
    public readonly IMotionData? MotionData;

    public ActionDefinition(
        string actionId,
        TCategory category,
        int totalFrames,
        FrameWindow cancelWindow,
        FrameWindow? hitboxWindow = null,
        FrameWindow? invincibleWindow = null,
        IMotionData? motionData = null)
    {
        ActionId = actionId;
        Category = category;
        TotalFrames = totalFrames;
        CancelWindow = cancelWindow;
        HitboxWindow = hitboxWindow;
        InvincibleWindow = invincibleWindow;
        MotionData = motionData;
    }
}
