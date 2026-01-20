using System;

namespace Tomato.ActionSelector.Tests;

// =============================================
// テスト用ゲーム固有定義
// これらはライブラリ層ではなくテスト/ゲーム層に属する
// =============================================

/// <summary>
/// テスト用キャラクターフラグ。
/// </summary>
[Flags]
public enum CharacterFlags : uint
{
    None = 0,
    Grounded = 1 << 0,
    Airborne = 1 << 1,
    Attacking = 1 << 2,
    Guarding = 1 << 3,
    InCombo = 1 << 4,
}

/// <summary>
/// テスト用キャラクター状態。
/// </summary>
public readonly struct CharacterState
{
    public readonly CharacterFlags Flags;
    public readonly float Health;
    public readonly float MaxHealth;
    public readonly float Stamina;
    public readonly float MaxStamina;

    public CharacterState(
        CharacterFlags flags = CharacterFlags.Grounded,
        float health = 100f,
        float maxHealth = 100f,
        float stamina = 100f,
        float maxStamina = 100f)
    {
        Flags = flags;
        Health = health;
        MaxHealth = maxHealth;
        Stamina = stamina;
        MaxStamina = maxStamina;
    }

    public bool IsGrounded => (Flags & CharacterFlags.Grounded) != 0;
    public bool IsAirborne => (Flags & CharacterFlags.Airborne) != 0;
    public float HealthRatio => MaxHealth > 0 ? Health / MaxHealth : 0;
    public float StaminaRatio => MaxStamina > 0 ? Stamina / MaxStamina : 0;

    public static readonly CharacterState DefaultGrounded = new(CharacterFlags.Grounded);
    public static readonly CharacterState DefaultAirborne = new(CharacterFlags.Airborne);
}

/// <summary>
/// テスト用コンバット状態。
/// </summary>
public readonly struct CombatState
{
    public readonly bool IsLockedOn;
    public readonly float DistanceToTarget;
    public readonly int ComboCount;
    public readonly int StyleRank;

    public CombatState(
        bool isLockedOn = false,
        float distanceToTarget = float.MaxValue,
        int comboCount = 0,
        int styleRank = 0)
    {
        IsLockedOn = isLockedOn;
        DistanceToTarget = distanceToTarget;
        ComboCount = comboCount;
        StyleRank = styleRank;
    }

    public bool HasTarget => IsLockedOn;
}

/// <summary>
/// テスト用ボタンタイプ拡張（ゲーム固有名）。
/// </summary>
public static class GameButtonTypes
{
    // 格ゲー用エイリアス
    public const ButtonType Attack = ButtonType.Button0;
    public const ButtonType Jump = ButtonType.Button1;
    public const ButtonType Guard = ButtonType.Button2;
    public const ButtonType Dash = ButtonType.Button3;
    public const ButtonType Special = ButtonType.Button4;
    public const ButtonType Punch = ButtonType.Button5;
    public const ButtonType Kick = ButtonType.Button6;
    public const ButtonType LightAttack = ButtonType.Button0;
    public const ButtonType HeavyAttack = ButtonType.Button4;
}

/// <summary>
/// テスト用GameState拡張。
/// </summary>
public static class GameStateExtensions
{
    public static readonly GameState DefaultGrounded = new(
        InputState.Empty,
        flags: (uint)CharacterFlags.Grounded);

    public static readonly GameState DefaultAirborne = new(
        InputState.Empty,
        flags: (uint)CharacterFlags.Airborne);

    public static GameStateBuilder CreateBuilder() => new();
}

/// <summary>
/// GameStateビルダー（テスト用）。
/// </summary>
public class GameStateBuilder
{
    private InputState _input = InputState.Empty;
    private CharacterState _character = CharacterState.DefaultGrounded;
    private CombatState _combat = default;
    private uint _flags = 0;

    public GameStateBuilder WithInput(InputState input)
    {
        _input = input;
        return this;
    }

    public GameStateBuilder WithGrounded()
    {
        _flags |= (uint)CharacterFlags.Grounded;
        _flags &= ~(uint)CharacterFlags.Airborne;
        return this;
    }

    public GameStateBuilder WithAirborne()
    {
        _flags |= (uint)CharacterFlags.Airborne;
        _flags &= ~(uint)CharacterFlags.Grounded;
        return this;
    }

    public GameStateBuilder WithCharacter(CharacterState character)
    {
        _character = character;
        _flags = (uint)character.Flags;
        return this;
    }

    public GameStateBuilder WithCombat(CombatState combat)
    {
        _combat = combat;
        return this;
    }

    public GameState Build() => new(_input, flags: _flags);
}

/// <summary>
/// テスト用条件ファクトリ（ゲーム固有条件）。
/// </summary>
public static class Cond
{
    public static ICondition<GameState> Always => AlwaysCondition<GameState>.Instance;
    public static ICondition<GameState> Never => NeverCondition<GameState>.Instance;

    public static ICondition<GameState> Grounded =>
        new DelegateCondition<GameState>(s => s.HasFlag((uint)CharacterFlags.Grounded));

    public static ICondition<GameState> Airborne =>
        new DelegateCondition<GameState>(s => s.HasFlag((uint)CharacterFlags.Airborne));

    public static ICondition<GameState> InCombo =>
        new DelegateCondition<GameState>(s => s.HasFlag((uint)CharacterFlags.InCombo));

    public static ICondition<GameState> HasTarget =>
        new DelegateCondition<GameState>(s => s.HasFlag((uint)CharacterFlags.InCombo)); // Simplified

    public static ICondition<GameState> HealthAbove(float ratio) =>
        new DelegateCondition<GameState>(s => true); // Simplified for tests

    public static ICondition<GameState> HealthBelow(float ratio) =>
        new DelegateCondition<GameState>(s => false); // Simplified for tests

    public static ICondition<GameState> TargetInRange(float range) =>
        new DelegateCondition<GameState>(s => true); // Simplified for tests

    public static ICondition<GameState> Not(ICondition<GameState> condition) =>
        new NotCondition<GameState>(condition);

    public static ICondition<GameState> All(params ICondition<GameState>[] conditions) =>
        new AllCondition<GameState>(conditions);

    public static ICondition<GameState> Any(params ICondition<GameState>[] conditions) =>
        new AnyCondition<GameState>(conditions);
}

/// <summary>
/// テスト用短縮名 B クラス。
/// </summary>
public static class B
{
    public static ButtonType P => GameButtonTypes.Punch;
    public static ButtonType K => GameButtonTypes.Kick;
    public static ButtonType LP => GameButtonTypes.LightAttack;
    public static ButtonType HP => GameButtonTypes.HeavyAttack;
    public static ButtonType Atk => GameButtonTypes.Attack;
}
