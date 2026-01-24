using Xunit;

namespace Tomato.FlowTree.Tests;

public class CoreTests
{
    [Fact]
    public void FlowCallStack_PushPop()
    {
        var stack = new FlowCallStack(8);
        var tree1 = new FlowTree("Tree1");
        var tree2 = new FlowTree("Tree2");
        tree1.Build().Success().Complete();
        tree2.Build().Success().Complete();

        Assert.True(stack.IsEmpty);
        Assert.False(stack.IsFull);

        stack.Push(new CallFrame(tree1));
        stack.Push(new CallFrame(tree2));

        Assert.Equal(2, stack.Count);
        Assert.False(stack.IsEmpty);

        var frame = stack.Pop();
        Assert.Same(tree2, frame.Tree);

        frame = stack.Pop();
        Assert.Same(tree1, frame.Tree);
        Assert.True(stack.IsEmpty);
    }

    [Fact]
    public void FlowCallStack_Overflow()
    {
        var stack = new FlowCallStack(2);
        var tree1 = new FlowTree("Tree1");
        var tree2 = new FlowTree("Tree2");
        var tree3 = new FlowTree("Tree3");
        tree1.Build().Success().Complete();
        tree2.Build().Success().Complete();
        tree3.Build().Success().Complete();

        Assert.True(stack.TryPush(new CallFrame(tree1)));
        Assert.True(stack.TryPush(new CallFrame(tree2)));
        Assert.False(stack.TryPush(new CallFrame(tree3)));
        Assert.True(stack.IsFull);
    }

    [Fact]
    public void FlowCallStack_Contains()
    {
        var stack = new FlowCallStack(8);
        var tree1 = new FlowTree("Tree1");
        var tree2 = new FlowTree("Tree2");
        var tree3 = new FlowTree("Tree3");
        tree1.Build().Success().Complete();
        tree2.Build().Success().Complete();
        tree3.Build().Success().Complete();

        stack.Push(new CallFrame(tree1));
        stack.Push(new CallFrame(tree2));

        Assert.True(stack.Contains(tree1));
        Assert.True(stack.Contains(tree2));
        Assert.False(stack.Contains(tree3));
    }

    [Fact]
    public void FlowContext_GenericState()
    {
        var state = new TestState { Counter = 10 };
        var tree = new FlowTree();
        tree.Build(state)
            .Action(s =>
            {
                s.Counter++;
                return NodeStatus.Success;
            })
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(11, state.Counter);
    }

    [Fact]
    public void FlowContext_GenericCondition()
    {
        var state = new TestState { IsEnabled = false };
        var tree = new FlowTree();
        tree.Build(state)
            .Condition(s => s.IsEnabled)
            .Complete();

        Assert.Equal(NodeStatus.Failure, tree.Tick(0.016f));

        state.IsEnabled = true;
        tree.Reset();
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
    }

    private class TestState
    {
        public int Counter { get; set; }
        public bool IsEnabled { get; set; }
    }
}
