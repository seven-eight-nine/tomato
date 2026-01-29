using System;
using Tomato.EntityHandleSystem;
using Tomato.ActionExecutionSystem;
using Tomato.UnitLODSystem;
using Tomato.ActionSelector;

namespace Tomato.GameLoop.Context;

/// <summary>
/// Entity単位のゲームコンテキスト。
/// AnyHandle、ActionStateMachineを統合する。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class EntityContext<TCategory> where TCategory : struct, Enum
{
    /// <summary>
    /// EntityのAnyHandle。
    /// </summary>
    public AnyHandle Handle { get; }

    /// <summary>
    /// このEntityのアクション状態機械。
    /// </summary>
    public ActionStateMachine<TCategory> ActionStateMachine { get; }

    /// <summary>
    /// このEntityのジャッジメント群。
    /// </summary>
    public IActionJudgment<TCategory, InputState, GameState>[] Judgments { get; set; }

    /// <summary>
    /// Unitへの参照（オプション）。
    /// </summary>
    public Unit? Unit { get; set; }

    /// <summary>
    /// 削除マークされているかどうか。
    /// </summary>
    public bool IsMarkedForDeletion { get; internal set; }

    /// <summary>
    /// アクティブかどうか。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// EntityContextを生成する。
    /// </summary>
    /// <param name="handle">EntityのAnyHandle</param>
    public EntityContext(AnyHandle handle)
    {
        Handle = handle;
        ActionStateMachine = new ActionStateMachine<TCategory>();
        Judgments = Array.Empty<IActionJudgment<TCategory, InputState, GameState>>();
        Unit = null;
        IsMarkedForDeletion = false;
        IsActive = true;
    }

    /// <summary>
    /// コンテキストをリセットする（再利用時）。
    /// </summary>
    public void Reset()
    {
        Judgments = Array.Empty<IActionJudgment<TCategory, InputState, GameState>>();
        Unit = null;
        IsMarkedForDeletion = false;
        IsActive = true;
    }
}
