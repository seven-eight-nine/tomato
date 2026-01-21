using System.Collections.Generic;
using System.Linq;
using Tomato.CombatSystem.Tests.Mocks;
using Xunit;

namespace Tomato.CombatSystem.Tests;

public class CombatManagerTests
{
    private static DamageBody CreateBody(IDamageReceiver owner, int priority = 1)
    {
        var body = new DamageBody { Priority = priority };
        body.BindOwner(owner);
        return body;
    }

    [Fact]
    public void CreateAttack_CreatesValidHandle()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();

        var handle = manager.CreateAttack(info);

        Assert.True(handle.IsValid);
        Assert.Equal(1, manager.ActiveAttackCount);
    }

    [Fact]
    public void ReleaseAttack_InvalidatesHandle()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle = manager.CreateAttack(info);

        manager.ReleaseAttack(handle);

        Assert.False(handle.IsValid);
        Assert.Equal(0, manager.ActiveAttackCount);
    }

    [Fact]
    public void AttackTo_SingleTarget_Success()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target = new MockDamageReceiver("Target");
        var info = new MockAttackInfo { Attacker = attacker };
        var handle = manager.CreateAttack(info);
        var body = CreateBody(target);

        var result = manager.AttackTo(handle, body);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, target.DamageReceivedCount);
    }

    [Fact]
    public void AttackTo_InvalidHandle_ReturnsInvalidHandle()
    {
        var manager = new CombatManager();
        var target = new MockDamageReceiver("Target");
        var body = CreateBody(target);

        var result = manager.AttackTo(AttackHandle.Invalid, body);

        Assert.Equal(AttackResultStatus.InvalidHandle, result.Status);
    }

    [Fact]
    public void AttackTo_NullTarget_ReturnsInvalidTarget()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle = manager.CreateAttack(info);

        var result = manager.AttackTo(handle, (DamageBody)null!);

        Assert.Equal(AttackResultStatus.InvalidTarget, result.Status);
    }

    [Fact]
    public void AttackTo_SelfTarget_ReturnsTargetFiltered()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Self");
        var info = new MockAttackInfo { Attacker = attacker };
        var handle = manager.CreateAttack(info);
        var body = CreateBody(attacker);

        var result = manager.AttackTo(handle, body);

        Assert.Equal(AttackResultStatus.TargetFiltered, result.Status);
    }

    [Fact]
    public void AttackTo_AttackableCountLimit_ReturnsAttackLimitReached()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target1 = new MockDamageReceiver("Target1");
        var target2 = new MockDamageReceiver("Target2");
        var info = new MockAttackInfo
        {
            Attacker = attacker,
            AttackableCount = 1  // 1回のみ攻撃可能
        };
        var handle = manager.CreateAttack(info);
        var body1 = CreateBody(target1);
        var body2 = CreateBody(target2);

        var result1 = manager.AttackTo(handle, body1);
        var result2 = manager.AttackTo(handle, body2);

        Assert.True(result1.IsSuccess);
        Assert.Equal(AttackResultStatus.AttackLimitReached, result2.Status);
    }

    [Fact]
    public void AttackTo_HittableCountLimit_ReturnsHitLimitReached()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target = new MockDamageReceiver("Target");
        var info = new MockAttackInfo
        {
            Attacker = attacker,
            HittableCount = 1  // 同一ターゲットに1回のみ
        };
        var handle = manager.CreateAttack(info);
        var body = CreateBody(target);

        var result1 = manager.AttackTo(handle, body);
        var result2 = manager.AttackTo(handle, body);

        Assert.True(result1.IsSuccess);
        Assert.Equal(AttackResultStatus.HitLimitReached, result2.Status);
    }

    [Fact]
    public void AttackTo_MultipleTargets_AttacksInPriorityOrder()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var targetLow = new MockDamageReceiver("TargetLow");
        var targetHigh = new MockDamageReceiver("TargetHigh");
        var info = new MockAttackInfo { Attacker = attacker };
        var handle = manager.CreateAttack(info);
        var bodies = new List<DamageBody>
        {
            CreateBody(targetLow, priority: 1),
            CreateBody(targetHigh, priority: 10)
        };

        var results = manager.AttackTo(handle, bodies);

        Assert.Equal(2, results.Count);
        Assert.True(results.All(r => r.IsSuccess));
        // 高Priorityのtargetが先に攻撃される
        Assert.Equal(targetHigh, targetHigh.ReceivedDamages[0].Target);
    }

    [Fact]
    public void AttackTo_MultipleTargets_DeduplicatesSameOwner()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target = new MockDamageReceiver("Target");
        var info = new MockAttackInfo { Attacker = attacker };
        var handle = manager.CreateAttack(info);
        var bodies = new List<DamageBody>
        {
            CreateBody(target, priority: 1),
            CreateBody(target, priority: 10)  // 同じOwner
        };

        var results = manager.AttackTo(handle, bodies);

        Assert.Single(results);
        Assert.Equal(1, target.DamageReceivedCount);
    }

    [Fact]
    public void AttackTo_MultipleTargets_StopsAtAttackLimit()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target1 = new MockDamageReceiver("Target1");
        var target2 = new MockDamageReceiver("Target2");
        var target3 = new MockDamageReceiver("Target3");
        var info = new MockAttackInfo
        {
            Attacker = attacker,
            AttackableCount = 2
        };
        var handle = manager.CreateAttack(info);
        var bodies = new List<DamageBody>
        {
            CreateBody(target1, priority: 1),
            CreateBody(target2, priority: 2),
            CreateBody(target3, priority: 3)
        };

        var results = manager.AttackTo(handle, bodies);

        Assert.Equal(3, results.Count);
        Assert.True(results[0].IsSuccess);
        Assert.True(results[1].IsSuccess);
        Assert.Equal(AttackResultStatus.AttackLimitReached, results[2].Status);
    }

    [Fact]
    public void FilterTargets_FiltersBasedOnCanTarget()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var friend = new MockDamageReceiver("Friend");
        var enemy = new MockDamageReceiver("Enemy");
        var info = new MockAttackInfo(new HashSet<IDamageReceiver> { friend })
        {
            Attacker = attacker
        };
        var handle = manager.CreateAttack(info);
        var bodies = new List<DamageBody>
        {
            CreateBody(friend),
            CreateBody(enemy)
        };

        var filtered = manager.FilterTargets(handle, bodies).ToList();

        Assert.Single(filtered);
        Assert.Equal(enemy, filtered[0].Owner);
    }

    [Fact]
    public void SameHitGroup_SharesHitHistory()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target = new MockDamageReceiver("Target");

        var info1 = new MockAttackInfo
        {
            Attacker = attacker,
            HitGroup = 100,
            HittableCount = 1
        };
        var info2 = new MockAttackInfo
        {
            Attacker = attacker,
            HitGroup = 100,  // 同じHitGroup
            HittableCount = 1
        };

        var handle1 = manager.CreateAttack(info1);
        var handle2 = manager.CreateAttack(info2);
        var body = CreateBody(target);

        var result1 = manager.AttackTo(handle1, body);
        var result2 = manager.AttackTo(handle2, body);

        Assert.True(result1.IsSuccess);
        Assert.Equal(AttackResultStatus.HitLimitReached, result2.Status);
    }

    [Fact]
    public void DifferentHitGroup_HasSeparateHitHistory()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target = new MockDamageReceiver("Target");

        var info1 = new MockAttackInfo
        {
            Attacker = attacker,
            HitGroup = 100,
            HittableCount = 1
        };
        var info2 = new MockAttackInfo
        {
            Attacker = attacker,
            HitGroup = 200,  // 異なるHitGroup
            HittableCount = 1
        };

        var handle1 = manager.CreateAttack(info1);
        var handle2 = manager.CreateAttack(info2);
        var body = CreateBody(target);

        var result1 = manager.AttackTo(handle1, body);
        var result2 = manager.AttackTo(handle2, body);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }
}
