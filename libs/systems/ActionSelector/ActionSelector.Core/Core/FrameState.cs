using System;

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
    /// フレーム状態を生成する。
    /// </summary>
    /// <param name="input">入力状態</param>
    /// <param name="context">ゲーム固有コンテキスト</param>
    /// <param name="deltaTime">経過時間（秒）</param>
    /// <param name="totalTime">総経過時間（秒）</param>
    /// <param name="frameCount">フレーム番号</param>
    public FrameState(
        TInput input,
        TContext context,
        float deltaTime = 1f / 60f,
        float totalTime = 0f,
        int frameCount = 0)
    {
        Input = input;
        Context = context;
        DeltaTime = deltaTime;
        TotalTime = totalTime;
        FrameCount = frameCount;
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
    /// 前フレームからの経過時間（秒）。
    /// </summary>
    public readonly float DeltaTime;

    /// <summary>
    /// フレーム状態を生成する。
    /// </summary>
    public InputOnlyFrame(TInput input, float deltaTime = 1f / 60f)
    {
        Input = input;
        DeltaTime = deltaTime;
    }
}
