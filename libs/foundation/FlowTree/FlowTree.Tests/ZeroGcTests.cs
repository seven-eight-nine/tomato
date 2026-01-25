using System;
using Xunit;
using static Tomato.FlowTree.Flow;

namespace Tomato.FlowTree.Tests;

public class ZeroGcTests
{
    [Fact]
    public void Tick_NoAllocation()
    {
        // 事前構築
        var stack = new FlowCallStack(32);
        var state = new TestState();

        var subTree = new FlowTree("SubTree");
        subTree.Build(state, 
                Sequence(
                    Action(static () => NodeStatus.Success),
                    Action(static () => NodeStatus.Success)
                )
            );

        var tree = new FlowTree("Main");
        tree
            .WithCallStack(stack)
            .Build(state, 
                Sequence(
                    Action<TestState>(s =>
                    {
                        s.IntValue = 42;
                        return NodeStatus.Success;
                    }),
                    Selector(
                        Condition<TestState>(s => s.BoolValue),
                        Action(static () => NodeStatus.Success)
                    ),
                    SubTree(subTree),
                    Wait(0.1f)
                )
            );

        // ウォームアップ（JIT等）
        for (int i = 0; i < 100; i++)
        {
            tree.Reset();
            tree.Tick(0.05f);
        }

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 1000; i++)
        {
            tree.Reset();

            // Tick数回でツリーが完了するまで
            for (int t = 0; t < 10; t++)
            {
                var status = tree.Tick(0.016f);
                if (status != NodeStatus.Running)
                    break;
            }
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = allocatedAfter - allocatedBefore;

        // ゼロアロケーションを検証（多少の許容範囲を設ける）
        Assert.True(allocated < 1024, $"Allocated {allocated} bytes during Tick loop.");
    }

    [Fact]
    public void CallStack_NoAllocation()
    {
        var stack = new FlowCallStack(32);

        // ダミーツリーを作成
        var dummyTree1 = new FlowTree("Dummy1");
        var dummyTree2 = new FlowTree("Dummy2");
        dummyTree1.Build(Success);
        dummyTree2.Build(Success);

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            stack.Push(new CallFrame(dummyTree1, i));
            stack.Pop();
        }

        stack.Clear();

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10000; i++)
        {
            stack.Push(new CallFrame(dummyTree1, 0));
            stack.Push(new CallFrame(dummyTree2, 1));
            stack.TryPeek(out _);
            _ = stack.Contains(dummyTree1);
            stack.Pop();
            stack.Pop();
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = allocatedAfter - allocatedBefore;

        Assert.True(allocated < 1024, $"Allocated {allocated} bytes during CallStack operations.");
    }

    [Fact]
    public void CompositeNodes_NoAllocation()
    {
        var sequence = new SequenceNode(
            new ActionNode(static () => NodeStatus.Success),
            new ActionNode(static () => NodeStatus.Success)
        );

        var selector = new SelectorNode(
            new ActionNode(static () => NodeStatus.Failure),
            new ActionNode(static () => NodeStatus.Success)
        );

        var race = new RaceNode(
            new ActionNode(static () => NodeStatus.Success),
            new ActionNode(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext();

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            sequence.Reset();
            sequence.Tick(ref ctx);
            selector.Reset();
            selector.Tick(ref ctx);
            race.Reset();
            race.Tick(ref ctx);
        }

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10000; i++)
        {
            sequence.Reset();
            sequence.Tick(ref ctx);
            selector.Reset();
            selector.Tick(ref ctx);
            race.Reset();
            race.Tick(ref ctx);
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = allocatedAfter - allocatedBefore;

        Assert.True(allocated < 1024, $"Allocated {allocated} bytes during composite node operations.");
    }

    [Fact]
    public void DecoratorNodes_NoAllocation()
    {
        var innerAction = new ActionNode(static () => NodeStatus.Success);
        var inverter = new InverterNode(innerAction);
        var succeeder = new SucceederNode(new ActionNode(static () => NodeStatus.Failure));
        var repeat = new RepeatNode(3, new ActionNode(static () => NodeStatus.Success));

        var ctx = new FlowContext();

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            inverter.Reset();
            inverter.Tick(ref ctx);
            succeeder.Reset();
            succeeder.Tick(ref ctx);
            repeat.Reset();
            repeat.Tick(ref ctx);
        }

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10000; i++)
        {
            inverter.Reset();
            inverter.Tick(ref ctx);
            succeeder.Reset();
            succeeder.Tick(ref ctx);
            repeat.Reset();
            repeat.Tick(ref ctx);
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = allocatedAfter - allocatedBefore;

        Assert.True(allocated < 1024, $"Allocated {allocated} bytes during decorator node operations.");
    }

    [Fact]
    public void GenericContext_NoAllocation()
    {
        var state = new TestState { IntValue = 0 };

        var tree = new FlowTree();
        tree.Build(state, 
                Sequence(
                    Action<TestState>(s =>
                    {
                        s.IntValue++;
                        return NodeStatus.Success;
                    }),
                    Condition<TestState>(s => s.IntValue > 0)
                )
            );

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            tree.Reset();
            tree.Tick(0.016f);
        }

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10000; i++)
        {
            tree.Reset();
            tree.Tick(0.016f);
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = allocatedAfter - allocatedBefore;

        Assert.True(allocated < 1024, $"Allocated {allocated} bytes during generic context operations.");
    }

    private class TestState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int IntValue { get; set; }
        public float FloatValue { get; set; }
        public bool BoolValue { get; set; }
    }
}
