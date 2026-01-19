using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// シンプルなジャッジメント実装（GameState/InputState用）。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class SimpleJudgment<TCategory> : IControllableJudgment<TCategory, InputState, GameState>
    where TCategory : struct, Enum
{
    private readonly string _label;
    private readonly TCategory _category;
    private readonly IInputTrigger<InputState>? _input;
    private readonly ICondition<GameState>? _condition;
    private readonly ActionPriority _priority;
    private readonly Func<FrameState<InputState, GameState>, ActionPriority>? _dynamicPriority;
    private readonly string[] _tags;
    private readonly IActionResolver<InputState, GameState>? _resolver;
    private bool _isForcedInput;

    /// <summary>
    /// 固定優先度のジャッジメントを生成する。
    /// </summary>
    public SimpleJudgment(
        string label,
        TCategory category,
        IInputTrigger<InputState>? input,
        ICondition<GameState>? condition,
        ActionPriority priority,
        string[]? tags = null,
        IActionResolver<InputState, GameState>? resolver = null)
    {
        _label = label ?? throw new ArgumentNullException(nameof(label));
        _category = category;
        _input = input;
        _condition = condition;
        _priority = priority;
        _dynamicPriority = null;
        _tags = tags ?? Array.Empty<string>();
        _resolver = resolver;
    }

    /// <summary>
    /// 動的優先度のジャッジメントを生成する。
    /// </summary>
    public SimpleJudgment(
        string label,
        TCategory category,
        IInputTrigger<InputState>? input,
        ICondition<GameState>? condition,
        Func<FrameState<InputState, GameState>, ActionPriority> dynamicPriority,
        string[]? tags = null,
        IActionResolver<InputState, GameState>? resolver = null)
    {
        _label = label ?? throw new ArgumentNullException(nameof(label));
        _category = category;
        _input = input;
        _condition = condition;
        _priority = ActionPriority.Normal;
        _dynamicPriority = dynamicPriority ?? throw new ArgumentNullException(nameof(dynamicPriority));
        _tags = tags ?? Array.Empty<string>();
        _resolver = resolver;
    }

    /// <summary>
    /// 最小限のジャッジメントを生成する（常に入力成立）。
    /// </summary>
    public SimpleJudgment(
        string label,
        TCategory category,
        ActionPriority priority)
        : this(label, category, Triggers.Always, null, priority, null)
    {
    }

    /// <summary>
    /// 入力のみ指定するシンプルなジャッジメントを生成する。
    /// </summary>
    public SimpleJudgment(
        string label,
        TCategory category,
        IInputTrigger<InputState> input,
        ActionPriority priority)
        : this(label, category, input, null, priority, null)
    {
    }

    /// <summary>
    /// 条件のみ指定するシンプルなジャッジメントを生成する（AI用）。
    /// </summary>
    public SimpleJudgment(
        string label,
        TCategory category,
        ICondition<GameState> condition,
        ActionPriority priority)
        : this(label, category, Triggers.Always, condition, priority, null)
    {
    }

    // IActionJudgment implementation
    public string Label => _label;
    public TCategory Category => _category;
    public IInputTrigger<InputState>? Input => _input;
    public ICondition<GameState>? Condition => _condition;
    public IActionResolver<InputState, GameState>? Resolver => _resolver;
    public ReadOnlySpan<string> Tags => _tags;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionPriority GetPriority(in FrameState<InputState, GameState> state)
        => _dynamicPriority != null ? _dynamicPriority(state) : _priority;

    // IControllableJudgment implementation
    public bool IsForcedInput => _isForcedInput;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForceInput() => _isForcedInput = true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearForceInput() => _isForcedInput = false;

    public void ResetInput()
    {
        _input?.OnJudgmentStop();
        _input?.OnJudgmentStart();
    }

    public override string ToString() => $"Judgment[{_label}]";
}

/// <summary>
/// 動的優先度を持つジャッジメント（GameState/InputState用）。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class DynamicPriorityJudgment<TCategory> : IControllableJudgment<TCategory, InputState, GameState>
    where TCategory : struct, Enum
{
    private readonly string _label;
    private readonly TCategory _category;
    private readonly IInputTrigger<InputState>? _input;
    private readonly ICondition<GameState>? _condition;
    private readonly Func<FrameState<InputState, GameState>, ActionPriority> _priorityFunc;
    private readonly string[] _tags;
    private readonly IActionResolver<InputState, GameState>? _resolver;
    private bool _isForcedInput;

    public DynamicPriorityJudgment(
        string label,
        TCategory category,
        IInputTrigger<InputState>? input,
        ICondition<GameState>? condition,
        Func<FrameState<InputState, GameState>, ActionPriority> priorityFunc,
        string[]? tags = null,
        IActionResolver<InputState, GameState>? resolver = null)
    {
        _label = label ?? throw new ArgumentNullException(nameof(label));
        _category = category;
        _input = input;
        _condition = condition;
        _priorityFunc = priorityFunc ?? throw new ArgumentNullException(nameof(priorityFunc));
        _tags = tags ?? Array.Empty<string>();
        _resolver = resolver;
    }

    public string Label => _label;
    public TCategory Category => _category;
    public IInputTrigger<InputState>? Input => _input;
    public ICondition<GameState>? Condition => _condition;
    public IActionResolver<InputState, GameState>? Resolver => _resolver;
    public ReadOnlySpan<string> Tags => _tags;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ActionPriority GetPriority(in FrameState<InputState, GameState> state) => _priorityFunc(state);

    public bool IsForcedInput => _isForcedInput;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForceInput() => _isForcedInput = true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearForceInput() => _isForcedInput = false;

    public void ResetInput()
    {
        _input?.OnJudgmentStop();
        _input?.OnJudgmentStart();
    }

    public override string ToString() => $"DynamicJudgment[{_label}]";
}

/// <summary>
/// ジャッジメントの一括操作用拡張メソッド。
/// </summary>
public static class JudgmentExtensions
{
    /// <summary>
    /// 指定タグを持つすべてのジャッジメントの入力をリセットする。
    /// </summary>
    public static void ResetInputsByTag<TCategory, TInput, TContext>(
        this ReadOnlySpan<IActionJudgment<TCategory, TInput, TContext>> judgments,
        string tag)
        where TCategory : struct, Enum
    {
        foreach (var judgment in judgments)
        {
            if (judgment is IControllableJudgment<TCategory, TInput, TContext> cj && HasTag(judgment, tag))
            {
                cj.ResetInput();
            }
        }
    }

    /// <summary>
    /// 指定タグを持つすべてのジャッジメントの入力をリセットする。
    /// </summary>
    public static void ResetInputsByTag<TCategory, TInput, TContext>(
        this IActionJudgment<TCategory, TInput, TContext>[] judgments,
        string tag)
        where TCategory : struct, Enum
    {
        for (int i = 0; i < judgments.Length; i++)
        {
            var judgment = judgments[i];
            if (judgment is IControllableJudgment<TCategory, TInput, TContext> cj && HasTag(judgment, tag))
            {
                cj.ResetInput();
            }
        }
    }

    /// <summary>
    /// 指定タグを持つすべてのジャッジメントの強制入力を解除する。
    /// </summary>
    public static void ClearForceInputsByTag<TCategory, TInput, TContext>(
        this IActionJudgment<TCategory, TInput, TContext>[] judgments,
        string tag)
        where TCategory : struct, Enum
    {
        for (int i = 0; i < judgments.Length; i++)
        {
            var judgment = judgments[i];
            if (judgment is IControllableJudgment<TCategory, TInput, TContext> cj && HasTag(judgment, tag))
            {
                cj.ClearForceInput();
            }
        }
    }

    /// <summary>
    /// すべてのジャッジメントの入力をリセットする。
    /// </summary>
    public static void ResetAllInputs<TCategory, TInput, TContext>(
        this IActionJudgment<TCategory, TInput, TContext>[] judgments)
        where TCategory : struct, Enum
    {
        for (int i = 0; i < judgments.Length; i++)
        {
            if (judgments[i] is IControllableJudgment<TCategory, TInput, TContext> cj)
            {
                cj.ResetInput();
            }
        }
    }

    /// <summary>
    /// すべてのジャッジメントの強制入力を解除する。
    /// </summary>
    public static void ClearAllForceInputs<TCategory, TInput, TContext>(
        this IActionJudgment<TCategory, TInput, TContext>[] judgments)
        where TCategory : struct, Enum
    {
        for (int i = 0; i < judgments.Length; i++)
        {
            if (judgments[i] is IControllableJudgment<TCategory, TInput, TContext> cj)
            {
                cj.ClearForceInput();
            }
        }
    }

    /// <summary>
    /// 指定ジャッジメント以外のすべての入力をリセットする。
    /// </summary>
    public static void ResetInputsExcept<TCategory, TInput, TContext>(
        this IActionJudgment<TCategory, TInput, TContext>[] judgments,
        IActionJudgment<TCategory, TInput, TContext> except)
        where TCategory : struct, Enum
    {
        for (int i = 0; i < judgments.Length; i++)
        {
            var judgment = judgments[i];
            if (judgment != except && judgment is IControllableJudgment<TCategory, TInput, TContext> cj)
            {
                cj.ResetInput();
            }
        }
    }

    private static bool HasTag<TCategory, TInput, TContext>(
        IActionJudgment<TCategory, TInput, TContext> judgment, string tag)
        where TCategory : struct, Enum
    {
        var tags = judgment.Tags;
        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i] == tag)
                return true;
        }
        return false;
    }
}
