using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// 現在実行中のアクションの状態。
///
/// コンボ遷移システムの中核。実行中アクションが次に遷移可能な
/// ジャッジメント群を提供する。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <remarks>
/// 設計意図:
/// ジャッジメントは「候補」であり、実際に選択されるアクションは状態によって変わる。
/// 同一のジャッジメントが異なるアクションを選択することもありうる。
/// したがって、次に遷移可能なジャッジメントは、実行されたアクション側からしか
/// 決定できない。
///
/// 使用例:
/// <code>
/// var action = new ComboAction&lt;ActionCategory&gt;(
///     "Attack1",
///     cancelStartTick: 10,
///     cancelEndTick: 30,
///     transitionTargets: new[] { attack2Judge, launcherJudge });
/// </code>
/// </remarks>
public interface IRunningAction<TCategory> where TCategory : struct, Enum
{
    /// <summary>
    /// 実行中のアクションのラベル。
    /// </summary>
    string Label { get; }

    /// <summary>
    /// 実行開始からの経過tick数。
    /// </summary>
    int ElapsedTicks { get; }

    /// <summary>
    /// 現在キャンセル（遷移）可能かどうか。
    /// </summary>
    /// <remarks>
    /// true の場合のみ GetTransitionableJudgments() が有効な値を返す。
    /// キャンセルウィンドウ内かどうかで判定される。
    /// </remarks>
    bool CanCancel { get; }

    /// <summary>
    /// このアクションから遷移可能なジャッジメント群を返す。
    /// </summary>
    /// <returns>
    /// CanCancel == true の場合: 遷移可能なジャッジメント群
    /// CanCancel == false の場合: 空の Span
    /// </returns>
    /// <remarks>
    /// プロバイダがこのメソッドを呼び、エンジンに渡すジャッジメントを決定する。
    /// </remarks>
    ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetTransitionableJudgments();

    /// <summary>
    /// tick進行。
    /// </summary>
    /// <param name="deltaTicks">前フレームからの経過tick数</param>
    void Tick(int deltaTicks);
}

/// <summary>
/// コンボ遷移対応のアクション実装。
///
/// キャンセルウィンドウと遷移先ジャッジメントを持つ標準的な実装。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class ComboAction<TCategory> : IRunningAction<TCategory>
    where TCategory : struct, Enum
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly string _label;
    private readonly int _cancelStartTick;
    private readonly int _cancelEndTick;
    private readonly IActionJudgment<TCategory, InputState, GameState>[] _transitionTargets;

    private int _elapsedTicks;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// コンボ対応アクションを生成する。
    /// </summary>
    /// <param name="label">アクションラベル</param>
    /// <param name="cancelStartTick">キャンセル開始tick</param>
    /// <param name="cancelEndTick">キャンセル終了tick</param>
    /// <param name="transitionTargets">遷移先ジャッジメント群</param>
    public ComboAction(
        string label,
        int cancelStartTick,
        int cancelEndTick,
        IActionJudgment<TCategory, InputState, GameState>[] transitionTargets)
    {
        _label = label ?? throw new ArgumentNullException(nameof(label));
        _cancelStartTick = cancelStartTick;
        _cancelEndTick = cancelEndTick;
        _transitionTargets = transitionTargets ?? Array.Empty<IActionJudgment<TCategory, InputState, GameState>>();
    }

    /// <summary>
    /// キャンセル不可のアクションを生成する。
    /// </summary>
    public ComboAction(string label)
        : this(label, int.MaxValue, int.MaxValue, Array.Empty<IActionJudgment<TCategory, InputState, GameState>>())
    {
    }

    // ===========================================
    // IRunningAction 実装
    // ===========================================

    public string Label => _label;
    public int ElapsedTicks => _elapsedTicks;

    public bool CanCancel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _elapsedTicks >= _cancelStartTick && _elapsedTicks <= _cancelEndTick;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetTransitionableJudgments()
    {
        return CanCancel ? _transitionTargets : ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>>.Empty;
    }

    public void Tick(int deltaTicks)
    {
        _elapsedTicks += deltaTicks;
    }

    // ===========================================
    // デバッグ
    // ===========================================

    public override string ToString() =>
        $"ComboAction[{_label}] Tick:{_elapsedTicks} CanCancel:{CanCancel}";
}

/// <summary>
/// シンプルな実行中アクション。
///
/// 遷移先を持たない単発アクション用。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class SimpleRunningAction<TCategory> : IRunningAction<TCategory>
    where TCategory : struct, Enum
{
    private readonly string _label;
    private int _elapsedTicks;

    public SimpleRunningAction(string label)
    {
        _label = label ?? throw new ArgumentNullException(nameof(label));
    }

    public string Label => _label;
    public int ElapsedTicks => _elapsedTicks;
    public bool CanCancel => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetTransitionableJudgments()
        => ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>>.Empty;

    public void Tick(int deltaTicks)
    {
        _elapsedTicks += deltaTicks;
    }

    public override string ToString() =>
        $"SimpleAction[{_label}] Tick:{_elapsedTicks}";
}
