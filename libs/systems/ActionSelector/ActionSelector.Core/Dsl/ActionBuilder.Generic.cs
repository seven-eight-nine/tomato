using System;
using System.Collections.Generic;

namespace Tomato.ActionSelector;

/// <summary>
/// 汎用ジャッジメント定義ビルダー。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
/// <typeparam name="TInput">入力状態の型</typeparam>
/// <typeparam name="TContext">ゲーム固有コンテキストの型</typeparam>
public class JudgmentBuilder<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    private JudgmentBuilderEntry<TCategory, TInput, TContext>? _currentEntry;

    /// <summary>
    /// ジャッジメント定義を開始する。
    /// </summary>
    public static JudgmentBuilder<TCategory, TInput, TContext> Begin() => new();

    /// <summary>
    /// 新しいジャッジメントを追加する（固定優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> AddJudgment(TCategory category, ActionPriority priority)
    {
        FlushCurrent();
        _currentEntry = new JudgmentBuilderEntry<TCategory, TInput, TContext>(this, category, priority);
        return _currentEntry;
    }

    /// <summary>
    /// 新しいジャッジメントを追加する（動的優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> AddJudgment(
        TCategory category,
        Func<FrameState<TInput, TContext>, ActionPriority> priorityFunc)
    {
        FlushCurrent();
        _currentEntry = new JudgmentBuilderEntry<TCategory, TInput, TContext>(this, category, priorityFunc);
        return _currentEntry;
    }

    /// <summary>
    /// ジャッジメント定義を完了する。
    /// </summary>
    public void Done()
    {
        FlushCurrent();
    }

    private void FlushCurrent()
    {
        if (_currentEntry != null)
        {
            _currentEntry.Flush();
            _currentEntry = null;
        }
    }
}

/// <summary>
/// 汎用ジャッジメント設定エントリ。
/// </summary>
public sealed class JudgmentBuilderEntry<TCategory, TInput, TContext>
    where TCategory : struct, Enum
{
    private readonly JudgmentBuilder<TCategory, TInput, TContext> _builder;
    private readonly TCategory _category;
    private readonly ActionPriority _priority;
    private readonly Func<FrameState<TInput, TContext>, ActionPriority>? _dynamicPriority;

    private string? _label;
    private IInputTrigger<TInput>? _input;
    private List<ICondition<TContext>>? _conditions;
    private string[]? _tags;
    private IActionResolver<TInput, TContext>? _resolver;
    private bool _flushed;
    private IActionJudgment<TCategory, TInput, TContext>? _createdJudgment;

    internal JudgmentBuilderEntry(
        JudgmentBuilder<TCategory, TInput, TContext> builder,
        TCategory category,
        ActionPriority priority)
    {
        _builder = builder;
        _category = category;
        _priority = priority;
        _dynamicPriority = null;
    }

    internal JudgmentBuilderEntry(
        JudgmentBuilder<TCategory, TInput, TContext> builder,
        TCategory category,
        Func<FrameState<TInput, TContext>, ActionPriority> priorityFunc)
    {
        _builder = builder;
        _category = category;
        _priority = ActionPriority.Normal;
        _dynamicPriority = priorityFunc ?? throw new ArgumentNullException(nameof(priorityFunc));
    }

    /// <summary>
    /// 入力条件を設定。
    /// 複数回呼ぶと上書きされる（最後の設定が有効）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> Input(IInputTrigger<TInput> input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        return this;
    }

    /// <summary>
    /// 状態条件を追加。
    /// 複数回呼ぶとすべてがANDで結合される。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> Condition(ICondition<TContext> condition)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        _conditions ??= new List<ICondition<TContext>>();
        _conditions.Add(condition);
        return this;
    }

    /// <summary>
    /// ラベルを設定。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> Label(string label)
    {
        _label = label;
        return this;
    }

    /// <summary>
    /// ラベルと説明を設定。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> Label(string label, string description)
    {
        _label = label;
        return this;
    }

    /// <summary>
    /// タグを設定。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> Tags(params string[] tags)
    {
        _tags = tags;
        return this;
    }

    /// <summary>
    /// リゾルバを設定（インターフェース版）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> Resolver(IActionResolver<TInput, TContext> resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    /// <summary>
    /// リゾルバを設定（デリゲート版）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> Resolver(
        Func<FrameState<TInput, TContext>, ResolvedAction> resolverFunc)
    {
        _resolver = new DelegateResolver<TInput, TContext>(
            resolverFunc ?? throw new ArgumentNullException(nameof(resolverFunc)));
        return this;
    }

    /// <summary>
    /// ジャッジメントを変数に取り出す。
    /// </summary>
    public JudgmentBuilder<TCategory, TInput, TContext> Out(
        out IActionJudgment<TCategory, TInput, TContext> judgment)
    {
        Flush();
        judgment = _createdJudgment!;
        return _builder;
    }

    /// <summary>
    /// 次のジャッジメントを追加する（固定優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> AddJudgment(TCategory category, ActionPriority priority)
    {
        Flush();
        return _builder.AddJudgment(category, priority);
    }

    /// <summary>
    /// 次のジャッジメントを追加する（動的優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory, TInput, TContext> AddJudgment(
        TCategory category,
        Func<FrameState<TInput, TContext>, ActionPriority> priorityFunc)
    {
        Flush();
        return _builder.AddJudgment(category, priorityFunc);
    }

    /// <summary>
    /// ジャッジメント定義を完了する。
    /// </summary>
    public void Done()
    {
        Flush();
        _builder.Done();
    }

    internal void Flush()
    {
        if (_flushed) return;
        _flushed = true;

        var label = _label ?? $"Judgment_{Guid.NewGuid():N}";

        // 条件を結合（0個→null、1個→そのまま、複数→AllConditionでAND結合）
        ICondition<TContext>? finalCondition = _conditions switch
        {
            null => null,
            { Count: 0 } => null,
            { Count: 1 } => _conditions[0],
            _ => new AllCondition<TContext>(_conditions.ToArray())
        };

        if (_dynamicPriority != null)
        {
            _createdJudgment = new SimpleJudgment<TCategory, TInput, TContext>(
                label,
                _category,
                _input,
                finalCondition,
                _dynamicPriority,
                _tags,
                _resolver);
        }
        else
        {
            _createdJudgment = new SimpleJudgment<TCategory, TInput, TContext>(
                label,
                _category,
                _input,
                finalCondition,
                _priority,
                _tags,
                _resolver);
        }
    }
}

/// <summary>
/// InputState/GameState 用の JudgmentBuilder 便利クラス。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class JudgmentBuilder<TCategory> where TCategory : struct, Enum
{
    private readonly JudgmentBuilder<TCategory, InputState, GameState> _inner = new();

    /// <summary>
    /// ジャッジメント定義を開始する。
    /// </summary>
    public static JudgmentBuilder<TCategory> Begin() => new();

    /// <summary>
    /// 新しいジャッジメントを追加する（固定優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> AddJudgment(TCategory category, ActionPriority priority)
    {
        var innerEntry = _inner.AddJudgment(category, priority);
        return new JudgmentBuilderEntry<TCategory>(this, innerEntry);
    }

    /// <summary>
    /// 新しいジャッジメントを追加する（動的優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> AddJudgment(
        TCategory category,
        Func<FrameState<InputState, GameState>, ActionPriority> priorityFunc)
    {
        var innerEntry = _inner.AddJudgment(category, priorityFunc);
        return new JudgmentBuilderEntry<TCategory>(this, innerEntry);
    }

    /// <summary>
    /// ジャッジメント定義を完了する。
    /// </summary>
    public void Done()
    {
        _inner.Done();
    }
}

/// <summary>
/// InputState/GameState 用の JudgmentEntry 便利クラス。
/// </summary>
/// <typeparam name="TCategory">カテゴリのenum型</typeparam>
public sealed class JudgmentBuilderEntry<TCategory> where TCategory : struct, Enum
{
    private readonly JudgmentBuilder<TCategory> _builder;
    private readonly JudgmentBuilderEntry<TCategory, InputState, GameState> _inner;

    internal JudgmentBuilderEntry(
        JudgmentBuilder<TCategory> builder,
        JudgmentBuilderEntry<TCategory, InputState, GameState> inner)
    {
        _builder = builder;
        _inner = inner;
    }

    /// <summary>
    /// 入力条件を設定。
    /// 複数回呼ぶと上書きされる（最後の設定が有効）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> Input(IInputTrigger<InputState> input)
    {
        _inner.Input(input);
        return this;
    }

    /// <summary>
    /// 状態条件を追加。
    /// 複数回呼ぶとすべてがANDで結合される。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> Condition(ICondition<GameState> condition)
    {
        _inner.Condition(condition);
        return this;
    }

    /// <summary>
    /// ラベルを設定。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> Label(string label)
    {
        _inner.Label(label);
        return this;
    }

    /// <summary>
    /// ラベルと説明を設定。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> Label(string label, string description)
    {
        _inner.Label(label, description);
        return this;
    }

    /// <summary>
    /// タグを設定。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> Tags(params string[] tags)
    {
        _inner.Tags(tags);
        return this;
    }

    /// <summary>
    /// リゾルバを設定（インターフェース版）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> Resolver(IActionResolver<InputState, GameState> resolver)
    {
        _inner.Resolver(resolver);
        return this;
    }

    /// <summary>
    /// リゾルバを設定（デリゲート版）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> Resolver(
        Func<FrameState<InputState, GameState>, ResolvedAction> resolverFunc)
    {
        _inner.Resolver(resolverFunc);
        return this;
    }

    /// <summary>
    /// ジャッジメントを変数に取り出す。
    /// </summary>
    public JudgmentBuilder<TCategory> Out(
        out IActionJudgment<TCategory, InputState, GameState> judgment)
    {
        _inner.Out(out judgment);
        return _builder;
    }

    /// <summary>
    /// 次のジャッジメントを追加する（固定優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> AddJudgment(TCategory category, ActionPriority priority)
    {
        _inner.Flush();
        return _builder.AddJudgment(category, priority);
    }

    /// <summary>
    /// 次のジャッジメントを追加する（動的優先度）。
    /// </summary>
    public JudgmentBuilderEntry<TCategory> AddJudgment(
        TCategory category,
        Func<FrameState<InputState, GameState>, ActionPriority> priorityFunc)
    {
        _inner.Flush();
        return _builder.AddJudgment(category, priorityFunc);
    }

    /// <summary>
    /// ジャッジメント定義を完了する。
    /// </summary>
    public void Done()
    {
        _builder.Done();
    }

    internal void Flush() => _inner.Flush();
}
