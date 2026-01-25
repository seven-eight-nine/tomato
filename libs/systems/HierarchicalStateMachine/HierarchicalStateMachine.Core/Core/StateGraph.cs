using System;
using System.Collections.Generic;

namespace Tomato.HierarchicalStateMachine;

/// <summary>
/// 状態と遷移を管理するグラフ構造。
/// Any State からの遷移をサポート。
/// </summary>
/// <typeparam name="TContext">コンテキストの型</typeparam>
public class StateGraph<TContext>
{
    private readonly Dictionary<StateId, IState<TContext>> _states = new();
    private readonly Dictionary<StateId, List<Transition<TContext>>> _transitions = new();
    private readonly List<Transition<TContext>> _anyStateTransitions = new();

    /// <summary>
    /// 登録されている全状態。
    /// </summary>
    public IReadOnlyDictionary<StateId, IState<TContext>> States => _states;

    /// <summary>
    /// 状態ごとの遷移リスト。
    /// </summary>
    public IReadOnlyDictionary<StateId, List<Transition<TContext>>> Transitions => _transitions;

    /// <summary>
    /// Any State からの遷移リスト。
    /// </summary>
    public IReadOnlyList<Transition<TContext>> AnyStateTransitions => _anyStateTransitions;

    /// <summary>
    /// 状態を追加。
    /// </summary>
    public StateGraph<TContext> AddState(IState<TContext> state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (_states.ContainsKey(state.Id))
            throw new InvalidOperationException($"State '{state.Id}' already exists.");

        _states[state.Id] = state;
        _transitions[state.Id] = new List<Transition<TContext>>();
        return this;
    }

    /// <summary>
    /// 遷移を追加。
    /// </summary>
    public StateGraph<TContext> AddTransition(Transition<TContext> transition)
    {
        if (transition == null)
            throw new ArgumentNullException(nameof(transition));

        if (transition.From.IsAny)
        {
            // Any State からの遷移
            _anyStateTransitions.Add(transition);
        }
        else
        {
            if (!_states.ContainsKey(transition.From))
                throw new InvalidOperationException($"Source state '{transition.From}' not found.");

            if (!_states.ContainsKey(transition.To))
                throw new InvalidOperationException($"Target state '{transition.To}' not found.");

            _transitions[transition.From].Add(transition);
        }
        return this;
    }

    /// <summary>
    /// 指定状態から利用可能な全遷移を取得。
    /// Any State からの遷移も含む。
    /// </summary>
    public IEnumerable<Transition<TContext>> GetTransitionsFrom(StateId stateId)
    {
        if (_transitions.TryGetValue(stateId, out var transitions))
        {
            foreach (var t in transitions)
                yield return t;
        }

        // Any State からの遷移も追加
        foreach (var t in _anyStateTransitions)
        {
            // 自分自身への遷移は除外
            if (t.To != stateId)
                yield return t;
        }
    }

    /// <summary>
    /// 指定IDの状態を取得。
    /// </summary>
    public IState<TContext>? GetState(StateId id)
    {
        return _states.TryGetValue(id, out var state) ? state : null;
    }

    /// <summary>
    /// 指定IDの状態が存在するか。
    /// </summary>
    public bool HasState(StateId id) => _states.ContainsKey(id);

    /// <summary>
    /// 状態数。
    /// </summary>
    public int StateCount => _states.Count;
}
