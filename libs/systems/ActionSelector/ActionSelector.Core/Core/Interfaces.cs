using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

// =====================================================
// 汎用インターフェース
// =====================================================

/// <summary>
/// ゲームコンテキストの状態条件を評価する汎用インターフェース。
/// </summary>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
/// <remarks>
/// パフォーマンス:
/// - TContext は in 修飾子で参照渡し（コピー回避）
/// </remarks>
public interface ICondition<TContext>
{
    /// <summary>
    /// 現在のコンテキストで条件が満たされているかを評価する。
    /// </summary>
    /// <param name="context">ゲーム固有コンテキスト（読み取り専用）</param>
    /// <returns>条件が満たされていれば true</returns>
    bool Evaluate(in TContext context);
}

/// <summary>
/// 入力条件を判定する汎用トリガー。
/// </summary>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <remarks>
/// ライフサイクル:
/// 1. OnJudgmentStart() - ジャッジメントがアクティブになった時（1回）
/// 2. OnJudgmentUpdate() - 毎tick（アクティブな間）
/// 3. OnJudgmentStop() - ジャッジメントが非アクティブになった時（1回）
/// </remarks>
public interface IInputTrigger<TInput>
{
    /// <summary>
    /// 現在の入力でトリガーされているかを返す。
    /// </summary>
    bool IsTriggered(in TInput input);

    /// <summary>
    /// ジャッジメントがアクティブになった時に呼ばれる。
    /// </summary>
    void OnJudgmentStart();

    /// <summary>
    /// ジャッジメントが非アクティブになった時に呼ばれる。
    /// </summary>
    void OnJudgmentStop();

    /// <summary>
    /// 毎tick呼ばれる。
    /// </summary>
    void OnJudgmentUpdate(in TInput input, int deltaTicks);
}

/// <summary>
/// ジャッジメント成立時のアクション解決を行う汎用インターフェース。
/// </summary>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
public interface IActionResolver<TInput, TContext>
{
    /// <summary>
    /// 成立時に実行するアクションを解決する。
    /// </summary>
    ResolvedAction Resolve(in FrameState<TInput, TContext> state);
}

/// <summary>
/// アクションの成立条件を表明する汎用ジャッジメント。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
public interface IActionJudgment<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    /// <summary>
    /// ジャッジメントのラベル（識別用）。
    /// </summary>
    string Label { get; }

    /// <summary>
    /// 所属カテゴリ。
    /// </summary>
    TCategory Category { get; }

    /// <summary>
    /// 入力条件。null の場合、入力は不成立とみなす。
    /// </summary>
    IInputTrigger<TInput>? Input { get; }

    /// <summary>
    /// 状態条件。null の場合、状態条件は常に成立とみなす。
    /// </summary>
    ICondition<TContext>? Condition { get; }

    /// <summary>
    /// 現在の状態における優先度を返す。
    /// </summary>
    ActionPriority GetPriority(in FrameState<TInput, TContext> state);

    /// <summary>
    /// 分類タグ（デバッグ・フィルタリング用）。
    /// </summary>
    ReadOnlySpan<string> Tags { get; }

    /// <summary>
    /// アクションリゾルバ（任意）。
    /// </summary>
    IActionResolver<TInput, TContext>? Resolver { get; }
}

/// <summary>
/// 外部から制御可能な汎用ジャッジメント。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
public interface IControllableJudgment<TCategory, TInput, TContext>
    : IActionJudgment<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    /// <summary>
    /// 入力が強制的に成立状態かどうか。
    /// </summary>
    bool IsForcedInput { get; }

    /// <summary>
    /// 入力を強制的に成立状態にする。
    /// </summary>
    void ForceInput();

    /// <summary>
    /// 強制成立状態を解除する。
    /// </summary>
    void ClearForceInput();

    /// <summary>
    /// 入力の内部状態をリセットする。
    /// </summary>
    void ResetInput();
}

// =====================================================
// 汎用ユーティリティ
// =====================================================

/// <summary>
/// 常に成立する汎用条件。
/// </summary>
public sealed class AlwaysCondition<TContext> : ICondition<TContext>
{
    public static readonly AlwaysCondition<TContext> Instance = new();
    private AlwaysCondition() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in TContext context) => true;
}

/// <summary>
/// 決して成立しない汎用条件。
/// </summary>
public sealed class NeverCondition<TContext> : ICondition<TContext>
{
    public static readonly NeverCondition<TContext> Instance = new();
    private NeverCondition() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in TContext context) => false;
}

/// <summary>
/// デリゲートベースの汎用条件。
/// </summary>
public sealed class DelegateCondition<TContext> : ICondition<TContext>
{
    private readonly Func<TContext, bool> _predicate;

    public DelegateCondition(Func<TContext, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in TContext context) => _predicate(context);
}

/// <summary>
/// 常にトリガーする汎用トリガー。
/// </summary>
public sealed class AlwaysTrigger<TInput> : IInputTrigger<TInput>
{
    public static readonly AlwaysTrigger<TInput> Instance = new();
    private AlwaysTrigger() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in TInput input) => true;

    public void OnJudgmentStart() { }
    public void OnJudgmentStop() { }
    public void OnJudgmentUpdate(in TInput input, int deltaTicks) { }
}

/// <summary>
/// 決してトリガーしない汎用トリガー。
/// </summary>
public sealed class NeverTrigger<TInput> : IInputTrigger<TInput>
{
    public static readonly NeverTrigger<TInput> Instance = new();
    private NeverTrigger() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in TInput input) => false;

    public void OnJudgmentStart() { }
    public void OnJudgmentStop() { }
    public void OnJudgmentUpdate(in TInput input, int deltaTicks) { }
}

/// <summary>
/// デリゲートベースの汎用リゾルバ。
/// </summary>
public sealed class DelegateResolver<TInput, TContext> : IActionResolver<TInput, TContext>
{
    private readonly Func<FrameState<TInput, TContext>, ResolvedAction> _resolver;

    public DelegateResolver(Func<FrameState<TInput, TContext>, ResolvedAction> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public ResolvedAction Resolve(in FrameState<TInput, TContext> state) => _resolver(state);
}

// =====================================================
// 条件の論理演算（汎用版）
// =====================================================

/// <summary>
/// 条件の反転（汎用版）。
/// </summary>
public sealed class NotCondition<TContext> : ICondition<TContext>
{
    private readonly ICondition<TContext> _inner;

    public NotCondition(ICondition<TContext> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in TContext context) => !_inner.Evaluate(in context);
}

/// <summary>
/// すべての条件が成立することを要求（汎用版）。
/// </summary>
public sealed class AllCondition<TContext> : ICondition<TContext>
{
    private readonly ICondition<TContext>[] _conditions;

    public AllCondition(params ICondition<TContext>[] conditions)
    {
        _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in TContext context)
    {
        for (int i = 0; i < _conditions.Length; i++)
        {
            if (!_conditions[i].Evaluate(in context))
                return false;
        }
        return true;
    }
}

/// <summary>
/// いずれかの条件が成立することを要求（汎用版）。
/// </summary>
public sealed class AnyCondition<TContext> : ICondition<TContext>
{
    private readonly ICondition<TContext>[] _conditions;

    public AnyCondition(params ICondition<TContext>[] conditions)
    {
        _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in TContext context)
    {
        for (int i = 0; i < _conditions.Length; i++)
        {
            if (_conditions[i].Evaluate(in context))
                return true;
        }
        return false;
    }
}

// =====================================================
// トリガーの論理演算（汎用版）
// =====================================================

/// <summary>
/// すべてのトリガーが成立することを要求（汎用版）。
/// </summary>
public sealed class AllTrigger<TInput> : IInputTrigger<TInput>
{
    private readonly IInputTrigger<TInput>[] _triggers;

    public AllTrigger(params IInputTrigger<TInput>[] triggers)
    {
        _triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in TInput input)
    {
        for (int i = 0; i < _triggers.Length; i++)
        {
            if (!_triggers[i].IsTriggered(in input))
                return false;
        }
        return true;
    }

    public void OnJudgmentStart()
    {
        for (int i = 0; i < _triggers.Length; i++)
            _triggers[i].OnJudgmentStart();
    }

    public void OnJudgmentStop()
    {
        for (int i = 0; i < _triggers.Length; i++)
            _triggers[i].OnJudgmentStop();
    }

    public void OnJudgmentUpdate(in TInput input, int deltaTicks)
    {
        for (int i = 0; i < _triggers.Length; i++)
            _triggers[i].OnJudgmentUpdate(in input, deltaTicks);
    }
}

/// <summary>
/// いずれかのトリガーが成立することを要求（汎用版）。
/// </summary>
public sealed class AnyTrigger<TInput> : IInputTrigger<TInput>
{
    private readonly IInputTrigger<TInput>[] _triggers;

    public AnyTrigger(params IInputTrigger<TInput>[] triggers)
    {
        _triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in TInput input)
    {
        for (int i = 0; i < _triggers.Length; i++)
        {
            if (_triggers[i].IsTriggered(in input))
                return true;
        }
        return false;
    }

    public void OnJudgmentStart()
    {
        for (int i = 0; i < _triggers.Length; i++)
            _triggers[i].OnJudgmentStart();
    }

    public void OnJudgmentStop()
    {
        for (int i = 0; i < _triggers.Length; i++)
            _triggers[i].OnJudgmentStop();
    }

    public void OnJudgmentUpdate(in TInput input, int deltaTicks)
    {
        for (int i = 0; i < _triggers.Length; i++)
            _triggers[i].OnJudgmentUpdate(in input, deltaTicks);
    }
}

// =====================================================
// 条件の拡張メソッド（汎用版）
// =====================================================

/// <summary>
/// 汎用条件の拡張メソッド。
/// </summary>
public static class GenericConditionExtensions
{
    /// <summary>
    /// 条件を反転する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<TContext> Not<TContext>(this ICondition<TContext> condition)
        => new NotCondition<TContext>(condition);

    /// <summary>
    /// AND 条件を作成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<TContext> And<TContext>(
        this ICondition<TContext> a,
        ICondition<TContext> b)
        => new AllCondition<TContext>(a, b);

    /// <summary>
    /// OR 条件を作成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<TContext> Or<TContext>(
        this ICondition<TContext> a,
        ICondition<TContext> b)
        => new AnyCondition<TContext>(a, b);
}

/// <summary>
/// 汎用トリガーの拡張メソッド。
/// </summary>
public static class GenericTriggerExtensions
{
    /// <summary>
    /// AND トリガーを作成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<TInput> And<TInput>(
        this IInputTrigger<TInput> a,
        IInputTrigger<TInput> b)
        => new AllTrigger<TInput>(a, b);

    /// <summary>
    /// OR トリガーを作成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<TInput> Or<TInput>(
        this IInputTrigger<TInput> a,
        IInputTrigger<TInput> b)
        => new AnyTrigger<TInput>(a, b);
}
