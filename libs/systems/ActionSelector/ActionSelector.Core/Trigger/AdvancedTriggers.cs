using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// 段階的チャージトリガー。
///
/// ボタンを押し続けてチャージし、離した瞬間に発動する。
/// 複数のチャージレベルを持ち、チャージtick数に応じてレベルが上がる。
/// </summary>
/// <remarks>
/// 使用例:
/// <code>
/// // 30tickでLv1、60tickでLv2、120tickでLv3
/// var trigger = Triggers.Charge(ButtonType.Attack, 30, 60, 120);
/// </code>
///
/// パフォーマンス:
/// - 配列は初期化時に1回だけ確保
/// - 判定はシンプルなフラグチェック
/// </remarks>
public sealed class ChargeTrigger : IInputTrigger<InputState>
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly ButtonType _button;
    private readonly int[] _thresholds;

    private int _chargeTicks;
    private int _chargeLevel;
    private bool _released;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// チャージトリガーを生成する。
    /// </summary>
    /// <param name="button">チャージするボタン</param>
    /// <param name="thresholds">各チャージレベルの閾値（tick）。昇順で指定。</param>
    public ChargeTrigger(ButtonType button, int[] thresholds)
    {
        _button = button;
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
    }

    // ===========================================
    // プロパティ
    // ===========================================

    /// <summary>
    /// 現在のチャージレベル（0 = 未チャージ）。
    /// </summary>
    public int ChargeLevel => _chargeLevel;

    /// <summary>
    /// 最大チャージレベル。
    /// </summary>
    public int MaxChargeLevel => _thresholds.Length;

    /// <summary>
    /// 現在のチャージtick数。
    /// </summary>
    public int ChargeTicks => _chargeTicks;

    // ===========================================
    // IInputTrigger 実装
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input)
    {
        // ボタンリリース時にチャージ済みなら発動
        return _released && _chargeLevel > 0;
    }

    public void OnJudgmentStart()
    {
        _chargeTicks = 0;
        _chargeLevel = 0;
        _released = false;
    }

    public void OnJudgmentStop()
    {
        _chargeTicks = 0;
        _chargeLevel = 0;
        _released = false;
    }

    public void OnJudgmentUpdate(in InputState input, int deltaTicks)
    {
        if (input.IsHeld(_button))
        {
            // チャージ中
            _chargeTicks += deltaTicks;
            _released = false;

            // レベルアップ判定
            while (_chargeLevel < _thresholds.Length &&
                   _chargeTicks >= _thresholds[_chargeLevel])
            {
                _chargeLevel++;
            }
        }
        else if (input.IsReleased(_button))
        {
            // リリースの瞬間
            _released = true;
        }
        else
        {
            // リリース後の次フレームでリセット
            if (_released)
            {
                _chargeTicks = 0;
                _chargeLevel = 0;
                _released = false;
            }
        }
    }
}

/// <summary>
/// 連打トリガー。
///
/// 指定tick数内に指定回数ボタンを押すとトリガー。
/// </summary>
/// <remarks>
/// 使用例:
/// <code>
/// // 60tick以内に3回押す
/// var trigger = Triggers.Mash(ButtonType.Attack, 3, 60);
/// </code>
///
/// パフォーマンス:
/// - リングバッファで押下tick時刻を管理
/// - 古い記録は自動的に上書き
/// </remarks>
public sealed class MashTrigger : IInputTrigger<InputState>
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly ButtonType _button;
    private readonly int _requiredCount;
    private readonly int _window;

    // リングバッファで押下tick時刻を記録
    private readonly int[] _pressTimestamps;
    private int _writeIndex;
    private int _currentTick;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// 連打トリガーを生成する。
    /// </summary>
    /// <param name="button">連打するボタン</param>
    /// <param name="requiredCount">必要な押下回数</param>
    /// <param name="window">判定tick数</param>
    public MashTrigger(ButtonType button, int requiredCount, int window)
    {
        if (requiredCount < 1)
            throw new ArgumentOutOfRangeException(nameof(requiredCount), "requiredCount must be >= 1");
        if (window <= 0)
            throw new ArgumentOutOfRangeException(nameof(window), "window must be > 0");

        _button = button;
        _requiredCount = requiredCount;
        _window = window;
        _pressTimestamps = new int[requiredCount];
    }

    // ===========================================
    // IInputTrigger 実装
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input)
    {
        // 有効な押下回数をカウント
        int validCount = 0;
        int threshold = _currentTick - _window;

        for (int i = 0; i < _pressTimestamps.Length; i++)
        {
            if (_pressTimestamps[i] >= threshold)
                validCount++;
        }

        return validCount >= _requiredCount;
    }

    public void OnJudgmentStart()
    {
        Array.Clear(_pressTimestamps, 0, _pressTimestamps.Length);
        _writeIndex = 0;
        _currentTick = 0;
    }

    public void OnJudgmentStop()
    {
        Array.Clear(_pressTimestamps, 0, _pressTimestamps.Length);
        _writeIndex = 0;
        _currentTick = 0;
    }

    public void OnJudgmentUpdate(in InputState input, int deltaTicks)
    {
        _currentTick += deltaTicks;

        if (input.IsPressed(_button))
        {
            // 押下tick時刻を記録（リングバッファ）
            _pressTimestamps[_writeIndex] = _currentTick;
            _writeIndex = (_writeIndex + 1) % _pressTimestamps.Length;
        }
    }
}

/// <summary>
/// 同時押しトリガー。
///
/// 複数のボタンがすべて押されている時にトリガー。
/// </summary>
/// <remarks>
/// 使用例:
/// <code>
/// // 攻撃+ガード同時押し
/// var trigger = Triggers.Simultaneous(ButtonType.Attack, ButtonType.Guard);
/// </code>
/// </remarks>
public sealed class SimultaneousTrigger : IInputTrigger<InputState>
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly ButtonType _combinedButtons;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// 同時押しトリガーを生成する。
    /// </summary>
    public SimultaneousTrigger(params ButtonType[] buttons)
    {
        if (buttons == null || buttons.Length == 0)
            throw new ArgumentException("At least one button required", nameof(buttons));

        // 全ボタンを OR 結合
        _combinedButtons = ButtonType.None;
        for (int i = 0; i < buttons.Length; i++)
        {
            _combinedButtons |= buttons[i];
        }
    }

    // ===========================================
    // IInputTrigger 実装
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input)
    {
        return input.AreAllHeld(_combinedButtons);
    }

    public void OnJudgmentStart() { }
    public void OnJudgmentStop() { }
    public void OnJudgmentUpdate(in InputState input, int deltaTicks) { }
}

/// <summary>
/// コマンド入力の1ステップ。
/// </summary>
/// <remarks>
/// 方向のみ、ボタンのみ、または方向+ボタンを指定可能。
/// </remarks>
public readonly struct CommandInput
{
    /// <summary>
    /// 方向入力（null = 方向不問）。
    /// </summary>
    public readonly Direction? Direction;

    /// <summary>
    /// ボタン入力（null = ボタン不問）。
    /// </summary>
    public readonly ButtonType? Button;

    /// <summary>
    /// このステップの最大受付tick数。
    /// </summary>
    public readonly int MaxDuration;

    private CommandInput(Direction? direction, ButtonType? button, int maxDuration)
    {
        Direction = direction;
        Button = button;
        MaxDuration = maxDuration;
    }

    /// <summary>
    /// 方向のみのステップ。
    /// </summary>
    public static CommandInput Dir(Direction direction, int maxDuration = 12)
        => new(direction, null, maxDuration);

    /// <summary>
    /// ボタンのみのステップ。
    /// </summary>
    public static CommandInput Btn(ButtonType button, int maxDuration = 6)
        => new(null, button, maxDuration);

    /// <summary>
    /// 方向+ボタンのステップ。
    /// </summary>
    public static CommandInput DirBtn(Direction direction, ButtonType button, int maxDuration = 9)
        => new(direction, button, maxDuration);
}

/// <summary>
/// コマンド入力トリガー。
///
/// 波動拳（↓↘→+P）のような連続入力を判定する。
/// </summary>
/// <remarks>
/// 使用例:
/// <code>
/// // 波動拳コマンド
/// var hadouken = Triggers.Command(
///     CommandInput.Dir(Direction.Down),
///     CommandInput.Dir(Direction.DownRight),
///     CommandInput.DirBtn(Direction.Right, ButtonType.Punch));
/// </code>
///
/// パフォーマンス:
/// - ステップごとにタイムアウト管理
/// - 早期リセットで無駄な判定を回避
/// </remarks>
public sealed class CommandTrigger : IInputTrigger<InputState>
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly CommandInput[] _sequence;
    private readonly int _totalWindow;

    private int _currentStep;
    private int _elapsedTicks;
    private int _stepTicks;
    private bool _completed;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// コマンド入力トリガーを生成する。
    /// </summary>
    /// <param name="sequence">コマンドシーケンス</param>
    /// <param name="totalWindow">全体の入力受付tick数</param>
    public CommandTrigger(CommandInput[] sequence, int totalWindow = 30)
    {
        _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        if (sequence.Length == 0)
            throw new ArgumentException("Sequence must not be empty", nameof(sequence));
        _totalWindow = totalWindow;
    }

    // ===========================================
    // プロパティ
    // ===========================================

    /// <summary>
    /// 現在のステップ（0 = 未開始）。
    /// </summary>
    public int CurrentStep => _currentStep;

    /// <summary>
    /// コマンドが完了したかどうか。
    /// </summary>
    public bool IsCompleted => _completed;

    // ===========================================
    // IInputTrigger 実装
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input) => _completed;

    public void OnJudgmentStart() => Reset();
    public void OnJudgmentStop() => Reset();

    public void OnJudgmentUpdate(in InputState input, int deltaTicks)
    {
        if (_completed) return;

        _elapsedTicks += deltaTicks;
        _stepTicks += deltaTicks;

        // 全体タイムアウト
        if (_elapsedTicks > _totalWindow)
        {
            Reset();
            return;
        }

        // 現在のステップのタイムアウト
        if (_currentStep < _sequence.Length &&
            _stepTicks > _sequence[_currentStep].MaxDuration)
        {
            Reset();
            return;
        }

        // ステップマッチング
        if (_currentStep < _sequence.Length)
        {
            var step = _sequence[_currentStep];

            // 方向チェック
            bool directionMatch = !step.Direction.HasValue ||
                                 input.CurrentDirection == step.Direction.Value;

            // ボタンチェック
            bool buttonMatch = !step.Button.HasValue ||
                              input.IsPressed(step.Button.Value);

            if (directionMatch && buttonMatch)
            {
                _currentStep++;
                _stepTicks = 0;

                if (_currentStep >= _sequence.Length)
                {
                    _completed = true;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reset()
    {
        _currentStep = 0;
        _elapsedTicks = 0;
        _stepTicks = 0;
        _completed = false;
    }
}
