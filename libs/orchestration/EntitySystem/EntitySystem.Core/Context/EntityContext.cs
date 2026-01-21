using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.ActionExecutionSystem;
using Tomato.CollisionSystem;
using Tomato.CharacterSpawnSystem;
using Tomato.ActionSelector;

namespace Tomato.EntitySystem.Context;

/// <summary>
/// Entity単位のゲームコンテキスト。
/// AnyHandle、ActionStateMachine、CollisionVolumeを統合する。
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
    /// このEntityが発行中の衝突ボリューム。
    /// </summary>
    public List<CollisionVolume> CollisionVolumes { get; }

    /// <summary>
    /// このEntityのジャッジメント群。
    /// </summary>
    public IActionJudgment<TCategory, InputState, GameState>[] Judgments { get; set; }

    /// <summary>
    /// CharacterSpawnControllerへの参照（オプション）。
    /// </summary>
    public CharacterSpawnController? SpawnController { get; set; }

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
        CollisionVolumes = new List<CollisionVolume>();
        Judgments = Array.Empty<IActionJudgment<TCategory, InputState, GameState>>();
        SpawnController = null;
        IsMarkedForDeletion = false;
        IsActive = true;
    }

    /// <summary>
    /// コンテキストをリセットする（再利用時）。
    /// </summary>
    public void Reset()
    {
        // ActionStateMachineは各カテゴリのアクションがnullになる
        // （新しいインスタンスが作られるため、特にリセット不要）
        CollisionVolumes.Clear();
        Judgments = Array.Empty<IActionJudgment<TCategory, InputState, GameState>>();
        SpawnController = null;
        IsMarkedForDeletion = false;
        IsActive = true;
    }
}
