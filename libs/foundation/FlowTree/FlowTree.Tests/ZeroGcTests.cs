using System;
using Xunit;

namespace Tomato.FlowTree.Tests;

public class ZeroGcTests
{
    private static FlowContext CreateContext(FlowCallStack? stack = null)
    {
        return FlowContext.Create(
            new Blackboard(64),
            stack,
            deltaTime: 0.016f
        );
    }

    [Fact]
    public void Tick_NoAllocation()
    {
        // 事前構築
        var stack = new FlowCallStack(32);
        var bb = new Blackboard(64);

        var subTree = new FlowTree("SubTree");
        subTree.Build()
            .Sequence()
                .Action(static (ref FlowContext _) => NodeStatus.Success)
                .Action(static (ref FlowContext _) => NodeStatus.Success)
            .End()
            .Complete();

        var tree = new FlowTree("Main");
        tree.Build()
            .Sequence()
                .Action(static (ref FlowContext ctx) =>
                {
                    var key = new BlackboardKey<int>(1);
                    ctx.Blackboard.SetInt(key, 42);
                    return NodeStatus.Success;
                })
                .Selector()
                    .Condition(static (ref FlowContext ctx) =>
                    {
                        var key = new BlackboardKey<bool>(2);
                        return ctx.Blackboard.GetBool(key);
                    })
                    .Action(static (ref FlowContext _) => NodeStatus.Success)
                .End()
                .SubTree(subTree)
                .Wait(0.1f)
            .End()
            .Complete();

        var ctx = FlowContext.Create(bb, stack, 0.05f);

        // ウォームアップ（JIT等）
        for (int i = 0; i < 100; i++)
        {
            tree.Reset();
            tree.Tick(ref ctx);
            ctx.DeltaTime = 0.05f;
        }

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 1000; i++)
        {
            tree.Reset();
            ctx.DeltaTime = 0.016f;

            // Tick数回でツリーが完了するまで
            for (int t = 0; t < 10; t++)
            {
                var status = tree.Tick(ref ctx);
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
    public void Blackboard_NoAllocation()
    {
        var bb = new Blackboard(64);
        var intKey = new BlackboardKey<int>(1);
        var floatKey = new BlackboardKey<float>(2);
        var boolKey = new BlackboardKey<bool>(3);

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            bb.SetInt(intKey, i);
            bb.SetFloat(floatKey, i * 0.5f);
            bb.SetBool(boolKey, i % 2 == 0);
            bb.GetInt(intKey);
            bb.GetFloat(floatKey);
            bb.GetBool(boolKey);
        }

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10000; i++)
        {
            bb.SetInt(intKey, i);
            bb.SetFloat(floatKey, i * 0.5f);
            bb.SetBool(boolKey, i % 2 == 0);
            _ = bb.GetInt(intKey);
            _ = bb.GetFloat(floatKey);
            _ = bb.GetBool(boolKey);
            bb.TryGetInt(intKey, out _);
            bb.TryGetFloat(floatKey, out _);
            bb.TryGetBool(boolKey, out _);
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = allocatedAfter - allocatedBefore;

        Assert.True(allocated < 1024, $"Allocated {allocated} bytes during Blackboard operations.");
    }

    [Fact]
    public void CallStack_NoAllocation()
    {
        var stack = new FlowCallStack(32);

        // ダミーツリーを作成
        var dummyTree1 = new FlowTree("Dummy1");
        var dummyTree2 = new FlowTree("Dummy2");
        dummyTree1.Build().Success().Complete();
        dummyTree2.Build().Success().Complete();

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
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var selector = new SelectorNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var parallel = new ParallelNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            sequence.Reset();
            sequence.Tick(ref ctx);
            selector.Reset();
            selector.Tick(ref ctx);
            parallel.Reset();
            parallel.Tick(ref ctx);
        }

        // GCアロケーション計測
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10000; i++)
        {
            sequence.Reset();
            sequence.Tick(ref ctx);
            selector.Reset();
            selector.Tick(ref ctx);
            parallel.Reset();
            parallel.Tick(ref ctx);
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocated = allocatedAfter - allocatedBefore;

        Assert.True(allocated < 1024, $"Allocated {allocated} bytes during composite node operations.");
    }

    [Fact]
    public void DecoratorNodes_NoAllocation()
    {
        var innerAction = new ActionNode(static (ref FlowContext _) => NodeStatus.Success);
        var inverter = new InverterNode(innerAction);
        var succeeder = new SucceederNode(new ActionNode(static (ref FlowContext _) => NodeStatus.Failure));
        var repeat = new RepeatNode(3, new ActionNode(static (ref FlowContext _) => NodeStatus.Success));

        var ctx = CreateContext();

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
}
