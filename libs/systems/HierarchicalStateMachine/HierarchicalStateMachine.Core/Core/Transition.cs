using System;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 状態間の遷移を表すクラス。
/// 静的コストと動的コスト、条件判定をサポート。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class Transition<TContext>
{
    /// <summary>
    /// 遷移元の状態ID。
    /// </summary>
    public StateId From { get; }

    /// <summary>
    /// 遷移先の状態ID。
    /// </summary>
    public StateId To { get; }

    /// <summary>
    /// 静的コスト（固定値）。
    /// </summary>
    public float BaseCost { get; }

    private readonly Func<TContext, bool>? _condition;
    private readonly Func<TContext, float>? _dynamicCost;

    /// <summary>
    /// 静的コストのみの遷移を作成。
    /// </summary>
    public Transition(StateId from, StateId to, float cost = 1f)
    {
        From = from;
        To = to;
        BaseCost = cost;
        _condition = null;
        _dynamicCost = null;
    }

    /// <summary>
    /// 条件付き遷移を作成。
    /// </summary>
    public Transition(StateId from, StateId to, float cost, Func<TContext, bool> condition)
    {
        From = from;
        To = to;
        BaseCost = cost;
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _dynamicCost = null;
    }

    /// <summary>
    /// 動的コスト付き遷移を作成。
    /// </summary>
    public Transition(StateId from, StateId to, Func<TContext, float> dynamicCost)
    {
        From = from;
        To = to;
        BaseCost = 0f;
        _condition = null;
        _dynamicCost = dynamicCost ?? throw new ArgumentNullException(nameof(dynamicCost));
    }

    /// <summary>
    /// 条件と動的コスト付き遷移を作成。
    /// </summary>
    public Transition(StateId from, StateId to, Func<TContext, bool> condition, Func<TContext, float> dynamicCost)
    {
        From = from;
        To = to;
        BaseCost = 0f;
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _dynamicCost = dynamicCost ?? throw new ArgumentNullException(nameof(dynamicCost));
    }

    /// <summary>
    /// 遷移条件を評価。
    /// </summary>
    public bool CanTransition(TContext context)
    {
        return _condition?.Invoke(context) ?? true;
    }

    /// <summary>
    /// 遷移コストを計算。
    /// 動的コストが設定されている場合はそれを使用、なければ静的コストを返す。
    /// </summary>
    public float GetCost(TContext context)
    {
        return _dynamicCost?.Invoke(context) ?? BaseCost;
    }

    /// <summary>
    /// 動的コストが設定されているか。
    /// </summary>
    public bool HasDynamicCost => _dynamicCost != null;

    /// <summary>
    /// 条件が設定されているか。
    /// </summary>
    public bool HasCondition => _condition != null;
}
