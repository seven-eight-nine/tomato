using Tomato.CombatSystem.Tests.Mocks;
using Xunit;

namespace Tomato.CombatSystem.Tests;

public class AttackHandleTests
{
    private static DamageBody CreateBody(IDamageReceiver owner, int priority = 1)
    {
        var body = new DamageBody { Priority = priority };
        body.BindOwner(owner);
        return body;
    }

    [Fact]
    public void Invalid_IsNotValid()
    {
        var handle = AttackHandle.Invalid;

        Assert.False(handle.IsValid);
    }

    [Fact]
    public void CreatedHandle_IsValid()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();

        var handle = manager.CreateAttack(info);

        Assert.True(handle.IsValid);
    }

    [Fact]
    public void DisposedHandle_IsNotValid()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle = manager.CreateAttack(info);

        handle.Dispose();

        Assert.False(handle.IsValid);
    }

    [Fact]
    public void TryGetInfo_ValidHandle_ReturnsTrue()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle = manager.CreateAttack(info);

        var success = handle.TryGetInfo(out var result);

        Assert.True(success);
        Assert.Same(info, result);
    }

    [Fact]
    public void TryGetInfo_DisposedHandle_ReturnsFalse()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle = manager.CreateAttack(info);
        handle.Dispose();

        var success = handle.TryGetInfo(out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryCanAttack_ValidHandle_ReturnsTrue()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle = manager.CreateAttack(info);

        var success = handle.TryCanAttack(out var canAttack);

        Assert.True(success);
        Assert.True(canAttack);
    }

    [Fact]
    public void TryCanAttack_AttackLimitReached_ReturnsFalse()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target = new MockDamageReceiver("Target");
        var info = new MockAttackInfo
        {
            Attacker = attacker,
            AttackableCount = 1
        };
        var handle = manager.CreateAttack(info);
        var body = CreateBody(target);

        manager.AttackTo(handle, body);
        var success = handle.TryCanAttack(out var canAttack);

        Assert.True(success);
        Assert.False(canAttack);
    }

    [Fact]
    public void TryGetHitCount_TracksHits()
    {
        var manager = new CombatManager();
        var attacker = new MockDamageReceiver("Attacker");
        var target1 = new MockDamageReceiver("Target1");
        var target2 = new MockDamageReceiver("Target2");
        var info = new MockAttackInfo { Attacker = attacker };
        var handle = manager.CreateAttack(info);
        var body1 = CreateBody(target1);
        var body2 = CreateBody(target2);

        handle.TryGetHitCount(out var count0);
        manager.AttackTo(handle, body1);
        handle.TryGetHitCount(out var count1);
        manager.AttackTo(handle, body2);
        handle.TryGetHitCount(out var count2);

        Assert.Equal(0, count0);
        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
    }

    [Fact]
    public void TryUpdateTime_UpdatesElapsedTime()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle = manager.CreateAttack(info);

        handle.TryGetElapsedTime(out var time0);
        handle.TryUpdateTime(1.5f);
        handle.TryGetElapsedTime(out var time1);
        handle.TryUpdateTime(0.5f);
        handle.TryGetElapsedTime(out var time2);

        Assert.Equal(0f, time0);
        Assert.Equal(1.5f, time1);
        Assert.Equal(2.0f, time2);
    }

    [Fact]
    public void Equality_SameHandle_AreEqual()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle1 = manager.CreateAttack(info);
        var handle2 = handle1;

        Assert.Equal(handle1, handle2);
        Assert.True(handle1 == handle2);
    }

    [Fact]
    public void Equality_DifferentHandles_AreNotEqual()
    {
        var manager = new CombatManager();
        var info = new MockAttackInfo();
        var handle1 = manager.CreateAttack(info);
        var handle2 = manager.CreateAttack(info);

        Assert.NotEqual(handle1, handle2);
        Assert.True(handle1 != handle2);
    }
}
