using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// ICommandPoolable interface tests - t-wada style with 3x coverage
/// </summary>
public class ICommandPoolableTests
{
    #region Interface Definition Tests

    [Fact]
    public void ICommandPoolable_ShouldBeInterface()
    {
        var type = typeof(ICommandPoolable<>);

        Assert.True(type.IsInterface);
    }

    [Fact]
    public void ICommandPoolable_ShouldHaveGenericParameter()
    {
        var type = typeof(ICommandPoolable<>);

        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
    }

    [Fact]
    public void ICommandPoolable_GenericConstraint_ShouldBeClass()
    {
        var type = typeof(ICommandPoolable<>);
        var genericParam = type.GetGenericArguments()[0];
        var constraints = genericParam.GetGenericParameterConstraints();

        // The constraint is "where TSelf : class"
        Assert.True(genericParam.GenericParameterAttributes.HasFlag(
            System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint));
    }

    #endregion

    #region ResetToDefault Method Tests

    [Fact]
    public void ResetToDefault_ShouldExistOnInterface()
    {
        var method = typeof(ICommandPoolable<TestCommand>).GetMethod("ResetToDefault");

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void ResetToDefault_Implementation_ShouldResetFields()
    {
        var command = new TestCommand
        {
            IntValue = 42,
            StringValue = "test",
            BoolValue = true,
            FloatValue = 3.14f
        };

        command.ResetToDefault();

        Assert.Equal(0, command.IntValue);
        Assert.Null(command.StringValue);
        Assert.False(command.BoolValue);
        Assert.Equal(0f, command.FloatValue);
    }

    [Fact]
    public void ResetToDefault_Implementation_ShouldBeCallableMultipleTimes()
    {
        var command = new TestCommand { IntValue = 100 };

        command.ResetToDefault();
        command.IntValue = 200;
        command.ResetToDefault();

        Assert.Equal(0, command.IntValue);
    }

    #endregion

    #region Self-Referencing Generic Tests

    [Fact]
    public void ICommandPoolable_ShouldAllowSelfReferencingImplementation()
    {
        var command = new SelfReferencingCommand();

        Assert.IsAssignableFrom<ICommandPoolable<SelfReferencingCommand>>(command);
    }

    [Fact]
    public void ICommandPoolable_SelfReferencing_ShouldResetCorrectly()
    {
        var command = new SelfReferencingCommand { Data = "Hello" };

        command.ResetToDefault();

        Assert.Null(command.Data);
    }

    [Fact]
    public void ICommandPoolable_WithInheritance_ShouldWork()
    {
        var command = new DerivedCommand
        {
            BaseValue = 10,
            DerivedValue = 20
        };

        command.ResetToDefault();

        Assert.Equal(0, command.BaseValue);
        Assert.Equal(0, command.DerivedValue);
    }

    #endregion

    #region Collection Reset Tests

    [Fact]
    public void ResetToDefault_WithList_ShouldClearList()
    {
        var command = new CollectionCommand();
        command.Items.Add(1);
        command.Items.Add(2);
        command.Items.Add(3);

        command.ResetToDefault();

        Assert.Empty(command.Items);
    }

    [Fact]
    public void ResetToDefault_WithDictionary_ShouldClearDictionary()
    {
        var command = new CollectionCommand();
        command.Map["key1"] = "value1";
        command.Map["key2"] = "value2";

        command.ResetToDefault();

        Assert.Empty(command.Map);
    }

    [Fact]
    public void ResetToDefault_WithHashSet_ShouldClearHashSet()
    {
        var command = new CollectionCommand();
        command.UniqueItems.Add("a");
        command.UniqueItems.Add("b");

        command.ResetToDefault();

        Assert.Empty(command.UniqueItems);
    }

    #endregion

    #region Null Reference Tests

    [Fact]
    public void ResetToDefault_WithNullableReference_ShouldSetToNull()
    {
        var command = new NullableCommand
        {
            Reference = new object(),
            NullableInt = 42
        };

        command.ResetToDefault();

        Assert.Null(command.Reference);
        Assert.Null(command.NullableInt);
    }

    [Fact]
    public void ResetToDefault_WithArrayField_ShouldClearOrNull()
    {
        var command = new ArrayCommand
        {
            Data = new int[] { 1, 2, 3, 4, 5 }
        };

        command.ResetToDefault();

        // Depending on implementation, array might be cleared or nulled
        Assert.True(command.Data == null || command.Data.Length == 0 ||
                    command.Data.All(x => x == 0));
    }

    [Fact]
    public void ResetToDefault_WhenAlreadyDefault_ShouldNotThrow()
    {
        var command = new TestCommand();

        var exception = Record.Exception(() => command.ResetToDefault());

        Assert.Null(exception);
    }

    #endregion

    #region Integration with Pool Tests

    [Fact]
    public void ICommandPoolable_WithPool_ShouldResetOnReturn()
    {
        var command = CommandPool<PoolIntegratedCommand>.Rent();
        command.Counter = 100;

        CommandPool<PoolIntegratedCommand>.Return(command);

        Assert.Equal(0, command.Counter);
    }

    [Fact]
    public void ICommandPoolable_WithPool_RentedInstance_ShouldBeReset()
    {
        // First rent and modify
        var command1 = CommandPool<PoolIntegratedCommand>.Rent();
        command1.Counter = 999;
        CommandPool<PoolIntegratedCommand>.Return(command1);

        // Rent again - should get reset instance
        var command2 = CommandPool<PoolIntegratedCommand>.Rent();

        Assert.Equal(0, command2.Counter);

        CommandPool<PoolIntegratedCommand>.Return(command2);
    }

    [Fact]
    public void ICommandPoolable_ResetBehavior_ShouldBeIdempotent()
    {
        var command = new TestCommand { IntValue = 50 };

        command.ResetToDefault();
        var firstReset = command.IntValue;

        command.ResetToDefault();
        var secondReset = command.IntValue;

        Assert.Equal(firstReset, secondReset);
        Assert.Equal(0, firstReset);
    }

    #endregion

    #region Complex Type Tests

    [Fact]
    public void ResetToDefault_WithNestedObjects_ShouldResetCorrectly()
    {
        var command = new NestedCommand
        {
            Name = "Parent",
            Child = new NestedCommand.ChildObject { Value = 42 }
        };

        command.ResetToDefault();

        Assert.Null(command.Name);
        Assert.Null(command.Child);
    }

    [Fact]
    public void ResetToDefault_WithDateTime_ShouldResetToDefault()
    {
        var command = new DateTimeCommand
        {
            Timestamp = DateTime.Now
        };

        command.ResetToDefault();

        Assert.Equal(default(DateTime), command.Timestamp);
    }

    [Fact]
    public void ResetToDefault_WithGuid_ShouldResetToEmpty()
    {
        var command = new GuidCommand
        {
            Id = Guid.NewGuid()
        };

        command.ResetToDefault();

        Assert.Equal(Guid.Empty, command.Id);
    }

    #endregion

    #region Helper Classes

    private class TestCommand : ICommandPoolable<TestCommand>
    {
        public int IntValue { get; set; }
        public string? StringValue { get; set; }
        public bool BoolValue { get; set; }
        public float FloatValue { get; set; }

        public void ResetToDefault()
        {
            IntValue = 0;
            StringValue = null;
            BoolValue = false;
            FloatValue = 0f;
        }
    }

    private class SelfReferencingCommand : ICommandPoolable<SelfReferencingCommand>
    {
        public string? Data { get; set; }

        public void ResetToDefault()
        {
            Data = null;
        }
    }

    private class BaseCommand
    {
        public int BaseValue { get; set; }
    }

    private class DerivedCommand : BaseCommand, ICommandPoolable<DerivedCommand>
    {
        public int DerivedValue { get; set; }

        public void ResetToDefault()
        {
            BaseValue = 0;
            DerivedValue = 0;
        }
    }

    private class CollectionCommand : ICommandPoolable<CollectionCommand>
    {
        public List<int> Items { get; } = new List<int>();
        public Dictionary<string, string> Map { get; } = new Dictionary<string, string>();
        public HashSet<string> UniqueItems { get; } = new HashSet<string>();

        public void ResetToDefault()
        {
            Items.Clear();
            Map.Clear();
            UniqueItems.Clear();
        }
    }

    private class NullableCommand : ICommandPoolable<NullableCommand>
    {
        public object? Reference { get; set; }
        public int? NullableInt { get; set; }

        public void ResetToDefault()
        {
            Reference = null;
            NullableInt = null;
        }
    }

    private class ArrayCommand : ICommandPoolable<ArrayCommand>
    {
        public int[]? Data { get; set; }

        public void ResetToDefault()
        {
            if (Data != null)
            {
                Array.Clear(Data, 0, Data.Length);
            }
        }
    }

    private class PoolIntegratedCommand : ICommandPoolable<PoolIntegratedCommand>
    {
        public int Counter { get; set; }

        public void ResetToDefault()
        {
            Counter = 0;
        }
    }

    private class NestedCommand : ICommandPoolable<NestedCommand>
    {
        public string? Name { get; set; }
        public ChildObject? Child { get; set; }

        public class ChildObject
        {
            public int Value { get; set; }
        }

        public void ResetToDefault()
        {
            Name = null;
            Child = null;
        }
    }

    private class DateTimeCommand : ICommandPoolable<DateTimeCommand>
    {
        public DateTime Timestamp { get; set; }

        public void ResetToDefault()
        {
            Timestamp = default;
        }
    }

    private class GuidCommand : ICommandPoolable<GuidCommand>
    {
        public Guid Id { get; set; }

        public void ResetToDefault()
        {
            Id = Guid.Empty;
        }
    }

    #endregion
}
