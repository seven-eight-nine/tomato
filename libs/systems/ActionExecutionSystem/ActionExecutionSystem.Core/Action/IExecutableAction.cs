using System;
using Tomato.ActionSelector;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// 実行中アクションのインターフェース。
/// ActionSelectorのIRunningActionを実装しつつ、実行に必要な機能を追加。
/// </summary>
public interface IExecutableAction<TCategory> : IRunningAction<TCategory>
    where TCategory : struct, Enum
{
    /// <summary>アクションID。</summary>
    string ActionId { get; }

    /// <summary>カテゴリ。</summary>
    TCategory Category { get; }

    /// <summary>アクションが完了したか。</summary>
    bool IsComplete { get; }

    /// <summary>モーションデータ。</summary>
    IMotionData? MotionData { get; }

    /// <summary>アクション開始時に呼ばれる。</summary>
    void OnEnter();

    /// <summary>アクション終了時に呼ばれる。</summary>
    void OnExit();
}
