using System;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Attributes;

/// <summary>
/// Command attribute tests - t-wada style with 3x coverage
/// </summary>
public class CommandAttributeTests
{
    #region CommandQueueAttribute Tests

    [Fact]
    public void CommandQueueAttribute_ShouldHaveCorrectUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(CommandQueueAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void CommandQueueAttribute_ShouldBeCreatable()
    {
        var attr = new CommandQueueAttribute();
        Assert.NotNull(attr);
    }

    #endregion

    #region CommandMethodAttribute Tests

    [Fact]
    public void CommandMethodAttribute_ShouldHaveCorrectUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(CommandMethodAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void CommandMethodAttribute_DefaultClear_ShouldBeTrue()
    {
        var attr = new CommandMethodAttribute();
        Assert.True(attr.Clear);
    }

    [Fact]
    public void CommandMethodAttribute_WithClearFalse_ShouldBeFalse()
    {
        var attr = new CommandMethodAttribute(false);
        Assert.False(attr.Clear);
    }

    [Fact]
    public void CommandMethodAttribute_WithClearTrue_ShouldBeTrue()
    {
        var attr = new CommandMethodAttribute(true);
        Assert.True(attr.Clear);
    }

    #endregion

    #region CommandAttribute<T> Tests

    [Fact]
    public void CommandAttribute_ShouldHaveCorrectUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(CommandAttribute<>), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.True(usage.AllowMultiple);  // Can register to multiple queues
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void CommandAttribute_DefaultPriority_ShouldBeZero()
    {
        var attr = new CommandAttribute<TestQueue>();
        Assert.Equal(0, attr.Priority);
    }

    [Fact]
    public void CommandAttribute_DefaultPoolInitialCapacity_ShouldBeEight()
    {
        var attr = new CommandAttribute<TestQueue>();
        Assert.Equal(8, attr.PoolInitialCapacity);
    }

    [Fact]
    public void CommandAttribute_SetPriority_ShouldWork()
    {
        var attr = new CommandAttribute<TestQueue> { Priority = 100 };
        Assert.Equal(100, attr.Priority);
    }

    [Fact]
    public void CommandAttribute_SetPoolInitialCapacity_ShouldWork()
    {
        var attr = new CommandAttribute<TestQueue> { PoolInitialCapacity = 32 };
        Assert.Equal(32, attr.PoolInitialCapacity);
    }

    [Fact]
    public void CommandAttribute_NegativePriority_ShouldWork()
    {
        var attr = new CommandAttribute<TestQueue> { Priority = -10 };
        Assert.Equal(-10, attr.Priority);
    }

    #endregion

    #region Helper Classes

    private class TestQueue { }

    #endregion
}
