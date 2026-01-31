using System;
using Tomato.Time;

namespace Tomato.ActionSelector;

/// <summary>
/// 1フレームの状態を表す汎用構造体。
///
/// 入力状態とゲーム固有コンテキストを保持する。
/// </summary>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
/// <remarks>
/// パフォーマンス最適化:
/// - readonly struct で値渡し
/// - in 修飾子で参照渡し（大きな struct のコピー回避）
///
/// 使用例（格闘ゲーム）:
/// <code>
/// // 型エイリアスを定義
/// using FighterFrame = FrameState&lt;FighterInput, FighterContext&gt;;
///
/// void ProcessFrame(in FighterFrame state)
/// {
///     if (state.Context.Character.IsGrounded)
///     {
///         // 接地中の処理
///     }
/// }
/// </code>
/// </remarks>
public readonly struct FrameState<TInput, TContext>
{
    /// <summary>
    /// 現在フレームの入力状態。
    /// </summary>
    public readonly TInput Input;

    /// <summary>
    /// ゲーム固有のコンテキスト。
    /// </summary>
    /// <remarks>
    /// キャラクター状態、戦闘状態、リソース状態など、
    /// ゲームに応じた情報を格納する。
    /// </remarks>
    public readonly TContext Context;

    /// <summary>
    /// 前フレームからの経過tick数。
    /// </summary>
    public readonly int DeltaTicks;

    /// <summary>
    /// 現在のゲームtick。
    /// </summary>
    public readonly GameTick CurrentTick;

    /// <summary>
    /// フレーム状態を生成する。
    /// </summary>
    /// <param name="input">入力状態</param>
    /// <param name="context">ゲーム固有コンテキスト</param>
    /// <param name="deltaTicks">経過tick数</param>
    /// <param name="currentTick">現在のゲームtick</param>
    public FrameState(
        TInput input,
        TContext context,
        int deltaTicks = 1,
        GameTick currentTick = default)
    {
        Input = input;
        Context = context;
        DeltaTicks = deltaTicks;
        CurrentTick = currentTick;
    }
}

/// <summary>
/// 入力のみを持つフレーム状態（コンテキスト不要の場合）。
/// </summary>
/// <typeparam name="TInput">入力状態の型</typeparam>
public readonly struct InputOnlyFrame<TInput>
{
    /// <summary>
    /// 現在フレームの入力状態。
    /// </summary>
    public readonly TInput Input;

    /// <summary>
    /// 前フレームからの経過tick数。
    /// </summary>
    public readonly int DeltaTicks;

    /// <summary>
    /// フレーム状態を生成する。
    /// </summary>
    public InputOnlyFrame(TInput input, int deltaTicks = 1)
    {
        Input = input;
        DeltaTicks = deltaTicks;
    }
}
