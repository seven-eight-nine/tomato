using System;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// CommandPoolConfig comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class CommandPoolConfigTests
{
    #region Default Value Tests

    [Fact]
    public void InitialCapacity_Default_ShouldBeEight()
    {
        var value = CommandPoolConfig<DefaultConfigCommand>.InitialCapacity;

        Assert.Equal(8, value);
    }

    [Fact]
    public void InitialCapacity_ShouldBeReadable()
    {
        var exception = Record.Exception(() =>
        {
            var _ = CommandPoolConfig<DefaultConfigCommand>.InitialCapacity;
        });

        Assert.Null(exception);
    }

    #endregion

    #region Set Value Tests

    [Fact]
    public void InitialCapacity_SetValue_ShouldWork()
    {
        CommandPoolConfig<SetValueCommand>.InitialCapacity = 16;

        Assert.Equal(16, CommandPoolConfig<SetValueCommand>.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_SetToZero_ShouldWork()
    {
        CommandPoolConfig<ZeroCapacityCommand>.InitialCapacity = 0;

        Assert.Equal(0, CommandPoolConfig<ZeroCapacityCommand>.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_SetToLargeValue_ShouldWork()
    {
        CommandPoolConfig<LargeCapacityCommand>.InitialCapacity = 10000;

        Assert.Equal(10000, CommandPoolConfig<LargeCapacityCommand>.InitialCapacity);
    }

    #endregion

    #region Type Independence Tests

    [Fact]
    public void InitialCapacity_DifferentTypes_ShouldBeIndependent()
    {
        CommandPoolConfig<TypeA>.InitialCapacity = 100;
        CommandPoolConfig<TypeB>.InitialCapacity = 200;

        Assert.Equal(100, CommandPoolConfig<TypeA>.InitialCapacity);
        Assert.Equal(200, CommandPoolConfig<TypeB>.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_SetOneType_ShouldNotAffectOther()
    {
        var originalA = CommandPoolConfig<TypeC>.InitialCapacity;
        CommandPoolConfig<TypeD>.InitialCapacity = 500;

        Assert.Equal(originalA, CommandPoolConfig<TypeC>.InitialCapacity);
        Assert.Equal(500, CommandPoolConfig<TypeD>.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_MultipleTypes_ShouldAllBeConfigurable()
    {
        CommandPoolConfig<TypeE>.InitialCapacity = 1;
        CommandPoolConfig<TypeF>.InitialCapacity = 2;
        CommandPoolConfig<TypeG>.InitialCapacity = 3;

        Assert.Equal(1, CommandPoolConfig<TypeE>.InitialCapacity);
        Assert.Equal(2, CommandPoolConfig<TypeF>.InitialCapacity);
        Assert.Equal(3, CommandPoolConfig<TypeG>.InitialCapacity);
    }

    #endregion

    #region Persistence Tests

    [Fact]
    public void InitialCapacity_SetValue_ShouldPersist()
    {
        CommandPoolConfig<PersistCommand>.InitialCapacity = 42;

        var value1 = CommandPoolConfig<PersistCommand>.InitialCapacity;
        var value2 = CommandPoolConfig<PersistCommand>.InitialCapacity;

        Assert.Equal(42, value1);
        Assert.Equal(42, value2);
    }

    [Fact]
    public void InitialCapacity_SetMultipleTimes_ShouldUseLatestValue()
    {
        CommandPoolConfig<MultiSetCommand>.InitialCapacity = 10;
        CommandPoolConfig<MultiSetCommand>.InitialCapacity = 20;
        CommandPoolConfig<MultiSetCommand>.InitialCapacity = 30;

        Assert.Equal(30, CommandPoolConfig<MultiSetCommand>.InitialCapacity);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void InitialCapacity_NegativeValue_ShouldBeAllowed()
    {
        // While not recommended, the type system allows negative values
        CommandPoolConfig<NegativeCommand>.InitialCapacity = -1;

        Assert.Equal(-1, CommandPoolConfig<NegativeCommand>.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_MaxIntValue_ShouldWork()
    {
        CommandPoolConfig<MaxIntCommand>.InitialCapacity = int.MaxValue;

        Assert.Equal(int.MaxValue, CommandPoolConfig<MaxIntCommand>.InitialCapacity);
    }

    [Fact]
    public void InitialCapacity_MinIntValue_ShouldWork()
    {
        CommandPoolConfig<MinIntCommand>.InitialCapacity = int.MinValue;

        Assert.Equal(int.MinValue, CommandPoolConfig<MinIntCommand>.InitialCapacity);
    }

    #endregion

    #region Type Constraint Tests

    [Fact]
    public void CommandPoolConfig_ShouldAcceptClassTypes()
    {
        // CommandPoolConfig<T> requires T : class
        var _ = CommandPoolConfig<ClassCommand>.InitialCapacity;

        Assert.True(true); // Just verify it compiles and runs
    }

    [Fact]
    public void CommandPoolConfig_ShouldAcceptInterfaceImplementors()
    {
        var _ = CommandPoolConfig<InterfaceImplementor>.InitialCapacity;

        Assert.True(true);
    }

    #endregion

    #region Helper Classes

    private class DefaultConfigCommand { }
    private class SetValueCommand { }
    private class ZeroCapacityCommand { }
    private class LargeCapacityCommand { }
    private class TypeA { }
    private class TypeB { }
    private class TypeC { }
    private class TypeD { }
    private class TypeE { }
    private class TypeF { }
    private class TypeG { }
    private class PersistCommand { }
    private class MultiSetCommand { }
    private class NegativeCommand { }
    private class MaxIntCommand { }
    private class MinIntCommand { }
    private class ClassCommand { }

    private interface ITestInterface { }
    private class InterfaceImplementor : ITestInterface { }

    #endregion
}
