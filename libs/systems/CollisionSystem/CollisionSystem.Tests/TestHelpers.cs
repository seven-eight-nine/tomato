namespace Tomato.CollisionSystem.Tests;

/// <summary>
/// テスト用レイヤー定義。
/// 実ゲームではゲーム固有のレイヤーを定義する。
/// </summary>
public static class CollisionLayers
{
    public const uint None = 0;
    public const uint Player = 1u << 0;
    public const uint Enemy = 1u << 1;
    public const uint PlayerAttack = 1u << 2;
    public const uint EnemyAttack = 1u << 3;
    public const uint Environment = 1u << 4;
    public const uint Trigger = 1u << 5;
    public const uint All = uint.MaxValue;
}

/// <summary>
/// テスト用ボリュームタイプ。
/// </summary>
public static class VolumeType
{
    public const int Hitbox = 0;
    public const int Hurtbox = 1;
    public const int Projectile = 2;
    public const int Trigger = 3;
}

/// <summary>
/// CollisionFilter拡張（テスト用プリセット）。
/// </summary>
public static class CollisionFilterPresets
{
    public static CollisionFilter PlayerHitbox => new(
        CollisionLayers.Player,
        CollisionLayers.EnemyAttack | CollisionLayers.Environment);

    public static CollisionFilter EnemyHitbox => new(
        CollisionLayers.Enemy,
        CollisionLayers.PlayerAttack | CollisionLayers.Environment);

    public static CollisionFilter PlayerAttack => new(
        CollisionLayers.PlayerAttack,
        CollisionLayers.Enemy);

    public static CollisionFilter EnemyAttack => new(
        CollisionLayers.EnemyAttack,
        CollisionLayers.Player);
}
