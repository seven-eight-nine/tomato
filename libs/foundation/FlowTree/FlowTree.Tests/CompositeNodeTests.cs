using Xunit;

namespace Tomato.FlowTree.Tests;

public class CompositeNodeTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void SequenceNode_AllSuccess()
    {
        var sequence = new SequenceNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        var result = sequence.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, result);
    }

    [Fact]
    public void SequenceNode_FirstFailure()
    {
        var executed = new bool[3];

        var sequence = new SequenceNode(
            new ActionNode((ref FlowContext _) => { executed[0] = true; return NodeStatus.Success; }),
            new ActionNode((ref FlowContext _) => { executed[1] = true; return NodeStatus.Failure; }),
            new ActionNode((ref FlowContext _) => { executed[2] = true; return NodeStatus.Success; })
        );

        var ctx = CreateContext();
        var result = sequence.Tick(ref ctx);

        Assert.Equal(NodeStatus.Failure, result);
        Assert.True(executed[0]);
        Assert.True(executed[1]);
        Assert.False(executed[2]); // 3番目は実行されない
    }

    [Fact]
    public void SequenceNode_Running()
    {
        int callCount = 0;

        var sequence = new SequenceNode(
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Success; }),
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Running; }),
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
        );

        var ctx = CreateContext();

        // 1回目のTick
        var result = sequence.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(2, callCount);

        // 2回目のTick（Runningから再開）
        result = sequence.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(3, callCount); // 2番目のノードから再開
    }

    [Fact]
    public void SelectorNode_FirstSuccess()
    {
        var executed = new bool[3];

        var selector = new SelectorNode(
            new ActionNode((ref FlowContext _) => { executed[0] = true; return NodeStatus.Failure; }),
            new ActionNode((ref FlowContext _) => { executed[1] = true; return NodeStatus.Success; }),
            new ActionNode((ref FlowContext _) => { executed[2] = true; return NodeStatus.Success; })
        );

        var ctx = CreateContext();
        var result = selector.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, result);
        Assert.True(executed[0]);
        Assert.True(executed[1]);
        Assert.False(executed[2]); // 3番目は実行されない
    }

    [Fact]
    public void SelectorNode_AllFailure()
    {
        var selector = new SelectorNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure)
        );

        var ctx = CreateContext();
        var result = selector.Tick(ref ctx);

        Assert.Equal(NodeStatus.Failure, result);
    }

    [Fact]
    public void SelectorNode_Running()
    {
        int callCount = 0;

        var selector = new SelectorNode(
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Failure; }),
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Running; }),
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
        );

        var ctx = CreateContext();

        // 1回目のTick
        var result = selector.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(2, callCount);

        // 2回目のTick（Runningから再開）
        result = selector.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(3, callCount); // 2番目のノードから再開
    }

    [Fact]
    public void SequenceNode_Reset()
    {
        int index = 0;
        var sequence = new SequenceNode(
            new ActionNode((ref FlowContext _) => { index = 1; return NodeStatus.Running; }),
            new ActionNode((ref FlowContext _) => { index = 2; return NodeStatus.Success; })
        );

        var ctx = CreateContext();
        sequence.Tick(ref ctx); // Running状態
        Assert.Equal(1, index);

        sequence.Reset();
        sequence.Tick(ref ctx);
        Assert.Equal(1, index); // リセット後は最初から
    }
}
