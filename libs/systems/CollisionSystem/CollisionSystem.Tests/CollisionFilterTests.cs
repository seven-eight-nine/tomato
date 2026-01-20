using System;
using System.Collections.Generic;
using Xunit;
using Tomato.CollisionSystem;

namespace Tomato.CollisionSystem.Tests;

/// <summary>
/// 衝突フィルタテスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] CollisionFilterを作成できる
/// - [x] 同じレイヤー同士が衝突できる
/// - [x] マスクに含まれないレイヤーとは衝突しない
/// - [x] 双方向のマスクチェックが行われる
/// - [x] プリセットフィルタが正しく動作する
/// - [x] CollisionLayersの定義値が正しい
/// </summary>
public class CollisionFilterTests
{
    [Fact]
    public void CollisionFilter_ShouldBeCreatable()
    {
        var filter = new CollisionFilter(
            layer: CollisionLayers.Player,
            mask: CollisionLayers.EnemyAttack);

        Assert.Equal(CollisionLayers.Player, filter.Layer);
        Assert.Equal(CollisionLayers.EnemyAttack, filter.Mask);
    }

    [Fact]
    public void CollisionFilter_CanCollideWith_WhenLayerInMask_ShouldReturnTrue()
    {
        var playerFilter = new CollisionFilter(
            layer: CollisionLayers.Player,
            mask: CollisionLayers.EnemyAttack);

        var attackFilter = new CollisionFilter(
            layer: CollisionLayers.EnemyAttack,
            mask: CollisionLayers.Player);

        Assert.True(playerFilter.CanCollideWith(attackFilter));
    }

    [Fact]
    public void CollisionFilter_CanCollideWith_WhenLayerNotInMask_ShouldReturnFalse()
    {
        var playerFilter = new CollisionFilter(
            layer: CollisionLayers.Player,
            mask: CollisionLayers.EnemyAttack); // プレイヤーは敵攻撃とのみ衝突

        var environmentFilter = new CollisionFilter(
            layer: CollisionLayers.Environment,
            mask: CollisionLayers.All);

        Assert.False(playerFilter.CanCollideWith(environmentFilter));
    }

    [Fact]
    public void CollisionFilter_CanCollideWith_IsBidirectional()
    {
        // 一方向のみマスクに含む場合は衝突しない
        var filter1 = new CollisionFilter(
            layer: CollisionLayers.Player,
            mask: CollisionLayers.Enemy);

        var filter2 = new CollisionFilter(
            layer: CollisionLayers.Enemy,
            mask: CollisionLayers.Environment); // Playerをマスクに含まない

        Assert.False(filter1.CanCollideWith(filter2));
        Assert.False(filter2.CanCollideWith(filter1));
    }

    [Fact]
    public void CollisionFilter_CanCollideWith_WithAllMask_ShouldCollide()
    {
        var allFilter = new CollisionFilter(
            layer: CollisionLayers.Player,
            mask: CollisionLayers.All);

        var enemyFilter = new CollisionFilter(
            layer: CollisionLayers.Enemy,
            mask: CollisionLayers.All);

        Assert.True(allFilter.CanCollideWith(enemyFilter));
    }

    [Fact]
    public void CollisionFilter_CanCollideWith_WithNoneMask_ShouldNotCollide()
    {
        var noCollisionFilter = new CollisionFilter(
            layer: CollisionLayers.Player,
            mask: CollisionLayers.None);

        var enemyFilter = new CollisionFilter(
            layer: CollisionLayers.Enemy,
            mask: CollisionLayers.All);

        Assert.False(noCollisionFilter.CanCollideWith(enemyFilter));
    }

    [Fact]
    public void CollisionLayers_ShouldHaveCorrectValues()
    {
        Assert.Equal(0u, CollisionLayers.None);
        Assert.Equal(1u << 0, CollisionLayers.Player);
        Assert.Equal(1u << 1, CollisionLayers.Enemy);
        Assert.Equal(1u << 2, CollisionLayers.PlayerAttack);
        Assert.Equal(1u << 3, CollisionLayers.EnemyAttack);
        Assert.Equal(1u << 4, CollisionLayers.Environment);
        Assert.Equal(1u << 5, CollisionLayers.Trigger);
        Assert.Equal(uint.MaxValue, CollisionLayers.All);
    }

    [Fact]
    public void CollisionFilter_PlayerHitbox_ShouldCollideWithEnemyAttack()
    {
        var playerHitbox = CollisionFilterPresets.PlayerHitbox;
        var enemyAttack = CollisionFilterPresets.EnemyAttack;

        Assert.True(playerHitbox.CanCollideWith(enemyAttack));
    }

    [Fact]
    public void CollisionFilter_PlayerHitbox_ShouldNotCollideWithPlayerAttack()
    {
        var playerHitbox = CollisionFilterPresets.PlayerHitbox;
        var playerAttack = CollisionFilterPresets.PlayerAttack;

        Assert.False(playerHitbox.CanCollideWith(playerAttack));
    }

    [Fact]
    public void CollisionFilter_EnemyHitbox_ShouldCollideWithPlayerAttack()
    {
        var enemyHitbox = CollisionFilterPresets.EnemyHitbox;
        var playerAttack = CollisionFilterPresets.PlayerAttack;

        Assert.True(enemyHitbox.CanCollideWith(playerAttack));
    }

    [Fact]
    public void CollisionFilter_MultipleLayers_ShouldWork()
    {
        // プレイヤーと敵両方のレイヤーを持つフィルタ
        var multiLayerFilter = new CollisionFilter(
            layer: CollisionLayers.Player | CollisionLayers.Enemy,
            mask: CollisionLayers.Environment);

        var envFilter = new CollisionFilter(
            layer: CollisionLayers.Environment,
            mask: CollisionLayers.Player | CollisionLayers.Enemy);

        Assert.True(multiLayerFilter.CanCollideWith(envFilter));
    }
}
