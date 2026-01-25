using System;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// デリゲートベースのヒューリスティック。
/// ラムダ式でヒューリスティック関数を定義できる。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class DelegateHeuristic<TContext> : IHeuristic<TContext>
{
    private readonly Func<StateId, StateId, TContext, float> _estimator;

    public DelegateHeuristic(Func<StateId, StateId, TContext, float> estimator)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
    }

    public float Estimate(StateId current, StateId goal, TContext context)
    {
        return _estimator(current, goal, context);
    }
}
