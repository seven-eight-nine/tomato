using System;

namespace Tomato.CharacterSpawnSystem;

/// <summary>
/// 状態判定ヘルパー
/// </summary>
public static class CharacterStateHelper
{
    /// <summary>
    /// ロード中か
    /// </summary>
    public static bool IsLoading(CharacterInternalState state)
    {
        return state == CharacterInternalState.PlacedDataLoading ||
               state == CharacterInternalState.InstantiatingGOLoading;
    }

    /// <summary>
    /// エラー状態か
    /// </summary>
    public static bool IsError(CharacterInternalState state)
    {
        return state == CharacterInternalState.DataLoadFailed ||
               state == CharacterInternalState.GameObjectLoadFailed;
    }

    /// <summary>
    /// ゲームオブジェクトを持っているか
    /// </summary>
    public static bool HasGameObject(CharacterInternalState state)
    {
        return state == CharacterInternalState.InstantiatedInactive ||
               state == CharacterInternalState.InstantiatedActive;
    }

    /// <summary>
    /// アクティブか
    /// </summary>
    public static bool IsActive(CharacterInternalState state)
    {
        return state == CharacterInternalState.InstantiatedActive;
    }

    /// <summary>
    /// 安定状態か（遷移中でないか）
    /// </summary>
    public static bool IsStable(CharacterInternalState state)
    {
        return !IsLoading(state);
    }
}
