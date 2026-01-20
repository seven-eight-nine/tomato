using System;

namespace Tomato.ActionSelector;

/// <summary>
/// 演算子をサポートする条件ラッパー。
///
/// <example>
/// <code>
/// using static Tomato.ActionSelector.Cond;
///
/// // AND演算子
/// var cond = Grounded.And(HealthAbove(0.5f));
///
/// // 拡張メソッド経由
/// var cond2 = Grounded.And(HealthAbove(0.5f)).Or(Airborne);
/// </code>
/// </example>
/// </summary>
public readonly struct ComposableCondition : ICondition<GameState>
{
    private readonly ICondition<GameState> _inner;

    public ComposableCondition(ICondition<GameState> condition)
    {
        _inner = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public bool Evaluate(in GameState state) => _inner.Evaluate(in state);

    /// <summary>
    /// AND合成（両方成立）。
    /// </summary>
    public ComposableCondition And(ICondition<GameState> other)
        => new ComposableCondition(Conditions.All(_inner, other));

    /// <summary>
    /// OR合成（いずれか成立）。
    /// </summary>
    public ComposableCondition Or(ICondition<GameState> other)
        => new ComposableCondition(Conditions.Any(_inner, other));

    /// <summary>
    /// NOT（反転）。
    /// </summary>
    public ComposableCondition Not()
        => new ComposableCondition(Conditions.Not(_inner));

    /// <summary>
    /// 内部のICondition<GameState>を取得。
    /// </summary>
    public ICondition<GameState> Inner => _inner;

    /// <summary>
    /// &amp; 演算子（AND）。
    /// </summary>
    public static ComposableCondition operator &(ComposableCondition left, ComposableCondition right)
        => left.And(right._inner);

    /// <summary>
    /// | 演算子（OR）。
    /// </summary>
    public static ComposableCondition operator |(ComposableCondition left, ComposableCondition right)
        => left.Or(right._inner);

    /// <summary>
    /// ! 演算子（NOT）。
    /// </summary>
    public static ComposableCondition operator !(ComposableCondition cond)
        => cond.Not();
}

/// <summary>
/// 条件の合成用拡張メソッド。
/// </summary>
public static class ConditionExtensions
{
    /// <summary>
    /// 演算子をサポートする形式に変換。
    /// </summary>
    public static ComposableCondition Compose(this ICondition<GameState> condition)
        => new ComposableCondition(condition);

    /// <summary>
    /// AND合成（両方成立）。
    /// </summary>
    public static ICondition<GameState> And(this ICondition<GameState> left, ICondition<GameState> right)
        => Conditions.All(left, right);

    /// <summary>
    /// OR合成（いずれか成立）。
    /// </summary>
    public static ICondition<GameState> Or(this ICondition<GameState> left, ICondition<GameState> right)
        => Conditions.Any(left, right);

    /// <summary>
    /// NOT（反転）。
    /// </summary>
    public static ICondition<GameState> Not(this ICondition<GameState> condition)
        => Conditions.Not(condition);

    /// <summary>
    /// 複数のAND条件を連結。
    /// </summary>
    public static ICondition<GameState> AndAll(this ICondition<GameState> first, params ICondition<GameState>[] others)
    {
        var all = new ICondition<GameState>[others.Length + 1];
        all[0] = first;
        Array.Copy(others, 0, all, 1, others.Length);
        return Conditions.All(all);
    }

    /// <summary>
    /// 複数のOR条件を連結。
    /// </summary>
    public static ICondition<GameState> OrAny(this ICondition<GameState> first, params ICondition<GameState>[] others)
    {
        var all = new ICondition<GameState>[others.Length + 1];
        all[0] = first;
        Array.Copy(others, 0, all, 1, others.Length);
        return Conditions.Any(all);
    }
}
