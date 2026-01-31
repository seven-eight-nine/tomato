using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// 組み込みトリガーのファクトリ（InputState用）。
/// </summary>
public static class Triggers
{
    // ===========================================
    // 基本トリガー
    // ===========================================

    /// <summary>
    /// ボタン押下の瞬間にトリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Press(ButtonType button) => new PressTrigger(button);

    /// <summary>
    /// ボタンリリースの瞬間にトリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Release(ButtonType button) => new ReleaseTrigger(button);

    /// <summary>
    /// ボタンが保持されている間トリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Hold(ButtonType button) => new HoldTrigger(button, 0);

    /// <summary>
    /// ボタンを指定tick数以上保持したらトリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Hold(ButtonType button, int minTicks) => new HoldTrigger(button, minTicks);

    // ===========================================
    // 高度なトリガー
    // ===========================================

    /// <summary>
    /// チャージして離した時にトリガー。段階的なチャージレベルを持つ。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChargeTrigger Charge(ButtonType button, params int[] thresholds)
        => new ChargeTrigger(button, thresholds);

    /// <summary>
    /// 指定tick数内に指定回数ボタンを押したらトリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Mash(ButtonType button, int count, int window)
        => new MashTrigger(button, count, window);

    /// <summary>
    /// 複数ボタンの同時押しでトリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Simultaneous(params ButtonType[] buttons)
        => new SimultaneousTrigger(buttons);

    /// <summary>
    /// コマンド入力（↓↘→+Pなど）でトリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Command(params CommandInput[] sequence)
        => new CommandTrigger(sequence);

    /// <summary>
    /// コマンド入力（↓↘→+Pなど）でトリガー。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Command(CommandInput[] sequence, int totalWindow)
        => new CommandTrigger(sequence, totalWindow);

    // ===========================================
    // 特殊トリガー
    // ===========================================

    /// <summary>
    /// 常にトリガー。AI制御などに使用。
    /// </summary>
    public static IInputTrigger<InputState> Always => AlwaysTrigger<InputState>.Instance;

    /// <summary>
    /// 決してトリガーしない。無効化に使用。
    /// </summary>
    public static IInputTrigger<InputState> Never => NeverTrigger<InputState>.Instance;

    // ===========================================
    // 論理演算
    // ===========================================

    /// <summary>
    /// すべてのトリガーが成立することを要求。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> All(params IInputTrigger<InputState>[] triggers)
        => new AllTrigger<InputState>(triggers);

    /// <summary>
    /// いずれかのトリガーが成立することを要求。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IInputTrigger<InputState> Any(params IInputTrigger<InputState>[] triggers)
        => new AnyTrigger<InputState>(triggers);
}

// ===========================================
// InputState用の基本トリガー実装
// ===========================================

/// <summary>
/// ボタン押下トリガー。
/// </summary>
public sealed class PressTrigger : IInputTrigger<InputState>
{
    private readonly ButtonType _button;

    public PressTrigger(ButtonType button)
    {
        _button = button;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input) => input.IsPressed(_button);

    public void OnJudgmentStart() { }
    public void OnJudgmentStop() { }
    public void OnJudgmentUpdate(in InputState input, int deltaTicks) { }
}

/// <summary>
/// ボタンリリーストリガー。
/// </summary>
public sealed class ReleaseTrigger : IInputTrigger<InputState>
{
    private readonly ButtonType _button;

    public ReleaseTrigger(ButtonType button)
    {
        _button = button;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input) => input.IsReleased(_button);

    public void OnJudgmentStart() { }
    public void OnJudgmentStop() { }
    public void OnJudgmentUpdate(in InputState input, int deltaTicks) { }
}

/// <summary>
/// ボタン保持トリガー。
/// </summary>
public sealed class HoldTrigger : IInputTrigger<InputState>
{
    private readonly ButtonType _button;
    private readonly int _minTicks;
    private int _holdTicks;

    public HoldTrigger(ButtonType button, int minTicks)
    {
        _button = button;
        _minTicks = minTicks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input)
    {
        return input.IsHeld(_button) && _holdTicks >= _minTicks;
    }

    public void OnJudgmentStart()
    {
        _holdTicks = 0;
    }

    public void OnJudgmentStop()
    {
        _holdTicks = 0;
    }

    public void OnJudgmentUpdate(in InputState input, int deltaTicks)
    {
        if (input.IsHeld(_button))
        {
            _holdTicks += deltaTicks;
        }
        else
        {
            _holdTicks = 0;
        }
    }
}
