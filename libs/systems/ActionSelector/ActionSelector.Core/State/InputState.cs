using System;
using System.Runtime.CompilerServices;

namespace Tomato.ActionSelector;

/// <summary>
/// ボタンの種類。
/// ビットフラグとして使用し、複数のボタンを組み合わせることができる。
/// </summary>
[Flags]
public enum ButtonType : uint
{
    None = 0,

    // 基本ボタン（Face buttons）
    Button0 = 1 << 0,
    Button1 = 1 << 1,
    Button2 = 1 << 2,
    Button3 = 1 << 3,

    // 追加ボタン
    Button4 = 1 << 4,
    Button5 = 1 << 5,
    Button6 = 1 << 6,
    Button7 = 1 << 7,

    // ショルダー/トリガー
    L1 = 1 << 12,
    L2 = 1 << 13,
    R1 = 1 << 14,
    R2 = 1 << 15,

    // 方向入力
    Up = 1 << 16,
    Down = 1 << 17,
    Left = 1 << 18,
    Right = 1 << 19,

    // システム
    Start = 1 << 20,
    Select = 1 << 21,
}

/// <summary>
/// 方向。8方向 + ニュートラル。
/// </summary>
public enum Direction : byte
{
    Neutral = 0,
    Up = 1,
    UpRight = 2,
    Right = 3,
    DownRight = 4,
    Down = 5,
    DownLeft = 6,
    Left = 7,
    UpLeft = 8,
}

/// <summary>
/// 現在フレームの入力状態。
/// 押下・保持・リリースの状態をビットフラグで高速に判定。
/// </summary>
public readonly struct InputState
{
    /// <summary>
    /// 現在フレームで押されているボタン（押下中 or 保持中）。
    /// </summary>
    public readonly ButtonType Held;

    /// <summary>
    /// このフレームで新たに押されたボタン。
    /// </summary>
    public readonly ButtonType Pressed;

    /// <summary>
    /// このフレームで離されたボタン。
    /// </summary>
    public readonly ButtonType Released;

    /// <summary>
    /// 現在の方向入力。
    /// </summary>
    public readonly Direction CurrentDirection;

    /// <summary>
    /// 左スティックのX軸（-1.0 〜 1.0）。
    /// </summary>
    public readonly float LeftStickX;

    /// <summary>
    /// 左スティックのY軸（-1.0 〜 1.0）。
    /// </summary>
    public readonly float LeftStickY;

    /// <summary>
    /// 右スティックのX軸（-1.0 〜 1.0）。
    /// </summary>
    public readonly float RightStickX;

    /// <summary>
    /// 右スティックのY軸（-1.0 〜 1.0）。
    /// </summary>
    public readonly float RightStickY;

    public InputState(
        ButtonType held,
        ButtonType pressed,
        ButtonType released,
        Direction direction = Direction.Neutral,
        float leftStickX = 0f,
        float leftStickY = 0f,
        float rightStickX = 0f,
        float rightStickY = 0f)
    {
        Held = held;
        Pressed = pressed;
        Released = released;
        CurrentDirection = direction;
        LeftStickX = leftStickX;
        LeftStickY = leftStickY;
        RightStickX = rightStickX;
        RightStickY = rightStickY;
    }

    /// <summary>
    /// このフレームでボタンが押されたか（押下の瞬間）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPressed(ButtonType button) => (Pressed & button) == button;

    /// <summary>
    /// ボタンが保持されているか（押下中）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsHeld(ButtonType button) => (Held & button) == button;

    /// <summary>
    /// このフレームでボタンが離されたか（リリースの瞬間）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsReleased(ButtonType button) => (Released & button) == button;

    /// <summary>
    /// 複数ボタンがすべて押されているか。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AreAllHeld(ButtonType buttons) => (Held & buttons) == buttons;

    /// <summary>
    /// 複数ボタンのいずれかが押されているか。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAnyHeld(ButtonType buttons) => (Held & buttons) != ButtonType.None;

    /// <summary>
    /// 方向入力があるか。
    /// </summary>
    public bool HasDirectionalInput
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CurrentDirection != Direction.Neutral;
    }

    /// <summary>
    /// 何も入力されていない状態。
    /// </summary>
    public static readonly InputState Empty = new(
        ButtonType.None,
        ButtonType.None,
        ButtonType.None,
        Direction.Neutral);
}
