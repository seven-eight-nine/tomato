using System;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Attributes;

/// <summary>
/// EntityAttribute comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class EntityAttributeTests
{
    #region Attribute Usage Tests

    [Fact]
    public void EntityAttribute_ShouldHaveCorrectUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(EntityAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
    }

    [Fact]
    public void EntityAttribute_ShouldBeCreatable()
    {
        var attr = new EntityAttribute();
        Assert.NotNull(attr);
    }

    [Fact]
    public void EntityAttribute_ShouldInheritFromAttribute()
    {
        Assert.True(typeof(EntityAttribute).IsSubclassOf(typeof(Attribute)));
    }

    #endregion

    #region InitialCapacity Property Tests

    [Fact]
    public void InitialCapacity_Default_ShouldBe256()
    {
        var attr = new EntityAttribute();

        Assert.Equal(256, attr.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_CanBeSet()
    {
        var attr = new EntityAttribute { InitialCapacity = 512 };

        Assert.Equal(512, attr.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_CanBeSmall()
    {
        var attr = new EntityAttribute { InitialCapacity = 1 };

        Assert.Equal(1, attr.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_CanBeLarge()
    {
        var attr = new EntityAttribute { InitialCapacity = 10000 };

        Assert.Equal(10000, attr.InitialCapacity);
    }

    #endregion

    #region ArenaName Property Tests

    [Fact]
    public void ArenaName_Default_ShouldBeNull()
    {
        var attr = new EntityAttribute();

        Assert.Null(attr.ArenaName);
    }

    [Fact]
    public void ArenaName_CanBeSet()
    {
        var attr = new EntityAttribute { ArenaName = "CustomArena" };

        Assert.Equal("CustomArena", attr.ArenaName);
    }

    [Fact]
    public void ArenaName_CanBeEmptyString()
    {
        var attr = new EntityAttribute { ArenaName = "" };

        Assert.Equal("", attr.ArenaName);
    }

    #endregion

    #region Combined Property Tests

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        var attr = new EntityAttribute
        {
            InitialCapacity = 128,
            ArenaName = "MyArena"
        };

        Assert.Equal(128, attr.InitialCapacity);
        Assert.Equal("MyArena", attr.ArenaName);
    }

    #endregion
}
