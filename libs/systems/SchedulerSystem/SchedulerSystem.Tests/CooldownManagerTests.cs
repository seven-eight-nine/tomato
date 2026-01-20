using Xunit;

namespace Tomato.SchedulerSystem.Tests;

/// <summary>
/// CooldownManager テスト
/// </summary>
public class CooldownManagerTests
{
    [Fact]
    public void Constructor_ShouldInitializeAtFrameZero()
    {
        var manager = new CooldownManager();
        Assert.Equal(0, manager.CurrentFrame);
    }

    [Fact]
    public void Update_ShouldIncrementFrame()
    {
        var manager = new CooldownManager();
        manager.Update();
        Assert.Equal(1, manager.CurrentFrame);
    }

    [Fact]
    public void StartCooldown_ShouldMakeKeyOnCooldown()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 10);
        Assert.True(manager.IsOnCooldown("attack"));
    }

    [Fact]
    public void IsOnCooldown_UnknownKey_ShouldReturnFalse()
    {
        var manager = new CooldownManager();
        Assert.False(manager.IsOnCooldown("unknown"));
    }

    [Fact]
    public void IsOnCooldown_ShouldExpireAfterDuration()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 3);

        Assert.True(manager.IsOnCooldown("attack"));
        manager.Update(); // Frame 1
        Assert.True(manager.IsOnCooldown("attack"));
        manager.Update(); // Frame 2
        Assert.True(manager.IsOnCooldown("attack"));
        manager.Update(); // Frame 3
        Assert.False(manager.IsOnCooldown("attack"));
    }

    [Fact]
    public void GetRemainingFrames_ShouldReturnCorrectValue()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 5);

        Assert.Equal(5, manager.GetRemainingFrames("attack"));
        manager.Update();
        Assert.Equal(4, manager.GetRemainingFrames("attack"));
    }

    [Fact]
    public void GetRemainingFrames_UnknownKey_ShouldReturnZero()
    {
        var manager = new CooldownManager();
        Assert.Equal(0, manager.GetRemainingFrames("unknown"));
    }

    [Fact]
    public void GetRemainingFrames_Expired_ShouldReturnZero()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 1);
        manager.Update();
        Assert.Equal(0, manager.GetRemainingFrames("attack"));
    }

    [Fact]
    public void Reset_ShouldClearCooldown()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 10);
        Assert.True(manager.IsOnCooldown("attack"));

        manager.Reset("attack");
        Assert.False(manager.IsOnCooldown("attack"));
    }

    [Fact]
    public void StartCooldown_SameKey_ShouldOverwrite()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 5);
        manager.Update();
        manager.Update();
        Assert.Equal(3, manager.GetRemainingFrames("attack"));

        manager.StartCooldown("attack", 10);
        Assert.Equal(10, manager.GetRemainingFrames("attack"));
    }

    [Fact]
    public void Clear_ShouldRemoveAllCooldowns()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 10);
        manager.StartCooldown("skill", 20);

        manager.Clear();

        Assert.False(manager.IsOnCooldown("attack"));
        Assert.False(manager.IsOnCooldown("skill"));
    }

    [Fact]
    public void Cleanup_ShouldRemoveExpiredCooldowns()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("short", 2);
        manager.StartCooldown("long", 10);

        manager.Update();
        manager.Update();
        manager.Cleanup();

        // "short" は期限切れで削除される
        // "long" は残る
        Assert.False(manager.IsOnCooldown("short"));
        Assert.True(manager.IsOnCooldown("long"));
    }

    [Fact]
    public void MultipleCooldowns_ShouldBeIndependent()
    {
        var manager = new CooldownManager();
        manager.StartCooldown("attack", 3);
        manager.StartCooldown("skill", 5);

        manager.Update();
        manager.Update();
        manager.Update();

        Assert.False(manager.IsOnCooldown("attack"));
        Assert.True(manager.IsOnCooldown("skill"));
    }
}
