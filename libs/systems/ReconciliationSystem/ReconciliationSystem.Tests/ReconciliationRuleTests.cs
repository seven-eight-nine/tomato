using System;
using Xunit;
using Tomato.ReconciliationSystem;
using Tomato.CommandGenerator;
using Tomato.Math;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem.Tests;

/// <summary>
/// ReconciliationRule テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] EntityTypeを定義できる
/// - [x] PriorityBasedReconciliationRuleを作成できる
/// - [x] 同優先度の場合、半分ずつ押し出される
/// - [x] 高優先度Entity側は押し出されない
/// - [x] 優先度を動的に変更できる
/// </summary>
public class ReconciliationRuleTests
{
    private readonly MockArena _arena = new();

    [Fact]
    public void EntityType_ShouldHaveExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(EntityType), EntityType.Player));
        Assert.True(Enum.IsDefined(typeof(EntityType), EntityType.Enemy));
        Assert.True(Enum.IsDefined(typeof(EntityType), EntityType.Wall));
    }

    [Fact]
    public void PriorityBasedReconciliationRule_ShouldBeCreatable()
    {
        var rule = new PriorityBasedReconciliationRule();

        Assert.NotNull(rule);
    }

    [Fact]
    public void ComputePushout_SamePriority_ShouldSplitEqually()
    {
        var rule = new PriorityBasedReconciliationRule();
        var entityA = _arena.CreateHandle(1);
        var entityB = _arena.CreateHandle(2);

        // 法線がX方向、貫通深度0.2
        var normal = new Vector3(1, 0, 0);  // A -> B 方向
        var penetration = 0.2f;

        rule.ComputePushout(
            entityA, EntityType.Player,
            entityB, EntityType.Player,  // 同じ優先度
            in normal, penetration,
            out var pushoutA,
            out var pushoutB);

        // 半分ずつ押し出し (Aは反対方向)
        Assert.Equal(-0.1f, pushoutA.X, 3);
        Assert.Equal(0.1f, pushoutB.X, 3);
    }

    [Fact]
    public void ComputePushout_HigherPriority_ShouldNotBePushed()
    {
        var rule = new PriorityBasedReconciliationRule();
        var wall = _arena.CreateHandle(1);
        var player = _arena.CreateHandle(2);

        var normal = new Vector3(1, 0, 0);
        var penetration = 0.5f;

        rule.ComputePushout(
            wall, EntityType.Wall,     // 高優先度
            player, EntityType.Player, // 低優先度
            in normal, penetration,
            out var pushoutWall,
            out var pushoutPlayer);

        // 壁は動かない、プレイヤーだけ押し出される
        Assert.Equal(Vector3.Zero, pushoutWall);
        Assert.Equal(0.5f, pushoutPlayer.X, 3);
    }

    [Fact]
    public void ComputePushout_LowerPriorityFirst_ShouldBePushedBack()
    {
        var rule = new PriorityBasedReconciliationRule();
        var player = _arena.CreateHandle(1);
        var wall = _arena.CreateHandle(2);

        var normal = new Vector3(1, 0, 0);
        var penetration = 0.5f;

        rule.ComputePushout(
            player, EntityType.Player, // 低優先度
            wall, EntityType.Wall,     // 高優先度
            in normal, penetration,
            out var pushoutPlayer,
            out var pushoutWall);

        // 壁は動かない、プレイヤーだけ押し出される（反対方向）
        Assert.Equal(-0.5f, pushoutPlayer.X, 3);
        Assert.Equal(Vector3.Zero, pushoutWall);
    }

    [Fact]
    public void SetPriority_ShouldChangePriority()
    {
        var rule = new PriorityBasedReconciliationRule();
        var npc = _arena.CreateHandle(1);
        var player = _arena.CreateHandle(2);

        // NPCをPlayerより高優先度に変更
        rule.SetPriority(EntityType.NPC, 100);  // PlayerのデフォルトPriority(50)より高く

        var normal = new Vector3(1, 0, 0);
        var penetration = 1.0f;

        rule.ComputePushout(
            npc, EntityType.NPC,
            player, EntityType.Player,
            in normal, penetration,
            out var pushoutNPC,
            out var pushoutPlayer);

        // NPCは動かない（高優先度になった）
        Assert.Equal(Vector3.Zero, pushoutNPC);
        Assert.Equal(1.0f, pushoutPlayer.X, 3);
    }

    #region Helper Classes

    private class MockArena : IEntityArena
    {
        public AnyHandle CreateHandle(int index) => new AnyHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    #endregion
}
