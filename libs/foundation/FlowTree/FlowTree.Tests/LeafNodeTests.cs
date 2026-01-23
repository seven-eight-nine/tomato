using Xunit;

namespace Tomato.FlowTree.Tests;

public class LeafNodeTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void ActionNode_ReturnsSuccess()
    {
        var node = new ActionNode(static (ref FlowContext _) => NodeStatus.Success);
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    [Fact]
    public void ActionNode_ReturnsFailure()
    {
        var node = new ActionNode(static (ref FlowContext _) => NodeStatus.Failure);
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));
    }

    [Fact]
    public void ActionNode_ReturnsRunning()
    {
        var node = new ActionNode(static (ref FlowContext _) => NodeStatus.Running);
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
    }

    [Fact]
    public void ActionNode_UsesContext()
    {
        var key = new BlackboardKey<int>(1);
        var node = new ActionNode((ref FlowContext ctx) =>
        {
            ctx.Blackboard.SetInt(key, 42);
            return NodeStatus.Success;
        });

        var ctx = CreateContext();
        node.Tick(ref ctx);

        Assert.Equal(42, ctx.Blackboard.GetInt(key));
    }

    [Fact]
    public void ConditionNode_True()
    {
        var node = new ConditionNode(static (ref FlowContext _) => true);
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    [Fact]
    public void ConditionNode_False()
    {
        var node = new ConditionNode(static (ref FlowContext _) => false);
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));
    }

    [Fact]
    public void ConditionNode_UsesBlackboard()
    {
        var key = new BlackboardKey<bool>(1);
        var node = new ConditionNode((ref FlowContext ctx) => ctx.Blackboard.GetBool(key));

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));

        ctx.Blackboard.SetBool(key, true);
        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }
}
