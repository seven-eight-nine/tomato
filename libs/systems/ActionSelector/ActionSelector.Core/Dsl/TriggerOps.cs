using System;

namespace Tomato.ActionSelector;

/// <summary>
/// 演算子をサポートするトリガーラッパー。
///
/// <example>
/// <code>
/// using static Tomato.ActionSelector.Trig;
///
/// // AND演算子
/// var trigger = Press(Attack).And(Hold(Guard));
///
/// // OR演算子
/// var trigger2 = Press(Punch).Or(Press(Kick));
/// </code>
/// </example>
/// </summary>
public readonly struct ComposableTrigger : IInputTrigger<InputState>
{
    private readonly IInputTrigger<InputState> _inner;

    public ComposableTrigger(IInputTrigger<InputState> trigger)
    {
        _inner = trigger ?? throw new ArgumentNullException(nameof(trigger));
    }

    public bool IsTriggered(in InputState input) => _inner.IsTriggered(in input);
    public void OnJudgmentStart() => _inner.OnJudgmentStart();
    public void OnJudgmentStop() => _inner.OnJudgmentStop();
    public void OnJudgmentUpdate(in InputState input, float deltaTime)
        => _inner.OnJudgmentUpdate(in input, deltaTime);

    /// <summary>
    /// AND合成（両方成立）。
    /// </summary>
    public ComposableTrigger And(IInputTrigger<InputState> other)
        => new ComposableTrigger(Triggers.All(_inner, other));

    /// <summary>
    /// OR合成（いずれか成立）。
    /// </summary>
    public ComposableTrigger Or(IInputTrigger<InputState> other)
        => new ComposableTrigger(Triggers.Any(_inner, other));

    /// <summary>
    /// 内部のIInputTrigger<InputState>を取得。
    /// </summary>
    public IInputTrigger<InputState> Inner => _inner;

    /// <summary>
    /// &amp; 演算子（AND）。
    /// </summary>
    public static ComposableTrigger operator &(ComposableTrigger left, ComposableTrigger right)
        => left.And(right._inner);

    /// <summary>
    /// | 演算子（OR）。
    /// </summary>
    public static ComposableTrigger operator |(ComposableTrigger left, ComposableTrigger right)
        => left.Or(right._inner);
}

/// <summary>
/// トリガーの合成用拡張メソッド。
/// </summary>
public static class TriggerExtensions
{
    /// <summary>
    /// 演算子をサポートする形式に変換。
    /// </summary>
    public static ComposableTrigger Compose(this IInputTrigger<InputState> trigger)
        => new ComposableTrigger(trigger);

    /// <summary>
    /// AND合成（両方成立）。
    /// </summary>
    public static IInputTrigger<InputState> And(this IInputTrigger<InputState> left, IInputTrigger<InputState> right)
        => Triggers.All(left, right);

    /// <summary>
    /// OR合成（いずれか成立）。
    /// </summary>
    public static IInputTrigger<InputState> Or(this IInputTrigger<InputState> left, IInputTrigger<InputState> right)
        => Triggers.Any(left, right);

    /// <summary>
    /// 複数のANDトリガーを連結。
    /// </summary>
    public static IInputTrigger<InputState> AndAll(this IInputTrigger<InputState> first, params IInputTrigger<InputState>[] others)
    {
        var all = new IInputTrigger<InputState>[others.Length + 1];
        all[0] = first;
        Array.Copy(others, 0, all, 1, others.Length);
        return Triggers.All(all);
    }

    /// <summary>
    /// 複数のORトリガーを連結。
    /// </summary>
    public static IInputTrigger<InputState> OrAny(this IInputTrigger<InputState> first, params IInputTrigger<InputState>[] others)
    {
        var all = new IInputTrigger<InputState>[others.Length + 1];
        all[0] = first;
        Array.Copy(others, 0, all, 1, others.Length);
        return Triggers.Any(all);
    }
}
