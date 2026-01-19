using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// 汎用アクション選択エンジン。
///
/// 継承して使用することで、内部クラス（Judgment, Builder, List等）に
/// 短い名前でアクセスできる。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
/// <example>
/// <code>
/// // ゲーム固有のセレクタを定義（空のボディでOK）
/// public class FighterActionSelector : ActionSelector&lt;ActionCategory, InputState, GameState&gt; { }
///
/// // 使用時
/// FighterActionSelector.Judgment attack;
/// FighterActionSelector.Builder.Create()
///     .AddJudgment(ActionCategory.FullBody, Normal)
///     .Label("Attack")
///     .Out(out attack);
/// </code>
/// </example>
public partial class ActionSelector<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    public bool RecordEvaluations { get; set; } = true;
    public bool ValidateInput { get; set; } = true;

    private readonly CategoryRules<TCategory> _rules;
    private readonly TCategory[] _categoryValues;
    private readonly Dictionary<IActionJudgment<TCategory, TInput, TContext>, bool> _activeJudgments;
    private readonly IActionJudgment<TCategory, TInput, TContext>?[] _requestedActions;
    private readonly (IActionJudgment<TCategory, TInput, TContext> judgment, ActionPriority priority)[] _candidates;
    private readonly bool[] _categoryFilled;
    private int _candidateCount;
    private readonly SelectionResult<TCategory, TInput, TContext> _result;

    /// <summary>
    /// デフォルトコンストラクタ。NoExclusivityルールを使用する。
    /// </summary>
    public ActionSelector() : this(null, 256) { }

    public ActionSelector(CategoryRules<TCategory>? rules, int maxJudgments = 256)
    {
        _rules = rules ?? NoExclusivityRules<TCategory>.Instance;
        _categoryValues = (TCategory[])Enum.GetValues(typeof(TCategory));
        _activeJudgments = new Dictionary<IActionJudgment<TCategory, TInput, TContext>, bool>(maxJudgments);
        _requestedActions = new IActionJudgment<TCategory, TInput, TContext>?[_categoryValues.Length];
        _candidates = new (IActionJudgment<TCategory, TInput, TContext>, ActionPriority)[maxJudgments];
        _categoryFilled = new bool[_categoryValues.Length];
        _result = new SelectionResult<TCategory, TInput, TContext>();
    }

    public SelectionResult<TCategory, TInput, TContext> ProcessFrame(
        JudgmentList<TCategory, TInput, TContext> judgmentList,
        in FrameState<TInput, TContext> state,
        ProcessFrameOptions options = ProcessFrameOptions.None)
    {
        if (judgmentList == null)
            throw new ArgumentNullException(nameof(judgmentList));

        _result.Clear();
        ResetBuffers();

        var forcedInputOnly = (options & ProcessFrameOptions.ForcedInputOnly) != 0;

        if (!forcedInputOnly)
        {
            ManageLifecycle(judgmentList, in state);
        }

        CollectCandidates(judgmentList, in state);
        SortCandidates();
        DetermineRequestedActions(in state, forcedInputOnly);
        _result.SetRequestedActions(_requestedActions);

        return _result;
    }

    private void ResetBuffers()
    {
        _candidateCount = 0;
        Array.Clear(_requestedActions, 0, _requestedActions.Length);
        Array.Clear(_categoryFilled, 0, _categoryFilled.Length);
    }

    private void ManageLifecycle(
        JudgmentList<TCategory, TInput, TContext> judgmentList,
        in FrameState<TInput, TContext> state)
    {
        var newJudgments = new HashSet<IActionJudgment<TCategory, TInput, TContext>>();
        for (int i = 0; i < judgmentList.Count; i++)
        {
            newJudgments.Add(judgmentList[i].Judgment);
        }

        var toRemove = new List<IActionJudgment<TCategory, TInput, TContext>>();
        foreach (var kvp in _activeJudgments)
        {
            if (!newJudgments.Contains(kvp.Key))
            {
                kvp.Key.Input?.OnJudgmentStop();
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var j in toRemove)
        {
            _activeJudgments.Remove(j);
        }

        for (int i = 0; i < judgmentList.Count; i++)
        {
            var j = judgmentList[i].Judgment;
            if (!_activeJudgments.ContainsKey(j))
            {
                j.Input?.OnJudgmentStart();
                _activeJudgments[j] = true;
            }
            j.Input?.OnJudgmentUpdate(in state.Input, state.DeltaTime);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CollectCandidates(
        JudgmentList<TCategory, TInput, TContext> judgmentList,
        in FrameState<TInput, TContext> state)
    {
        for (int i = 0; i < judgmentList.Count; i++)
        {
            var entry = judgmentList[i];
            var judgment = entry.Judgment;
            var priority = entry.GetEffectivePriority(in state);

            if (!priority.IsDisabled)
            {
                _candidates[_candidateCount++] = (judgment, priority);
            }
            else if (RecordEvaluations)
            {
                _result.AddEvaluation(new JudgmentEvaluation<TCategory>(
                    judgment.Label,
                    judgment.Category,
                    priority,
                    EvaluationOutcome.Disabled));
            }
        }
    }

    private void SortCandidates()
    {
        if (_candidateCount <= 1) return;

        Array.Sort(_candidates, 0, _candidateCount,
            Comparer<(IActionJudgment<TCategory, TInput, TContext> judgment, ActionPriority priority)>.Create(
                (a, b) => a.priority.CompareTo(b.priority)));
    }

    private void DetermineRequestedActions(in FrameState<TInput, TContext> state, bool forcedInputOnly)
    {
        for (int i = 0; i < _candidateCount; i++)
        {
            var (judgment, priority) = _candidates[i];
            var category = judgment.Category;
            var categoryIndex = Convert.ToInt32(category);

            if (HasExclusiveConflict(category))
            {
                if (RecordEvaluations)
                {
                    _result.AddEvaluation(new JudgmentEvaluation<TCategory>(
                        judgment.Label, category, priority, EvaluationOutcome.ExclusivityConflict));
                }
                continue;
            }

            if (_categoryFilled[categoryIndex])
            {
                if (RecordEvaluations)
                {
                    _result.AddEvaluation(new JudgmentEvaluation<TCategory>(
                        judgment.Label, category, priority, EvaluationOutcome.CategoryOccupied));
                }
                continue;
            }

            var isForcedInput = judgment is IControllableJudgment<TCategory, TInput, TContext> cj && cj.IsForcedInput;
            bool inputFired;
            if (forcedInputOnly)
            {
                inputFired = isForcedInput;
            }
            else
            {
                inputFired = isForcedInput || (judgment.Input != null && judgment.Input.IsTriggered(in state.Input));
            }

            if (!inputFired)
            {
                if (RecordEvaluations)
                {
                    _result.AddEvaluation(new JudgmentEvaluation<TCategory>(
                        judgment.Label, category, priority, EvaluationOutcome.InputNotFired));
                }
                continue;
            }

            if (judgment.Condition != null && !judgment.Condition.Evaluate(in state.Context))
            {
                if (RecordEvaluations)
                {
                    _result.AddEvaluation(new JudgmentEvaluation<TCategory>(
                        judgment.Label, category, priority, EvaluationOutcome.ConditionFailed));
                }
                continue;
            }

            if (judgment.Resolver != null)
            {
                var resolved = judgment.Resolver.Resolve(in state);
                if (resolved.IsNone)
                {
                    if (RecordEvaluations)
                    {
                        _result.AddEvaluation(new JudgmentEvaluation<TCategory>(
                            judgment.Label, category, priority, EvaluationOutcome.ResolverRejected));
                    }
                    continue;
                }
            }

            _requestedActions[categoryIndex] = judgment;
            _categoryFilled[categoryIndex] = true;
            MarkExclusiveCategories(category);

            if (RecordEvaluations)
            {
                _result.AddEvaluation(new JudgmentEvaluation<TCategory>(
                    judgment.Label, category, priority, EvaluationOutcome.Selected));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasExclusiveConflict(TCategory category)
    {
        for (int i = 0; i < _categoryValues.Length; i++)
        {
            if (_requestedActions[i] != null)
            {
                var requestedCategory = _categoryValues[i];
                if (_rules.AreExclusive(category, requestedCategory))
                {
                    return true;
                }
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MarkExclusiveCategories(TCategory category)
    {
        var categoryIndex = Convert.ToInt32(category);
        _categoryFilled[categoryIndex] = true;

        for (int i = 0; i < _categoryValues.Length; i++)
        {
            if (i == categoryIndex) continue;
            if (_rules.AreExclusive(category, _categoryValues[i]))
            {
                _categoryFilled[i] = true;
            }
        }
    }
}

/// <summary>
/// GameState/InputState用のActionSelector。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class ActionSelector<TCategory>
    where TCategory : struct, Enum
{
    private readonly ActionSelector<TCategory, InputState, GameState> _inner;

    public bool RecordEvaluations
    {
        get => _inner.RecordEvaluations;
        set => _inner.RecordEvaluations = value;
    }

    public bool ValidateInput
    {
        get => _inner.ValidateInput;
        set => _inner.ValidateInput = value;
    }

    public ActionSelector(CategoryRules<TCategory>? rules = null, int maxJudgments = 256)
    {
        _inner = new ActionSelector<TCategory, InputState, GameState>(rules, maxJudgments);
    }

    public SelectionResult<TCategory, InputState, GameState> ProcessFrame(
        JudgmentList<TCategory> judgmentList,
        in GameState state,
        ProcessFrameOptions options = ProcessFrameOptions.None)
    {
        var frameState = new FrameState<InputState, GameState>(
            state.Input,
            state,
            state.DeltaTime,
            state.TotalTime,
            state.FrameCount);
        return _inner.ProcessFrame(judgmentList.AsGeneric(), in frameState, options);
    }
}

/// <summary>
/// 汎用選択結果。
/// </summary>
public class SelectionResult<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    private IActionJudgment<TCategory, TInput, TContext>?[] _requestedActions
        = Array.Empty<IActionJudgment<TCategory, TInput, TContext>?>();
    private readonly List<JudgmentEvaluation<TCategory>> _evaluations = new();

    public IReadOnlyList<JudgmentEvaluation<TCategory>> Evaluations => _evaluations;

    public bool TryGetRequested(TCategory category, out IActionJudgment<TCategory, TInput, TContext> judgment)
    {
        var index = Convert.ToInt32(category);
        if (index >= 0 && index < _requestedActions.Length && _requestedActions[index] != null)
        {
            judgment = _requestedActions[index]!;
            return true;
        }
        judgment = default!;
        return false;
    }

    internal void Clear()
    {
        _evaluations.Clear();
    }

    internal void SetRequestedActions(IActionJudgment<TCategory, TInput, TContext>?[] actions)
    {
        _requestedActions = actions;
    }

    internal void AddEvaluation(JudgmentEvaluation<TCategory> evaluation)
    {
        _evaluations.Add(evaluation);
    }

    /// <summary>
    /// 選択されたすべてのアクションを取得する。
    /// </summary>
    public IEnumerable<IActionJudgment<TCategory, TInput, TContext>> GetAllRequestedActions()
    {
        for (int i = 0; i < _requestedActions.Length; i++)
        {
            if (_requestedActions[i] != null)
            {
                yield return _requestedActions[i]!;
            }
        }
    }
}
