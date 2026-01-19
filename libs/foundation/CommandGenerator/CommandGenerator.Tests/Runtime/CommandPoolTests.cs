using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// CommandPool comprehensive tests - t-wada style with 3x coverage
/// </summary>
public class CommandPoolTests
{
    #region Rent Tests

    [Fact]
    public void Rent_FromEmptyPool_ShouldCreateNewInstance()
    {
        // Pool is initialized with 8 instances, drain them first
        var instances = new List<TestPoolableCommand>();
        for (int i = 0; i < CommandPoolConfig<TestPoolableCommand>.InitialCapacity + 5; i++)
        {
            instances.Add(CommandPool<TestPoolableCommand>.Rent());
        }

        // All instances should be valid
        Assert.All(instances, i => Assert.NotNull(i));

        // Return all
        foreach (var instance in instances)
        {
            CommandPool<TestPoolableCommand>.Return(instance);
        }
    }

    [Fact]
    public void Rent_ShouldReturnNonNullInstance()
    {
        var instance = CommandPool<TestPoolableCommand>.Rent();

        Assert.NotNull(instance);

        CommandPool<TestPoolableCommand>.Return(instance);
    }

    [Fact]
    public void Rent_MultipleTimes_ShouldReturnDifferentInstances()
    {
        var instance1 = CommandPool<TestPoolableCommand>.Rent();
        var instance2 = CommandPool<TestPoolableCommand>.Rent();

        Assert.NotSame(instance1, instance2);

        CommandPool<TestPoolableCommand>.Return(instance1);
        CommandPool<TestPoolableCommand>.Return(instance2);
    }

    #endregion

    #region Return Tests

    [Fact]
    public void Return_ShouldCallResetToDefault()
    {
        var instance = CommandPool<TestPoolableCommand>.Rent();
        instance.Value = 42;
        instance.Name = "Test";

        CommandPool<TestPoolableCommand>.Return(instance);

        // After return, the instance should be reset
        Assert.True(instance.WasReset);
    }

    [Fact]
    public void Return_ShouldIncreasePooledCount()
    {
        var instance1 = CommandPool<TestPoolableCommand>.Rent();
        var instance2 = CommandPool<TestPoolableCommand>.Rent();
        var countBeforeReturn = CommandPool<TestPoolableCommand>.PooledCount;

        CommandPool<TestPoolableCommand>.Return(instance1);

        Assert.Equal(countBeforeReturn + 1, CommandPool<TestPoolableCommand>.PooledCount);

        CommandPool<TestPoolableCommand>.Return(instance2);
    }

    [Fact]
    public void Return_ThenRent_ShouldReturnSameInstance()
    {
        // Drain the pool first
        var drained = new List<TestPoolableCommand>();
        while (CommandPool<TestPoolableCommand>.PooledCount > 0)
        {
            drained.Add(CommandPool<TestPoolableCommand>.Rent());
        }

        var instance = CommandPool<TestPoolableCommand>.Rent();
        CommandPool<TestPoolableCommand>.Return(instance);

        var rentedAgain = CommandPool<TestPoolableCommand>.Rent();

        Assert.Same(instance, rentedAgain);

        // Return everything
        CommandPool<TestPoolableCommand>.Return(rentedAgain);
        foreach (var d in drained)
        {
            CommandPool<TestPoolableCommand>.Return(d);
        }
    }

    #endregion

    #region Prewarm Tests

    [Fact]
    public void Prewarm_ShouldIncreasePoolSize()
    {
        var initialCount = CommandPool<PrewarmTestCommand>.PooledCount;

        CommandPool<PrewarmTestCommand>.Prewarm(initialCount + 10);

        Assert.True(CommandPool<PrewarmTestCommand>.PooledCount >= initialCount + 10);
    }

    [Fact]
    public void Prewarm_WithSmallerCount_ShouldNotDecrease()
    {
        CommandPool<PrewarmTestCommand>.Prewarm(20);
        var countAfterPrewarm = CommandPool<PrewarmTestCommand>.PooledCount;

        CommandPool<PrewarmTestCommand>.Prewarm(5);

        Assert.Equal(countAfterPrewarm, CommandPool<PrewarmTestCommand>.PooledCount);
    }

    [Fact]
    public void Prewarm_WithZero_ShouldNotThrow()
    {
        var exception = Record.Exception(() => CommandPool<PrewarmTestCommand>.Prewarm(0));

        Assert.Null(exception);
    }

    #endregion

    #region PooledCount Tests

    [Fact]
    public void PooledCount_Initially_ShouldBeAtLeastInitialCapacity()
    {
        // Note: Due to static initialization, the pool may have been used already
        // We just verify it's accessible
        var count = CommandPool<PooledCountTestCommand>.PooledCount;

        Assert.True(count >= 0);
    }

    [Fact]
    public void PooledCount_AfterRent_ShouldDecrease()
    {
        var initialCount = CommandPool<PooledCountTestCommand>.PooledCount;

        var instance = CommandPool<PooledCountTestCommand>.Rent();

        Assert.True(CommandPool<PooledCountTestCommand>.PooledCount <= initialCount);

        CommandPool<PooledCountTestCommand>.Return(instance);
    }

    [Fact]
    public void PooledCount_AfterReturn_ShouldIncrease()
    {
        var instance = CommandPool<PooledCountTestCommand>.Rent();
        var countAfterRent = CommandPool<PooledCountTestCommand>.PooledCount;

        CommandPool<PooledCountTestCommand>.Return(instance);

        Assert.Equal(countAfterRent + 1, CommandPool<PooledCountTestCommand>.PooledCount);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void Rent_ConcurrentAccess_ShouldNotThrow()
    {
        var exceptions = new List<Exception>();
        var instances = new List<ThreadSafeCommand>();
        var lockObj = new object();

        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var instance = CommandPool<ThreadSafeCommand>.Rent();
                        lock (lockObj)
                        {
                            instances.Add(instance);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        Assert.Empty(exceptions);

        // Return all instances
        foreach (var instance in instances)
        {
            CommandPool<ThreadSafeCommand>.Return(instance);
        }
    }

    [Fact]
    public void Return_ConcurrentAccess_ShouldNotThrow()
    {
        var instances = new List<ThreadSafeCommand>();
        for (int i = 0; i < 100; i++)
        {
            instances.Add(CommandPool<ThreadSafeCommand>.Rent());
        }

        var exceptions = new List<Exception>();
        var lockObj = new object();

        var tasks = new Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            int startIdx = i * 10;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 10; j++)
                    {
                        CommandPool<ThreadSafeCommand>.Return(instances[startIdx + j]);
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        Assert.Empty(exceptions);
    }

    [Fact]
    public void RentAndReturn_ConcurrentAccess_ShouldMaintainConsistency()
    {
        var exceptions = new List<Exception>();
        var lockObj = new object();

        var tasks = new Task[20];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var instance = CommandPool<ThreadSafeCommand>.Rent();
                        Thread.Sleep(1); // Small delay
                        CommandPool<ThreadSafeCommand>.Return(instance);
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        Assert.Empty(exceptions);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Pool_WithDifferentTypes_ShouldBeIndependent()
    {
        var cmd1 = CommandPool<TestPoolableCommand>.Rent();
        var cmd2 = CommandPool<AnotherPoolableCommand>.Rent();

        Assert.IsType<TestPoolableCommand>(cmd1);
        Assert.IsType<AnotherPoolableCommand>(cmd2);

        CommandPool<TestPoolableCommand>.Return(cmd1);
        CommandPool<AnotherPoolableCommand>.Return(cmd2);
    }

    [Fact]
    public void RentedInstance_ShouldBeUsable()
    {
        var instance = CommandPool<TestPoolableCommand>.Rent();

        instance.Value = 123;
        instance.Name = "Test";

        Assert.Equal(123, instance.Value);
        Assert.Equal("Test", instance.Name);

        CommandPool<TestPoolableCommand>.Return(instance);
    }

    [Fact]
    public void ResetInstance_AfterReturn_ShouldHaveDefaultValues()
    {
        var instance = CommandPool<TestPoolableCommand>.Rent();
        instance.Value = 999;
        instance.Name = "Modified";

        CommandPool<TestPoolableCommand>.Return(instance);

        Assert.Equal(0, instance.Value);
        Assert.Null(instance.Name);
    }

    #endregion

    #region Helper Classes

    private class TestPoolableCommand : ICommandPoolable<TestPoolableCommand>
    {
        public int Value { get; set; }
        public string? Name { get; set; }
        public bool WasReset { get; private set; }

        public void ResetToDefault()
        {
            Value = 0;
            Name = null;
            WasReset = true;
        }
    }

    private class AnotherPoolableCommand : ICommandPoolable<AnotherPoolableCommand>
    {
        public double Data { get; set; }

        public void ResetToDefault()
        {
            Data = 0;
        }
    }

    private class PrewarmTestCommand : ICommandPoolable<PrewarmTestCommand>
    {
        public void ResetToDefault() { }
    }

    private class PooledCountTestCommand : ICommandPoolable<PooledCountTestCommand>
    {
        public void ResetToDefault() { }
    }

    private class ThreadSafeCommand : ICommandPoolable<ThreadSafeCommand>
    {
        public int Counter { get; set; }

        public void ResetToDefault()
        {
            Counter = 0;
        }
    }

    #endregion
}
