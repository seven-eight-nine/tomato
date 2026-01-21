using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// 段階的チャージトリガー。
///
/// ボタンを押し続けてチャージし、離した瞬間に発動する。
/// 複数のチャージレベルを持ち、チャージ時間に応じてレベルが上がる。
/// </summary>
/// <remarks>
/// 使用例:
/// <code>
/// // 0.5秒でLv1、1.0秒でLv2、2.0秒でLv3
/// var trigger = Triggers.Charge(ButtonType.Attack, 0.5f, 1.0f, 2.0f);
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
    private readonly float[] _thresholds;

    private float _chargeTime;
    private int _chargeLevel;
    private bool _released;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// チャージトリガーを生成する。
    /// </summary>
    /// <param name="button">チャージするボタン</param>
    /// <param name="thresholds">各チャージレベルの閾値（秒）。昇順で指定。</param>
    public ChargeTrigger(ButtonType button, float[] thresholds)
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
    /// 現在のチャージ時間（秒）。
    /// </summary>
    public float ChargeTime => _chargeTime;

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
        _chargeTime = 0f;
        _chargeLevel = 0;
        _released = false;
    }

    public void OnJudgmentStop()
    {
        _chargeTime = 0f;
        _chargeLevel = 0;
        _released = false;
    }

    public void OnJudgmentUpdate(in InputState input, float deltaTime)
    {
        if (input.IsHeld(_button))
        {
            // チャージ中
            _chargeTime += deltaTime;
            _released = false;

            // レベルアップ判定
            while (_chargeLevel < _thresholds.Length &&
                   _chargeTime >= _thresholds[_chargeLevel])
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
                _chargeTime = 0f;
                _chargeLevel = 0;
                _released = false;
            }
        }
    }
}

/// <summary>
/// 連打トリガー。
///
/// 指定時間内に指定回数ボタンを押すとトリガー。
/// </summary>
/// <remarks>
/// 使用例:
/// <code>
/// // 1秒以内に3回押す
/// var trigger = Triggers.Mash(ButtonType.Attack, 3, 1.0f);
/// </code>
///
/// パフォーマンス:
/// - リングバッファで押下時刻を管理
/// - 古い記録は自動的に上書き
/// </remarks>
public sealed class MashTrigger : IInputTrigger<InputState>
{
    // ===========================================
    // フィールド
    // ===========================================

    private readonly ButtonType _button;
    private readonly int _requiredCount;
    private readonly float _window;

    // リングバッファで押下時刻を記録
    private readonly float[] _pressTimestamps;
    private int _writeIndex;
    private float _currentTime;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// 連打トリガーを生成する。
    /// </summary>
    /// <param name="button">連打するボタン</param>
    /// <param name="requiredCount">必要な押下回数</param>
    /// <param name="window">判定時間（秒）</param>
    public MashTrigger(ButtonType button, int requiredCount, float window)
    {
        if (requiredCount < 1)
            throw new ArgumentOutOfRangeException(nameof(requiredCount), "requiredCount must be >= 1");
        if (window <= 0)
            throw new ArgumentOutOfRangeException(nameof(window), "window must be > 0");

        _button = button;
        _requiredCount = requiredCount;
        _window = window;
        _pressTimestamps = new float[requiredCount];
    }

    // ===========================================
    // IInputTrigger 実装
    // ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsTriggered(in InputState input)
    {
        // 有効な押下回数をカウント
        int validCount = 0;
        float threshold = _currentTime - _window;

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
        _currentTime = 0f;
    }

    public void OnJudgmentStop()
    {
        Array.Clear(_pressTimestamps, 0, _pressTimestamps.Length);
        _writeIndex = 0;
        _currentTime = 0f;
    }

    public void OnJudgmentUpdate(in InputState input, float deltaTime)
    {
        _currentTime += deltaTime;

        if (input.IsPressed(_button))
        {
            // 押下時刻を記録（リングバッファ）
            _pressTimestamps[_writeIndex] = _currentTime;
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
    public void OnJudgmentUpdate(in InputState input, float deltaTime) { }
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
    /// このステップの最大受付時間（秒）。
    /// </summary>
    public readonly float MaxDuration;

    private CommandInput(Direction? direction, ButtonType? button, float maxDuration)
    {
        Direction = direction;
        Button = button;
        MaxDuration = maxDuration;
    }

    /// <summary>
    /// 方向のみのステップ。
    /// </summary>
    public static CommandInput Dir(Direction direction, float maxDuration = 0.2f)
        => new(direction, null, maxDuration);

    /// <summary>
    /// ボタンのみのステップ。
    /// </summary>
    public static CommandInput Btn(ButtonType button, float maxDuration = 0.1f)
        => new(null, button, maxDuration);

    /// <summary>
    /// 方向+ボタンのステップ。
    /// </summary>
    public static CommandInput DirBtn(Direction direction, ButtonType button, float maxDuration = 0.15f)
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
    private readonly float _totalWindow;

    private int _currentStep;
    private float _elapsedTime;
    private float _stepTime;
    private bool _completed;

    // ===========================================
    // コンストラクタ
    // ===========================================

    /// <summary>
    /// コマンド入力トリガーを生成する。
    /// </summary>
    /// <param name="sequence">コマンドシーケンス</param>
    /// <param name="totalWindow">全体の入力受付時間（秒）</param>
    public CommandTrigger(CommandInput[] sequence, float totalWindow = 0.5f)
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

    public void OnJudgmentUpdate(in InputState input, float deltaTime)
    {
        if (_completed) return;

        _elapsedTime += deltaTime;
        _stepTime += deltaTime;

        // 全体タイムアウト
        if (_elapsedTime > _totalWindow)
        {
            Reset();
            return;
        }

        // 現在のステップのタイムアウト
        if (_currentStep < _sequence.Length &&
            _stepTime > _sequence[_currentStep].MaxDuration)
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
                _stepTime = 0f;

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
        _elapsedTime = 0f;
        _stepTime = 0f;
        _completed = false;
    }
}
