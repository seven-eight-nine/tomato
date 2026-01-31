using System;
using Tomato.Time;

namespace Tomato.ActionSelector;

/// <summary>
/// 現在のゲーム状態。
/// 選択エンジンに渡される読み取り専用の状態スナップショット。
/// </summary>
public readonly struct GameState
{
    /// <summary>
    /// 現在フレームの入力状態。
    /// </summary>
    public readonly InputState Input;

    /// <summary>
    /// リソース状態。
    /// </summary>
    public readonly IResourceState Resources;

    /// <summary>
    /// 前フレームからの経過tick数。
    /// </summary>
    public readonly int DeltaTicks;

    /// <summary>
    /// 現在のゲームtick。
    /// </summary>
    public readonly GameTick CurrentTick;

    /// <summary>
    /// ユーザー定義のフラグ（ビットマスク）。
    /// </summary>
    public readonly uint Flags;

    public GameState(
        InputState input,
        IResourceState? resources = null,
        int deltaTicks = 1,
        GameTick currentTick = default,
        uint flags = 0)
    {
        Input = input;
        Resources = resources ?? EmptyResourceState.Instance;
        DeltaTicks = deltaTicks;
        CurrentTick = currentTick;
        Flags = flags;
    }

    /// <summary>
    /// 指定フラグが設定されているか。
    /// </summary>
    public bool HasFlag(uint flag) => (Flags & flag) == flag;

    /// <summary>
    /// いずれかのフラグが設定されているか。
    /// </summary>
    public bool HasAnyFlag(uint flags) => (Flags & flags) != 0;

    /// <summary>
    /// デフォルト状態。
    /// </summary>
    public static readonly GameState Default = new(InputState.Empty);
}
