using Xunit;

namespace Tomato.FlowTree.Tests;

public class CoreTests
{
    [Fact]
    public void BlackboardKey_Equality()
    {
        var key1 = new BlackboardKey<int>(1);
        var key2 = new BlackboardKey<int>(1);
        var key3 = new BlackboardKey<int>(2);

        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
    }

    [Fact]
    public void Blackboard_IntValues()
    {
        var bb = new Blackboard();
        var key = new BlackboardKey<int>(1);

        Assert.False(bb.TryGetInt(key, out _));
        Assert.Equal(42, bb.GetInt(key, 42));

        bb.SetInt(key, 100);
        Assert.True(bb.TryGetInt(key, out var value));
        Assert.Equal(100, value);
        Assert.Equal(100, bb.GetInt(key));
    }

    [Fact]
    public void Blackboard_FloatValues()
    {
        var bb = new Blackboard();
        var key = new BlackboardKey<float>(1);

        bb.SetFloat(key, 3.14f);
        Assert.True(bb.TryGetFloat(key, out var value));
        Assert.Equal(3.14f, value);
    }

    [Fact]
    public void Blackboard_BoolValues()
    {
        var bb = new Blackboard();
        var key = new BlackboardKey<bool>(1);

        Assert.False(bb.GetBool(key));
        bb.SetBool(key, true);
        Assert.True(bb.GetBool(key));
    }

    [Fact]
    public void Blackboard_Clear()
    {
        var bb = new Blackboard();
        var intKey = new BlackboardKey<int>(1);
        var floatKey = new BlackboardKey<float>(2);

        bb.SetInt(intKey, 100);
        bb.SetFloat(floatKey, 1.5f);

        bb.Clear();

        Assert.False(bb.TryGetInt(intKey, out _));
        Assert.False(bb.TryGetFloat(floatKey, out _));
    }

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
}
