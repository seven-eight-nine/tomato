using System;
using Xunit;

namespace Tomato.EntityHandleSystem.Tests.Attributes;

/// <summary>
/// EntityMethodAttribute comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class EntityMethodAttributeTests
{
    #region Attribute Usage Tests

    [Fact]
    public void EntityMethodAttribute_ShouldHaveCorrectUsage()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(EntityMethodAttribute), typeof(AttributeUsageAttribute))!;

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
    }

    [Fact]
    public void EntityMethodAttribute_ShouldBeCreatable()
    {
        var attr = new EntityMethodAttribute();
        Assert.NotNull(attr);
    }

    [Fact]
    public void EntityMethodAttribute_ShouldInheritFromAttribute()
    {
        Assert.True(typeof(EntityMethodAttribute).IsSubclassOf(typeof(Attribute)));
    }

    #endregion

    #region Unsafe Property Tests

    [Fact]
    public void Unsafe_Default_ShouldBeFalse()
    {
        var attr = new EntityMethodAttribute();

        Assert.False(attr.Unsafe);
    }

    [Fact]
    public void Unsafe_CanBeSetToTrue()
    {
        var attr = new EntityMethodAttribute { Unsafe = true };

        Assert.True(attr.Unsafe);
    }

    [Fact]
    public void Unsafe_CanBeSetToFalse()
    {
        var attr = new EntityMethodAttribute { Unsafe = false };

        Assert.False(attr.Unsafe);
    }

    #endregion

    #region Multiple Instance Tests

    [Fact]
    public void MultipleInstances_ShouldBeIndependent()
    {
        var attr1 = new EntityMethodAttribute { Unsafe = true };
        var attr2 = new EntityMethodAttribute { Unsafe = false };

        Assert.True(attr1.Unsafe);
        Assert.False(attr2.Unsafe);
    }

    [Fact]
    public void DefaultInstance_ShouldNotAffectOthers()
    {
        var defaultAttr = new EntityMethodAttribute();
        var modifiedAttr = new EntityMethodAttribute { Unsafe = true };

        Assert.False(defaultAttr.Unsafe);
        Assert.True(modifiedAttr.Unsafe);
    }

    #endregion
}
