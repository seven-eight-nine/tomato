namespace Tomato.ActionSelector;

/// <summary>
/// トリガーの短縮ファクトリ。
///
/// static using と組み合わせて使用する:
/// <code>
/// using static Tomato.ActionSelector.Trig;
/// using static Tomato.ActionSelector.Buttons;
///
/// var trigger = Press(Attack);
/// var holdTrigger = Hold(Guard, 30);  // 30 ticks
/// var cmdTrigger = Cmd(Dirs.Down, Dirs.DownRight, Dirs.Right.Plus(Punch));
///
/// // 演算子を使う場合（Composable版）
/// using static Tomato.ActionSelector.T;
/// var trig = Press(Attack) | Press(Jump);
/// </code>
/// </summary>
public static class Trig
{
    // ===========================================
    // 基本トリガー
    // ===========================================

    /// <summary>
    /// ボタン押下の瞬間にトリガー。
    /// </summary>
    public static IInputTrigger<InputState> Press(ButtonType button)
        => Triggers.Press(button);

    /// <summary>
    /// ボタンリリースの瞬間にトリガー。
    /// </summary>
    public static IInputTrigger<InputState> Release(ButtonType button)
        => Triggers.Release(button);

    /// <summary>
    /// ボタンが保持されている間トリガー。
    /// </summary>
    public static IInputTrigger<InputState> Hold(ButtonType button)
        => Triggers.Hold(button);

    /// <summary>
    /// ボタンを指定tick数以上保持したらトリガー。
    /// </summary>
    public static IInputTrigger<InputState> Hold(ButtonType button, int minTicks)
        => Triggers.Hold(button, minTicks);

    // ===========================================
    // 高度なトリガー
    // ===========================================

    /// <summary>
    /// チャージして離した時にトリガー。
    /// </summary>
    public static ChargeTrigger Charge(ButtonType button, params int[] thresholds)
        => Triggers.Charge(button, thresholds);

    /// <summary>
    /// 連打トリガー。
    /// </summary>
    public static IInputTrigger<InputState> Mash(ButtonType button, int count, int window)
        => Triggers.Mash(button, count, window);

    /// <summary>
    /// 複数ボタンの同時押しでトリガー。
    /// </summary>
    public static IInputTrigger<InputState> Simultaneous(params ButtonType[] buttons)
        => Triggers.Simultaneous(buttons);

    // ===========================================
    // コマンドトリガー
    // ===========================================

    /// <summary>
    /// コマンド入力（↓↘→+Pなど）でトリガー。
    /// </summary>
    public static IInputTrigger<InputState> Command(params CommandInput[] sequence)
        => Triggers.Command(sequence);

    /// <summary>
    /// コマンド入力（↓↘→+Pなど）でトリガー（タイムウィンドウ指定）。
    /// </summary>
    public static IInputTrigger<InputState> Command(CommandInput[] sequence, int totalWindow)
        => Triggers.Command(sequence, totalWindow);

    /// <summary>
    /// 簡易コマンドビルダー。
    /// </summary>
    /// <example>
    /// // 波動拳: ↓↘→+P
    /// Cmd(Down, DownRight, Right + Punch)
    /// </example>
    public static IInputTrigger<InputState> Cmd(params CmdStep[] steps)
        => Triggers.Command(ConvertSteps(steps));

    /// <summary>
    /// 簡易コマンドビルダー（タイムウィンドウ指定）。
    /// </summary>
    public static IInputTrigger<InputState> Cmd(int totalWindow, params CmdStep[] steps)
        => Triggers.Command(ConvertSteps(steps), totalWindow);

    private static CommandInput[] ConvertSteps(CmdStep[] steps)
    {
        var result = new CommandInput[steps.Length];
        for (int i = 0; i < steps.Length; i++)
        {
            result[i] = steps[i].ToCommandInput();
        }
        return result;
    }

    // ===========================================
    // 特殊トリガー
    // ===========================================

    /// <summary>
    /// 常にトリガー。AI制御などに使用。
    /// </summary>
    public static IInputTrigger<InputState> Always => Triggers.Always;

    /// <summary>
    /// 決してトリガーしない。無効化に使用。
    /// </summary>
    public static IInputTrigger<InputState> Never => Triggers.Never;

    // ===========================================
    // 論理演算
    // ===========================================

    /// <summary>
    /// すべてのトリガーが成立することを要求。
    /// </summary>
    public static IInputTrigger<InputState> All(params IInputTrigger<InputState>[] triggers)
        => Triggers.All(triggers);

    /// <summary>
    /// いずれかのトリガーが成立することを要求。
    /// </summary>
    public static IInputTrigger<InputState> Any(params IInputTrigger<InputState>[] triggers)
        => Triggers.Any(triggers);
}

/// <summary>
/// コマンド入力の1ステップ（簡易記法用）。
/// </summary>
public readonly struct CmdStep
{
    public readonly Direction? Direction;
    public readonly ButtonType? Button;

    internal CmdStep(Direction? direction, ButtonType? button)
    {
        Direction = direction;
        Button = button;
    }

    /// <summary>
    /// 方向からのステップ。
    /// </summary>
    public static implicit operator CmdStep(Direction direction)
        => new CmdStep(direction, null);

    /// <summary>
    /// ボタンからのステップ（コマンドの最後）。
    /// </summary>
    public static implicit operator CmdStep(ButtonType button)
        => new CmdStep(null, button);

    /// <summary>
    /// 方向+ボタンの合成。コマンドの最終入力に使用。
    /// </summary>
    public CmdStep With(ButtonType button)
        => new CmdStep(Direction, button);

    /// <summary>
    /// CommandInput に変換。
    /// </summary>
    internal CommandInput ToCommandInput()
    {
        if (Direction.HasValue && Button.HasValue)
            return CommandInput.DirBtn(Direction.Value, Button.Value);
        if (Direction.HasValue)
            return CommandInput.Dir(Direction.Value);
        if (Button.HasValue)
            return CommandInput.Btn(Button.Value);
        return CommandInput.Dir(Tomato.ActionSelector.Direction.Neutral);
    }
}

/// <summary>
/// 方向+ボタンを組み合わせるためのヘルパー拡張。
/// </summary>
public static class CmdStepExtensions
{
    /// <summary>
    /// 方向とボタンを組み合わせてCmdStepを作成。
    /// </summary>
    /// <example>
    /// Dirs.Right.Plus(Punch) → 「→+P」
    /// </example>
    public static CmdStep Plus(this Direction direction, ButtonType button)
        => new CmdStep(direction, button);
}

/// <summary>
/// 演算子が使えるトリガーファクトリ（Composable版）。
///
/// <example>
/// <code>
/// using static Tomato.ActionSelector.T;
/// using static Tomato.ActionSelector.Buttons;
///
/// // 演算子でトリガーを合成
/// var trig = Press(Attack) | Press(Jump);      // OR合成
/// var trig2 = Press(Attack) &amp; Hold(Guard);     // AND合成
/// </code>
/// </example>
/// </summary>
public static class T
{
    // ===========================================
    // 基本トリガー
    // ===========================================

    /// <summary>
    /// ボタン押下の瞬間にトリガー。
    /// </summary>
    public static ComposableTrigger Press(ButtonType button)
        => new ComposableTrigger(Triggers.Press(button));

    /// <summary>
    /// ボタンリリースの瞬間にトリガー。
    /// </summary>
    public static ComposableTrigger Release(ButtonType button)
        => new ComposableTrigger(Triggers.Release(button));

    /// <summary>
    /// ボタンが保持されている間トリガー。
    /// </summary>
    public static ComposableTrigger Hold(ButtonType button)
        => new ComposableTrigger(Triggers.Hold(button));

    /// <summary>
    /// ボタンを指定tick数以上保持したらトリガー。
    /// </summary>
    public static ComposableTrigger Hold(ButtonType button, int minTicks)
        => new ComposableTrigger(Triggers.Hold(button, minTicks));

    // ===========================================
    // 高度なトリガー
    // ===========================================

    /// <summary>
    /// チャージして離した時にトリガー。
    /// </summary>
    public static ComposableTrigger Charge(ButtonType button, params int[] thresholds)
        => new ComposableTrigger(Triggers.Charge(button, thresholds));

    /// <summary>
    /// 連打トリガー。
    /// </summary>
    public static ComposableTrigger Mash(ButtonType button, int count, int window)
        => new ComposableTrigger(Triggers.Mash(button, count, window));

    /// <summary>
    /// 複数ボタンの同時押しでトリガー。
    /// </summary>
    public static ComposableTrigger Simultaneous(params ButtonType[] buttons)
        => new ComposableTrigger(Triggers.Simultaneous(buttons));

    // ===========================================
    // コマンドトリガー
    // ===========================================

    /// <summary>
    /// コマンド入力（↓↘→+Pなど）でトリガー。
    /// </summary>
    public static ComposableTrigger Command(params CommandInput[] sequence)
        => new ComposableTrigger(Triggers.Command(sequence));

    /// <summary>
    /// コマンド入力（↓↘→+Pなど）でトリガー（タイムウィンドウ指定）。
    /// </summary>
    public static ComposableTrigger Command(CommandInput[] sequence, int totalWindow)
        => new ComposableTrigger(Triggers.Command(sequence, totalWindow));

    /// <summary>
    /// 簡易コマンドビルダー。
    /// </summary>
    /// <example>
    /// // 波動拳: ↓↘→+P
    /// Cmd(Down, DownRight, Right.Plus(Punch))
    /// </example>
    public static ComposableTrigger Cmd(params CmdStep[] steps)
        => new ComposableTrigger(Trig.Cmd(steps));

    /// <summary>
    /// 簡易コマンドビルダー（タイムウィンドウ指定）。
    /// </summary>
    public static ComposableTrigger Cmd(int totalWindow, params CmdStep[] steps)
        => new ComposableTrigger(Trig.Cmd(totalWindow, steps));

    // ===========================================
    // 特殊トリガー
    // ===========================================

    /// <summary>
    /// 常にトリガー。AI制御などに使用。
    /// </summary>
    public static ComposableTrigger Always => new ComposableTrigger(Triggers.Always);

    /// <summary>
    /// 決してトリガーしない。無効化に使用。
    /// </summary>
    public static ComposableTrigger Never => new ComposableTrigger(Triggers.Never);
}
