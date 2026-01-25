using System;
using System.Collections.Generic;
using Tomato.HierarchicalStateMachine;
using Xunit;

namespace HierarchicalStateMachine.Tests;

public class StateIdTests
{
    [Fact]
    public void Constructor_WithValidValue_CreatesInstance()
    {
        var id = new StateId("test");
        Assert.Equal("test", id.Value);
    }

    [Fact]
    public void Constructor_WithNullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StateId(null!));
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        var id1 = new StateId("test");
        var id2 = new StateId("test");
        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var id1 = new StateId("test1");
        var id2 = new StateId("test2");
        Assert.False(id1.Equals(id2));
        Assert.True(id1 != id2);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        var id1 = new StateId("test");
        var id2 = new StateId("test");
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var id = new StateId("test");
        Assert.Equal("test", id.ToString());
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        StateId id = "test";
        Assert.Equal("test", id.Value);
    }

    [Fact]
    public void ImplicitConversion_ToString_Works()
    {
        var id = new StateId("test");
        string value = id;
        Assert.Equal("test", value);
    }

    [Fact]
    public void Any_HasCorrectValue()
    {
        Assert.Equal("__ANY__", StateId.Any.Value);
    }

    [Fact]
    public void IsAny_ForAnyState_ReturnsTrue()
    {
        Assert.True(StateId.Any.IsAny);
    }

    [Fact]
    public void IsAny_ForNormalState_ReturnsFalse()
    {
        var id = new StateId("normal");
        Assert.False(id.IsAny);
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<StateId, int>();
        var id1 = new StateId("key1");
        var id2 = new StateId("key2");

        dict[id1] = 1;
        dict[id2] = 2;

        Assert.Equal(1, dict[id1]);
        Assert.Equal(2, dict[id2]);
        Assert.Equal(1, dict[new StateId("key1")]);
    }
}
