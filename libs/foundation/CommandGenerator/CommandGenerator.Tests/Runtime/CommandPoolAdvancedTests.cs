using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tomato.CommandGenerator.Tests.Runtime;

/// <summary>
/// Advanced CommandPool tests - t-wada style comprehensive coverage
/// Tests for collection field resets, large pool capacity, concurrent operations,
/// and edge cases like null return and duplicate return
/// </summary>
public class CommandPoolAdvancedTests
{
    #region Command with List Field Reset Tests

    [Fact]
    public void ListField_AfterReturn_ShouldBeEmpty()
    {
        var instance = CommandPool<ListFieldCommand>.Rent();
        instance.Items.Add(1);
        instance.Items.Add(2);
        instance.Items.Add(3);

        CommandPool<ListFieldCommand>.Return(instance);

        Assert.Empty(instance.Items);
    }

    [Fact]
    public void ListField_AfterReturn_ShouldStillBeUsable()
    {
        var instance = CommandPool<ListFieldCommand>.Rent();
        instance.Items.Add(1);
        CommandPool<ListFieldCommand>.Return(instance);

        // Re-rent (may or may not be the same instance)
        var reRented = CommandPool<ListFieldCommand>.Rent();
        reRented.Items.Add(100);

        Assert.Single(reRented.Items);
        Assert.Equal(100, reRented.Items[0]);

        CommandPool<ListFieldCommand>.Return(reRented);
    }

    [Fact]
    public void ListField_MultipleReturns_ShouldResetEachTime()
    {
        var instance = CommandPool<ListFieldCommand>.Rent();

        for (int i = 0; i < 5; i++)
        {
            instance.Items.Add(i);
            instance.Items.Add(i * 10);
            CommandPool<ListFieldCommand>.Return(instance);
            Assert.Empty(instance.Items);
            instance = CommandPool<ListFieldCommand>.Rent();
        }

        CommandPool<ListFieldCommand>.Return(instance);
    }

    #endregion

    #region Command with Dictionary Field Reset Tests

    [Fact]
    public void DictionaryField_AfterReturn_ShouldBeEmpty()
    {
        var instance = CommandPool<DictionaryFieldCommand>.Rent();
        instance.Data["key1"] = "value1";
        instance.Data["key2"] = "value2";

        CommandPool<DictionaryFieldCommand>.Return(instance);

        Assert.Empty(instance.Data);
    }

    [Fact]
    public void DictionaryField_AfterReturn_ShouldStillBeUsable()
    {
        var instance = CommandPool<DictionaryFieldCommand>.Rent();
        instance.Data["old"] = "data";
        CommandPool<DictionaryFieldCommand>.Return(instance);

        var reRented = CommandPool<DictionaryFieldCommand>.Rent();
        reRented.Data["new"] = "value";

        Assert.Single(reRented.Data);
        Assert.True(reRented.Data.ContainsKey("new"));

        CommandPool<DictionaryFieldCommand>.Return(reRented);
    }

    [Fact]
    public void DictionaryField_WithManyEntries_ShouldClearAll()
    {
        var instance = CommandPool<DictionaryFieldCommand>.Rent();

        for (int i = 0; i < 100; i++)
        {
            instance.Data[$"key{i}"] = $"value{i}";
        }

        Assert.Equal(100, instance.Data.Count);

        CommandPool<DictionaryFieldCommand>.Return(instance);

        Assert.Empty(instance.Data);
    }

    #endregion

    #region Very Large Pool Initial Capacity Tests

    [Fact]
    public void LargePool_InitialCapacity1000_ShouldWork()
    {
        // Set initial capacity before first use
        CommandPoolConfig<LargePoolCommand>.InitialCapacity = 1000;

        // Rent 500 instances (should come from pool)
        var instances = new List<LargePoolCommand>();
        for (int i = 0; i < 500; i++)
        {
            instances.Add(CommandPool<LargePoolCommand>.Rent());
        }

        Assert.Equal(500, instances.Count);
        Assert.All(instances, inst => Assert.NotNull(inst));

        // Return all
        foreach (var inst in instances)
        {
            CommandPool<LargePoolCommand>.Return(inst);
        }
    }

    [Fact]
    public void LargePool_RentMoreThanCapacity_ShouldCreateNew()
    {
        CommandPoolConfig<VeryLargePoolCommand>.InitialCapacity = 100;

        var instances = new List<VeryLargePoolCommand>();

        // Rent more than initial capacity
        for (int i = 0; i < 150; i++)
        {
            var instance = CommandPool<VeryLargePoolCommand>.Rent();
            Assert.NotNull(instance);
            instances.Add(instance);
        }

        Assert.Equal(150, instances.Count);

        // Return all
        foreach (var inst in instances)
        {
            CommandPool<VeryLargePoolCommand>.Return(inst);
        }

        // Pool should now have at least 150
        Assert.True(CommandPool<VeryLargePoolCommand>.PooledCount >= 150);
    }

    [Fact]
    public void LargePool_AfterPrewarm_ShouldHaveExpectedCount()
    {
        CommandPool<PrewarmLargeCommand>.Prewarm(1000);

        Assert.True(CommandPool<PrewarmLargeCommand>.PooledCount >= 1000);
    }

    #endregion

    #region Concurrent Rent/Return with 50+ Threads Tests

    [Fact]
    public void ConcurrentRentReturn_With50Threads_ShouldNotThrow()
    {
        const int threadCount = 50;
        const int operationsPerThread = 100;

        var exceptions = new ConcurrentBag<Exception>();
        var allInstances = new ConcurrentBag<ConcurrentTestCommand>();

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var instance = CommandPool<ConcurrentTestCommand>.Rent();
                        instance.Counter = i;
                        allInstances.Add(instance);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        Task.WaitAll(tasks);

        Assert.Empty(exceptions);
        Assert.Equal(threadCount * operationsPerThread, allInstances.Count);

        // Return all instances
        foreach (var inst in allInstances)
        {
            CommandPool<ConcurrentTestCommand>.Return(inst);
        }
    }

    [Fact]
    public void ConcurrentRentReturn_With100Threads_MixedOperations_ShouldNotThrow()
    {
        const int threadCount = 100;
        const int operationsPerThread = 50;

        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(threadCount);

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    // Synchronize all threads to start together
                    barrier.SignalAndWait();

                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var instance = CommandPool<MixedConcurrentCommand>.Rent();
                        instance.Value = threadId * 1000 + i;

                        // Small work simulation
                        Thread.SpinWait(100);

                        CommandPool<MixedConcurrentCommand>.Return(instance);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        Task.WaitAll(tasks);

        Assert.Empty(exceptions);
    }

    [Fact]
    public void ConcurrentRentReturn_StressTest_ShouldMaintainConsistency()
    {
        const int threadCount = 50;
        const int operationsPerThread = 200;

        var exceptions = new ConcurrentBag<Exception>();
        var rentCount = 0;
        var returnCount = 0;

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    var localInstances = new List<StressTestCommand>();

                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        // Rent
                        var instance = CommandPool<StressTestCommand>.Rent();
                        Interlocked.Increment(ref rentCount);
                        localInstances.Add(instance);

                        // Return half immediately
                        if (i % 2 == 0 && localInstances.Count > 0)
                        {
                            var toReturn = localInstances[^1];
                            localInstances.RemoveAt(localInstances.Count - 1);
                            CommandPool<StressTestCommand>.Return(toReturn);
                            Interlocked.Increment(ref returnCount);
                        }
                    }

                    // Return remaining
                    foreach (var inst in localInstances)
                    {
                        CommandPool<StressTestCommand>.Return(inst);
                        Interlocked.Increment(ref returnCount);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        Task.WaitAll(tasks);

        Assert.Empty(exceptions);
        Assert.Equal(rentCount, returnCount);
    }

    #endregion

    #region Return Null Tests

    [Fact]
    public void Return_Null_ShouldThrowNullReferenceException()
    {
        // Returning null should throw because ResetToDefault() is called on the instance
        Assert.Throws<NullReferenceException>(() =>
        {
            CommandPool<NullTestCommand>.Return(null!);
        });
    }

    [Fact]
    public void Return_NullWithExplicitCheck_ExceptionShouldBeCaught()
    {
        var exception = Record.Exception(() =>
        {
            CommandPool<NullTestCommand>.Return(null!);
        });

        Assert.NotNull(exception);
        Assert.IsType<NullReferenceException>(exception);
    }

    #endregion

    #region Return Same Item Twice Tests

    [Fact]
    public void Return_SameItemTwice_ShouldNotThrow()
    {
        var instance = CommandPool<DuplicateReturnCommand>.Rent();

        // Return the same instance twice
        // This should not throw, but may lead to pool corruption
        var exception = Record.Exception(() =>
        {
            CommandPool<DuplicateReturnCommand>.Return(instance);
            CommandPool<DuplicateReturnCommand>.Return(instance);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Return_SameItemTwice_MayLeadToPoolCorruption()
    {
        // Drain pool first to have predictable state
        var drained = new List<CorruptionTestCommand>();
        while (CommandPool<CorruptionTestCommand>.PooledCount > 0)
        {
            drained.Add(CommandPool<CorruptionTestCommand>.Rent());
        }

        // Get a fresh instance
        var instance = CommandPool<CorruptionTestCommand>.Rent();
        instance.Id = Guid.NewGuid();
        var originalId = instance.Id;

        // Return the same instance twice
        CommandPool<CorruptionTestCommand>.Return(instance);
        CommandPool<CorruptionTestCommand>.Return(instance);

        // Now pool has the same instance twice
        Assert.True(CommandPool<CorruptionTestCommand>.PooledCount >= 2);

        // Rent two instances - they should be the same object
        var rented1 = CommandPool<CorruptionTestCommand>.Rent();
        var rented2 = CommandPool<CorruptionTestCommand>.Rent();

        // This demonstrates the corruption - same instance rented twice
        Assert.Same(rented1, rented2);

        // Cleanup
        CommandPool<CorruptionTestCommand>.Return(rented1);
        // Don't return rented2 as it's the same instance
        foreach (var d in drained)
        {
            CommandPool<CorruptionTestCommand>.Return(d);
        }
    }

    [Fact]
    public void Return_SameItemMultipleTimes_PooledCountIncreases()
    {
        // Drain pool first
        var drained = new List<MultiReturnCommand>();
        while (CommandPool<MultiReturnCommand>.PooledCount > 0)
        {
            drained.Add(CommandPool<MultiReturnCommand>.Rent());
        }

        var instance = CommandPool<MultiReturnCommand>.Rent();
        var initialCount = CommandPool<MultiReturnCommand>.PooledCount;

        // Return 3 times
        CommandPool<MultiReturnCommand>.Return(instance);
        CommandPool<MultiReturnCommand>.Return(instance);
        CommandPool<MultiReturnCommand>.Return(instance);

        // Pool count should increase by 3 (even though it's the same instance)
        Assert.Equal(initialCount + 3, CommandPool<MultiReturnCommand>.PooledCount);

        // Cleanup - rent the 3 "instances" back
        for (int i = 0; i < 3; i++)
        {
            CommandPool<MultiReturnCommand>.Rent();
        }
        foreach (var d in drained)
        {
            CommandPool<MultiReturnCommand>.Return(d);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RentReturn_RapidSuccession_ShouldWork()
    {
        for (int i = 0; i < 1000; i++)
        {
            var instance = CommandPool<RapidTestCommand>.Rent();
            instance.Value = i;
            CommandPool<RapidTestCommand>.Return(instance);
        }

        // Should not throw and pool should still function
        var final = CommandPool<RapidTestCommand>.Rent();
        Assert.NotNull(final);
        CommandPool<RapidTestCommand>.Return(final);
    }

    [Fact]
    public void Pool_AfterManyOperations_ShouldRemainStable()
    {
        const int iterations = 500;
        var instances = new List<StabilityTestCommand>();

        // Rent many
        for (int i = 0; i < iterations; i++)
        {
            instances.Add(CommandPool<StabilityTestCommand>.Rent());
        }

        // Return all
        foreach (var inst in instances)
        {
            CommandPool<StabilityTestCommand>.Return(inst);
        }

        instances.Clear();

        // Rent again
        for (int i = 0; i < iterations; i++)
        {
            var inst = CommandPool<StabilityTestCommand>.Rent();
            Assert.NotNull(inst);
            instances.Add(inst);
        }

        // Return all
        foreach (var inst in instances)
        {
            CommandPool<StabilityTestCommand>.Return(inst);
        }
    }

    #endregion

    #region Helper Classes

    private class ListFieldCommand : ICommandPoolable<ListFieldCommand>
    {
        public List<int> Items { get; } = new();

        public void ResetToDefault()
        {
            Items.Clear();
        }
    }

    private class DictionaryFieldCommand : ICommandPoolable<DictionaryFieldCommand>
    {
        public Dictionary<string, string> Data { get; } = new();

        public void ResetToDefault()
        {
            Data.Clear();
        }
    }

    private class LargePoolCommand : ICommandPoolable<LargePoolCommand>
    {
        public int Value { get; set; }

        public void ResetToDefault()
        {
            Value = 0;
        }
    }

    private class VeryLargePoolCommand : ICommandPoolable<VeryLargePoolCommand>
    {
        public int Value { get; set; }

        public void ResetToDefault()
        {
            Value = 0;
        }
    }

    private class PrewarmLargeCommand : ICommandPoolable<PrewarmLargeCommand>
    {
        public void ResetToDefault() { }
    }

    private class ConcurrentTestCommand : ICommandPoolable<ConcurrentTestCommand>
    {
        public int Counter { get; set; }

        public void ResetToDefault()
        {
            Counter = 0;
        }
    }

    private class MixedConcurrentCommand : ICommandPoolable<MixedConcurrentCommand>
    {
        public int Value { get; set; }

        public void ResetToDefault()
        {
            Value = 0;
        }
    }

    private class StressTestCommand : ICommandPoolable<StressTestCommand>
    {
        public int Data { get; set; }

        public void ResetToDefault()
        {
            Data = 0;
        }
    }

    private class NullTestCommand : ICommandPoolable<NullTestCommand>
    {
        public void ResetToDefault() { }
    }

    private class DuplicateReturnCommand : ICommandPoolable<DuplicateReturnCommand>
    {
        public int Value { get; set; }

        public void ResetToDefault()
        {
            Value = 0;
        }
    }

    private class CorruptionTestCommand : ICommandPoolable<CorruptionTestCommand>
    {
        public Guid Id { get; set; }

        public void ResetToDefault()
        {
            Id = Guid.Empty;
        }
    }

    private class MultiReturnCommand : ICommandPoolable<MultiReturnCommand>
    {
        public void ResetToDefault() { }
    }

    private class RapidTestCommand : ICommandPoolable<RapidTestCommand>
    {
        public int Value { get; set; }

        public void ResetToDefault()
        {
            Value = 0;
        }
    }

    private class StabilityTestCommand : ICommandPoolable<StabilityTestCommand>
    {
        public int Data { get; set; }

        public void ResetToDefault()
        {
            Data = 0;
        }
    }

    #endregion
}
