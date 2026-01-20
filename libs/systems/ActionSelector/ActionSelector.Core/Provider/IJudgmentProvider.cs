using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// エンジンにジャッジメントを供給するプロバイダ。
///
/// ゲーム状態と実行中アクションに応じて、評価対象のジャッジメント群を決定する。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <remarks>
/// 使用パターン:
/// <code>
/// var judgments = provider.GetActiveJudgments(in state, currentAction);
/// var result = engine.ProcessFrame(judgments, in state);
/// </code>
/// </remarks>
public interface IJudgmentProvider<TCategory> where TCategory : struct, Enum
{
    /// <summary>
    /// 現在アクティブなジャッジメント群を返す。
    /// </summary>
    /// <param name="state">現在のゲーム状態</param>
    /// <param name="currentAction">現在実行中のアクション（ない場合は null）</param>
    /// <returns>評価対象のジャッジメント群</returns>
    ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetActiveJudgments(
        in GameState state,
        IRunningAction<TCategory>? currentAction);
}

/// <summary>
/// シンプルなジャッジメントプロバイダ。
///
/// 常に固定のジャッジメント群を返す。コンボ遷移を考慮しない。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class SimpleProvider<TCategory> : IJudgmentProvider<TCategory>
    where TCategory : struct, Enum
{
    private readonly IActionJudgment<TCategory, InputState, GameState>[] _judgments;

    /// <summary>
    /// シンプルプロバイダを生成する。
    /// </summary>
    public SimpleProvider(IActionJudgment<TCategory, InputState, GameState>[] judgments)
    {
        _judgments = judgments ?? throw new ArgumentNullException(nameof(judgments));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetActiveJudgments(
        in GameState state,
        IRunningAction<TCategory>? currentAction)
    {
        return _judgments;
    }
}

/// <summary>
/// コンボ遷移対応のプロバイダ。
///
/// 実行中アクションから遷移可能なジャッジメントと、
/// ベースジャッジメントを適切に切り替える。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <remarks>
/// 動作:
/// - currentAction が null or CanCancel == false → ベースジャッジメントを返す
/// - currentAction.CanCancel == true → 遷移可能ジャッジメントを返す
///
/// 使用例:
/// <code>
/// var provider = new ComboAwareProvider&lt;ActionCategory&gt;(baseJudgments);
/// var judgments = provider.GetActiveJudgments(in state, currentAction);
/// </code>
/// </remarks>
public sealed class ComboAwareProvider<TCategory> : IJudgmentProvider<TCategory>
    where TCategory : struct, Enum
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly IActionJudgment<TCategory, InputState, GameState>[] _baseJudgments;
    private readonly IActionJudgment<TCategory, InputState, GameState>[] _buffer;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// コンボ対応プロバイダを生成する。
    /// </summary>
    /// <param name="baseJudgments">ベースとなるジャッジメント群</param>
    /// <param name="maxTransitionTargets">遷移先の最大数（デフォルト: ベースの2倍）</param>
    public ComboAwareProvider(
        IActionJudgment<TCategory, InputState, GameState>[] baseJudgments,
        int maxTransitionTargets = 0)
    {
        _baseJudgments = baseJudgments ?? throw new ArgumentNullException(nameof(baseJudgments));

        int bufferSize = maxTransitionTargets > 0
            ? maxTransitionTargets
            : baseJudgments.Length * 2;
        _buffer = new IActionJudgment<TCategory, InputState, GameState>[bufferSize];
    }

    // ===========================================
    // IJudgmentProvider 実装
    // ===========================================

    public ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetActiveJudgments(
        in GameState state,
        IRunningAction<TCategory>? currentAction)
    {
        // アクションがない or キャンセル不可 → ベースを返す
        if (currentAction == null || !currentAction.CanCancel)
        {
            return _baseJudgments;
        }

        // キャンセル可能 → 遷移先のみを返す
        var transitionable = currentAction.GetTransitionableJudgments();
        if (transitionable.IsEmpty)
        {
            // 遷移先がない場合はベースを返す
            return _baseJudgments;
        }

        // バッファにコピー
        int count = Math.Min(transitionable.Length, _buffer.Length);
        for (int i = 0; i < count; i++)
        {
            _buffer[i] = transitionable[i];
        }

        return _buffer.AsSpan(0, count);
    }
}

/// <summary>
/// マージプロバイダ。
///
/// 遷移可能ジャッジメントとベースジャッジメントの両方を返す。
/// より柔軟なコンボシステム向け。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <remarks>
/// ComboAwareProvider との違い:
/// - ComboAwareProvider: キャンセル可能時は遷移先のみ
/// - MergeProvider: キャンセル可能時は遷移先 + ベース（優先度は遷移先が高い想定）
/// </remarks>
public sealed class MergeProvider<TCategory> : IJudgmentProvider<TCategory>
    where TCategory : struct, Enum
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly IActionJudgment<TCategory, InputState, GameState>[] _baseJudgments;
    private readonly IActionJudgment<TCategory, InputState, GameState>[] _buffer;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// マージプロバイダを生成する。
    /// </summary>
    public MergeProvider(
        IActionJudgment<TCategory, InputState, GameState>[] baseJudgments,
        int maxTransitionTargets = 0)
    {
        _baseJudgments = baseJudgments ?? throw new ArgumentNullException(nameof(baseJudgments));

        int bufferSize = maxTransitionTargets > 0
            ? baseJudgments.Length + maxTransitionTargets
            : baseJudgments.Length * 2;
        _buffer = new IActionJudgment<TCategory, InputState, GameState>[bufferSize];
    }

    // ===========================================
    // IJudgmentProvider 実装
    // ===========================================

    public ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetActiveJudgments(
        in GameState state,
        IRunningAction<TCategory>? currentAction)
    {
        int count = 0;

        // 1. 遷移可能ジャッジメントを追加（優先）
        if (currentAction != null && currentAction.CanCancel)
        {
            var transitionable = currentAction.GetTransitionableJudgments();
            for (int i = 0; i < transitionable.Length && count < _buffer.Length; i++)
            {
                _buffer[count++] = transitionable[i];
            }
        }

        // 2. ベースジャッジメントを追加
        for (int i = 0; i < _baseJudgments.Length && count < _buffer.Length; i++)
        {
            _buffer[count++] = _baseJudgments[i];
        }

        return _buffer.AsSpan(0, count);
    }
}

/// <summary>
/// 条件付きプロバイダ。
///
/// 複数のプロバイダを切り替える。
/// ゲームモードやキャラクター状態に応じた切り替えに使用。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class ConditionalProvider<TCategory> : IJudgmentProvider<TCategory>
    where TCategory : struct, Enum
{
    private readonly Func<GameState, IJudgmentProvider<TCategory>> _selector;

    /// <summary>
    /// 条件付きプロバイダを生成する。
    /// </summary>
    /// <param name="selector">状態に応じてプロバイダを選択する関数</param>
    public ConditionalProvider(Func<GameState, IJudgmentProvider<TCategory>> selector)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetActiveJudgments(
        in GameState state,
        IRunningAction<TCategory>? currentAction)
    {
        var provider = _selector(state);
        return provider.GetActiveJudgments(in state, currentAction);
    }
}
