using System;
using Xunit;

namespace Tomato.HandleSystem.Tests;

public class HandleableAttributeTests
{
    [Fact]
    public void HandleableAttribute_ShouldHaveCorrectUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(HandleableAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
    }

    [Fact]
    public void HandleableAttribute_InitialCapacity_Default_ShouldBe256()
    {
        var attr = new HandleableAttribute();
        Assert.Equal(256, attr.InitialCapacity);
    }

    [Fact]
    public void HandleableAttribute_InitialCapacity_CanBeSet()
    {
        var attr = new HandleableAttribute { InitialCapacity = 512 };
        Assert.Equal(512, attr.InitialCapacity);
    }

    [Fact]
    public void HandleableAttribute_ArenaName_Default_ShouldBeNull()
    {
        var attr = new HandleableAttribute();
        Assert.Null(attr.ArenaName);
    }

    [Fact]
    public void HandleableAttribute_ArenaName_CanBeSet()
    {
        var attr = new HandleableAttribute { ArenaName = "CustomArena" };
        Assert.Equal("CustomArena", attr.ArenaName);
    }

    [Fact]
    public void HandleableMethodAttribute_ShouldHaveCorrectUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(HandleableMethodAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
    }

    [Fact]
    public void HandleableMethodAttribute_Unsafe_DefaultIsFalse()
    {
        var attr = new HandleableMethodAttribute();
        Assert.False(attr.Unsafe);
    }

    [Fact]
    public void HandleableMethodAttribute_Unsafe_CanBeSet()
    {
        var attr = new HandleableMethodAttribute { Unsafe = true };
        Assert.True(attr.Unsafe);
    }
}
