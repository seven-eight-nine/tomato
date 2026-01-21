using Tomato.CombatSystem.Tests.Mocks;
using Xunit;

namespace Tomato.CombatSystem.Tests;

public class HitHistoryTests
{
    [Fact]
    public void CanHit_NoHistory_ReturnsTrue()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");

        var result = history.CanHit(1, target, 0f, 0);

        Assert.True(result);
    }

    [Fact]
    public void CanHit_WithinLimit_ReturnsTrue()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");
        history.RecordHit(1, target);

        var result = history.CanHit(1, target, 0f, 2);  // limit=2

        Assert.True(result);
    }

    [Fact]
    public void CanHit_AtLimit_ReturnsFalse()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");
        history.RecordHit(1, target);
        history.RecordHit(1, target);

        var result = history.CanHit(1, target, 0f, 2);  // limit=2

        Assert.False(result);
    }

    [Fact]
    public void CanHit_UnlimitedCount_AlwaysReturnsTrue()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");

        for (int i = 0; i < 100; i++)
        {
            history.RecordHit(1, target);
        }

        var result = history.CanHit(1, target, 0f, 0);  // limit=0 (unlimited)

        Assert.True(result);
    }

    [Fact]
    public void CanHit_DifferentTargets_TrackedSeparately()
    {
        var history = new HitHistory();
        var target1 = new MockDamageReceiver("Target1");
        var target2 = new MockDamageReceiver("Target2");
        history.RecordHit(1, target1);

        var result1 = history.CanHit(1, target1, 0f, 1);
        var result2 = history.CanHit(1, target2, 0f, 1);

        Assert.False(result1);  // target1は上限到達
        Assert.True(result2);   // target2は未ヒット
    }

    [Fact]
    public void CanHit_DifferentHitGroups_TrackedSeparately()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");
        history.RecordHit(1, target);

        var result1 = history.CanHit(1, target, 0f, 1);
        var result2 = history.CanHit(2, target, 0f, 1);

        Assert.False(result1);  // group1は上限到達
        Assert.True(result2);   // group2は未ヒット
    }

    [Fact]
    public void CanHit_IntervalNotElapsed_ReturnsFalse()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");
        history.RecordHit(1, target);

        var result = history.CanHit(1, target, 1.0f, 0);  // interval=1秒

        Assert.False(result);  // 時間が経過していない
    }

    [Fact]
    public void CanHit_IntervalElapsed_ReturnsTrue()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");
        history.RecordHit(1, target);
        history.Update(2.0f);  // 2秒経過

        var result = history.CanHit(1, target, 1.0f, 0);  // interval=1秒

        Assert.True(result);
    }

    [Fact]
    public void Clear_RemovesAllHistory()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");
        history.RecordHit(1, target);

        history.Clear();

        var result = history.CanHit(1, target, 0f, 1);
        Assert.True(result);
    }

    [Fact]
    public void ClearHitGroup_RemovesOnlyThatGroup()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");
        history.RecordHit(1, target);
        history.RecordHit(2, target);

        history.ClearHitGroup(1);

        var result1 = history.CanHit(1, target, 0f, 1);
        var result2 = history.CanHit(2, target, 0f, 1);
        Assert.True(result1);   // group1はクリアされた
        Assert.False(result2);  // group2は残っている
    }

    [Fact]
    public void Update_AdvancesTimeForAllEntries()
    {
        var history = new HitHistory();
        var target1 = new MockDamageReceiver("Target1");
        var target2 = new MockDamageReceiver("Target2");
        history.RecordHit(1, target1);
        history.RecordHit(1, target2);

        history.Update(5.0f);

        // 両方とも時間が経過している
        Assert.True(history.CanHit(1, target1, 1.0f, 0));
        Assert.True(history.CanHit(1, target2, 1.0f, 0));
    }

    [Fact]
    public void GetHitCount_ReturnsCorrectCount()
    {
        var history = new HitHistory();
        var target = new MockDamageReceiver("Target");

        Assert.Equal(0, history.GetHitCount(1, target));

        history.RecordHit(1, target);
        Assert.Equal(1, history.GetHitCount(1, target));

        history.RecordHit(1, target);
        Assert.Equal(2, history.GetHitCount(1, target));
    }
}
