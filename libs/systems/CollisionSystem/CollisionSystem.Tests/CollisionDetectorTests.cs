using System;
using System.Collections.Generic;
using Xunit;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;

namespace Tomato.CollisionSystem.Tests;

/// <summary>
/// 衝突検出テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] CollisionDetectorを作成できる
/// - [x] ボリュームを追加できる
/// - [x] 衝突を検出できる
/// - [x] フィルタに従って衝突をフィルタリングする
/// - [x] 衝突結果にコンタクト情報が含まれる
/// - [x] 期限切れボリュームを自動削除する
/// - [x] 同じオーナーのボリューム同士は衝突しない
/// </summary>
public class CollisionDetectorTests
{
    private readonly MockArena _arena = new();

    [Fact]
    public void CollisionDetector_ShouldBeCreatable()
    {
        var detector = new CollisionDetector();

        Assert.NotNull(detector);
    }

    [Fact]
    public void CollisionDetector_AddVolume_ShouldRegisterVolume()
    {
        var detector = new CollisionDetector();
        var volume = CreateHitbox(_arena.CreateHandle(1), Vector3.Zero);

        detector.AddVolume(volume, Vector3.Zero);

        Assert.Equal(1, detector.VolumeCount);
    }

    [Fact]
    public void CollisionDetector_DetectCollisions_WhenOverlapping_ShouldReturnCollision()
    {
        var detector = new CollisionDetector();

        // プレイヤーの被ダメージ判定
        var playerHurtbox = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);

        // 敵の攻撃判定
        var enemyHitbox = new CollisionVolume(
            owner: _arena.CreateHandle(2),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.EnemyAttack,
            volumeType: VolumeType.Hitbox);

        detector.AddVolume(playerHurtbox, new Vector3(0f, 0f, 0f));
        detector.AddVolume(enemyHitbox, new Vector3(1f, 0f, 0f)); // 重なる位置

        var results = new List<CollisionResult>();
        detector.DetectCollisions(results);

        Assert.Single(results);
    }

    [Fact]
    public void CollisionDetector_DetectCollisions_WhenNotOverlapping_ShouldReturnEmpty()
    {
        var detector = new CollisionDetector();

        var playerHurtbox = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);

        var enemyHitbox = new CollisionVolume(
            owner: _arena.CreateHandle(2),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.EnemyAttack,
            volumeType: VolumeType.Hitbox);

        detector.AddVolume(playerHurtbox, new Vector3(0f, 0f, 0f));
        detector.AddVolume(enemyHitbox, new Vector3(10f, 0f, 0f)); // 離れた位置

        var results = new List<CollisionResult>();
        detector.DetectCollisions(results);

        Assert.Empty(results);
    }

    [Fact]
    public void CollisionDetector_DetectCollisions_ShouldRespectFilter()
    {
        var detector = new CollisionDetector();

        // プレイヤーの被ダメージ判定（敵攻撃とのみ衝突）
        var playerHurtbox = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);

        // プレイヤーの攻撃判定（敵とのみ衝突）
        var playerHitbox = new CollisionVolume(
            owner: _arena.CreateHandle(2),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerAttack,
            volumeType: VolumeType.Hitbox);

        detector.AddVolume(playerHurtbox, new Vector3(0f, 0f, 0f));
        detector.AddVolume(playerHitbox, new Vector3(0.5f, 0f, 0f)); // 重なる位置

        var results = new List<CollisionResult>();
        detector.DetectCollisions(results);

        // プレイヤー同士は衝突しない（フィルタが一致しない）
        Assert.Empty(results);
    }

    [Fact]
    public void CollisionDetector_DetectCollisions_ShouldIncludeContactInfo()
    {
        var detector = new CollisionDetector();

        var playerHurtbox = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);

        var enemyHitbox = new CollisionVolume(
            owner: _arena.CreateHandle(2),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.EnemyAttack,
            volumeType: VolumeType.Hitbox);

        detector.AddVolume(playerHurtbox, new Vector3(0f, 0f, 0f));
        detector.AddVolume(enemyHitbox, new Vector3(1f, 0f, 0f));

        var results = new List<CollisionResult>();
        detector.DetectCollisions(results);

        Assert.Single(results);
        Assert.True(results[0].Contact.Penetration > 0);
    }

    [Fact]
    public void CollisionDetector_Tick_ShouldRemoveExpiredVolumes()
    {
        var detector = new CollisionDetector();

        var expiring = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerAttack,
            volumeType: VolumeType.Hitbox,
            lifetime: 1); // 1フレームで期限切れ

        detector.AddVolume(expiring, Vector3.Zero);
        Assert.Equal(1, detector.VolumeCount);

        detector.Tick();

        Assert.Equal(0, detector.VolumeCount);
    }

    [Fact]
    public void CollisionDetector_DetectCollisions_SameOwner_ShouldNotCollide()
    {
        var detector = new CollisionDetector();

        var sameOwner = _arena.CreateHandle(1);

        // 同じオーナーの2つのボリューム
        var volume1 = new CollisionVolume(
            owner: sameOwner,
            shape: new SphereShape(1.0f),
            filter: new CollisionFilter(CollisionLayers.Player, CollisionLayers.All),
            volumeType: VolumeType.Hurtbox);

        var volume2 = new CollisionVolume(
            owner: sameOwner, // 同じオーナー
            shape: new SphereShape(1.0f),
            filter: new CollisionFilter(CollisionLayers.Player, CollisionLayers.All),
            volumeType: VolumeType.Hitbox);

        detector.AddVolume(volume1, new Vector3(0f, 0f, 0f));
        detector.AddVolume(volume2, new Vector3(0.5f, 0f, 0f));

        var results = new List<CollisionResult>();
        detector.DetectCollisions(results);

        Assert.Empty(results);
    }

    [Fact]
    public void CollisionDetector_Clear_ShouldRemoveAllVolumes()
    {
        var detector = new CollisionDetector();

        detector.AddVolume(CreateHitbox(_arena.CreateHandle(1), Vector3.Zero), Vector3.Zero);
        detector.AddVolume(CreateHitbox(_arena.CreateHandle(2), Vector3.One), Vector3.One);

        detector.Clear();

        Assert.Equal(0, detector.VolumeCount);
    }

    [Fact]
    public void CollisionResult_ShouldContainBothVolumes()
    {
        var detector = new CollisionDetector();

        var playerHurtbox = new CollisionVolume(
            owner: _arena.CreateHandle(1),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.PlayerHitbox,
            volumeType: VolumeType.Hurtbox);

        var enemyHitbox = new CollisionVolume(
            owner: _arena.CreateHandle(2),
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.EnemyAttack,
            volumeType: VolumeType.Hitbox);

        detector.AddVolume(playerHurtbox, new Vector3(0f, 0f, 0f));
        detector.AddVolume(enemyHitbox, new Vector3(1f, 0f, 0f));

        var results = new List<CollisionResult>();
        detector.DetectCollisions(results);

        Assert.Single(results);
        // 順序は不定なので、両方のボリュームが含まれていることを確認
        var volumes = new[] { results[0].Volume1, results[0].Volume2 };
        Assert.Contains(playerHurtbox, volumes);
        Assert.Contains(enemyHitbox, volumes);
    }

    #region Helper Methods

    private static CollisionVolume CreateHitbox(VoidHandle owner, Vector3 position)
    {
        return new CollisionVolume(
            owner: owner,
            shape: new SphereShape(1.0f),
            filter: CollisionFilterPresets.EnemyAttack,
            volumeType: VolumeType.Hitbox);
    }

    #endregion

    #region Helper Classes

    private class MockArena : IEntityArena
    {
        public VoidHandle CreateHandle(int index) => new VoidHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    #endregion
}
