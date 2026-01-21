using System;

namespace Tomato.ActionSelector;

/// <summary>
/// ProcessFrame の動作を制御するオプションフラグ。
/// </summary>
[Flags]
public enum ProcessFrameOptions
{
    /// <summary>
    /// 通常モード。すべての入力判定を実行する。
    /// </summary>
    None = 0,

    /// <summary>
    /// ForceInput されたジャッジメントのみを評価する。
    ///
    /// このフラグが設定されている場合:
    /// - IsForcedInput が true のジャッジメントのみが入力成立とみなされる
    /// - 通常の入力判定処理（Input.IsTriggered）をスキップする
    /// - ライフサイクル管理（OnJudgmentUpdate 等）もスキップする
    ///
    /// AI制御時に使用することで、不要な入力判定処理を省きパフォーマンスを向上させる。
    /// </summary>
    /// <example>
    /// <code>
    /// // AI制御時
    /// retreat.ForceInput();
    /// var result = engine.ProcessFrame(list, state, ProcessFrameOptions.ForcedInputOnly);
    /// retreat.ClearForceInput();
    /// </code>
    /// </example>
    ForcedInputOnly = 1,
}
