using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// キャッシュ機能を持つ条件。
/// 同一フレーム内で複数のジャッジメントが同じ条件を参照する場合、
/// 評価結果をキャッシュすることで重複評価を回避する。
/// </summary>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
public interface ICachedCondition<TContext> : ICondition<TContext>
{
    /// <summary>
    /// キャッシュが無効（再評価が必要）かどうか。
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// キャッシュを無効化する。次の Evaluate() で再評価される。
    /// </summary>
    void Invalidate();
}

/// <summary>
/// 組み込み条件のファクトリ（GameState用）。
/// </summary>
public static class Conditions
{
    /// <summary>
    /// 常に成立する条件。
    /// </summary>
    public static ICondition<GameState> Always => AlwaysCondition<GameState>.Instance;

    /// <summary>
    /// 決して成立しない条件。
    /// </summary>
    public static ICondition<GameState> Never => NeverCondition<GameState>.Instance;

    /// <summary>
    /// 指定フラグが設定されていることを条件とする。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<GameState> HasFlag(uint flag)
        => new FlagCondition(flag);

    /// <summary>
    /// いずれかのフラグが設定されていることを条件とする。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<GameState> HasAnyFlag(uint flags)
        => new AnyFlagCondition(flags);

    /// <summary>
    /// 条件を反転する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<GameState> Not(ICondition<GameState> condition)
        => new NotCondition<GameState>(condition);

    /// <summary>
    /// すべての条件が成立することを要求する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<GameState> All(params ICondition<GameState>[] conditions)
        => new AllCondition<GameState>(conditions);

    /// <summary>
    /// いずれかの条件が成立することを要求する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<GameState> Any(params ICondition<GameState>[] conditions)
        => new AnyCondition<GameState>(conditions);

    /// <summary>
    /// デリゲートから条件を生成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICondition<GameState> From(Func<GameState, bool> predicate)
        => new DelegateCondition<GameState>(predicate);

    /// <summary>
    /// キャッシュ機能付きのデリゲート条件を生成する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ICachedCondition<GameState> Cached(Func<GameState, bool> predicate)
        => new CachedCondition<GameState>(predicate);
}

/// <summary>
/// 指定フラグが設定されていることを条件とする。
/// </summary>
internal sealed class FlagCondition : ICondition<GameState>
{
    private readonly uint _flag;

    public FlagCondition(uint flag) => _flag = flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in GameState context) => context.HasFlag(_flag);
}

/// <summary>
/// いずれかのフラグが設定されていることを条件とする。
/// </summary>
internal sealed class AnyFlagCondition : ICondition<GameState>
{
    private readonly uint _flags;

    public AnyFlagCondition(uint flags) => _flags = flags;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in GameState context) => context.HasAnyFlag(_flags);
}

/// <summary>
/// キャッシュ機能付き条件。
/// </summary>
internal sealed class CachedCondition<TContext> : ICachedCondition<TContext>
{
    private readonly Func<TContext, bool> _evaluator;
    private bool _isDirty = true;
    private bool _cachedResult;

    public CachedCondition(Func<TContext, bool> evaluator)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public bool IsDirty => _isDirty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Evaluate(in TContext context)
    {
        if (_isDirty)
        {
            _cachedResult = _evaluator(context);
            _isDirty = false;
        }
        return _cachedResult;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invalidate() => _isDirty = true;
}
