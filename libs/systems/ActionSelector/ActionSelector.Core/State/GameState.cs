using System;

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
    /// 前フレームからの経過時間（秒）。
    /// </summary>
    public readonly float DeltaTime;

    /// <summary>
    /// ゲーム開始からの総経過時間（秒）。
    /// </summary>
    public readonly float TotalTime;

    /// <summary>
    /// 現在のフレーム番号。
    /// </summary>
    public readonly int FrameCount;

    /// <summary>
    /// ユーザー定義のフラグ（ビットマスク）。
    /// </summary>
    public readonly uint Flags;

    public GameState(
        InputState input,
        IResourceState? resources = null,
        float deltaTime = 1f / 60f,
        float totalTime = 0f,
        int frameCount = 0,
        uint flags = 0)
    {
        Input = input;
        Resources = resources ?? EmptyResourceState.Instance;
        DeltaTime = deltaTime;
        TotalTime = totalTime;
        FrameCount = frameCount;
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
