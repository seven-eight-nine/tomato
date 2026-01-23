using Xunit;

namespace Tomato.FlowTree.Tests;

public class ParallelNodeTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void ParallelNode_AllSuccess()
    {
        var parallel = new ParallelNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, parallel.Tick(ref ctx));
    }

    [Fact]
    public void ParallelNode_OneFailure_RequireAll()
    {
        var parallel = new ParallelNode(
            ParallelPolicy.RequireAll,
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, parallel.Tick(ref ctx));
    }

    [Fact]
    public void ParallelNode_OneSuccess_RequireOne()
    {
        var parallel = new ParallelNode(
            ParallelPolicy.RequireOne,
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, parallel.Tick(ref ctx));
    }

    [Fact]
    public void ParallelNode_Running()
    {
        var callCount = new int[3];
        var parallel = new ParallelNode(
            new ActionNode((ref FlowContext _) => { callCount[0]++; return NodeStatus.Success; }),
            new ActionNode((ref FlowContext _) => { callCount[1]++; return NodeStatus.Running; }),
            new ActionNode((ref FlowContext _) => { callCount[2]++; return NodeStatus.Success; })
        );

        var ctx = CreateContext();

        // 1回目
        Assert.Equal(NodeStatus.Running, parallel.Tick(ref ctx));
        Assert.Equal(1, callCount[0]);
        Assert.Equal(1, callCount[1]);
        Assert.Equal(1, callCount[2]);

        // 2回目（完了したノードはスキップされる）
        Assert.Equal(NodeStatus.Running, parallel.Tick(ref ctx));
        Assert.Equal(1, callCount[0]); // スキップ
        Assert.Equal(2, callCount[1]);
        Assert.Equal(1, callCount[2]); // スキップ
    }
}

public class RaceNodeTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void RaceNode_FirstCompletes()
    {
        var race = new RaceNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, race.Tick(ref ctx));
    }

    [Fact]
    public void RaceNode_FirstFailure()
    {
        var race = new RaceNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, race.Tick(ref ctx));
    }

    [Fact]
    public void RaceNode_AllRunning()
    {
        var race = new RaceNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Running, race.Tick(ref ctx));
    }
}

public class JoinNodeTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void JoinNode_AllSuccess()
    {
        var join = new JoinNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, join.Tick(ref ctx));
    }

    [Fact]
    public void JoinNode_OneFailure_RequireAll()
    {
        var join = new JoinNode(
            JoinPolicy.RequireAll,
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, join.Tick(ref ctx));
    }

    [Fact]
    public void JoinNode_OneSuccess_RequireAny()
    {
        var join = new JoinNode(
            JoinPolicy.RequireAny,
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, join.Tick(ref ctx));
    }

    [Fact]
    public void JoinNode_WaitsForAll()
    {
        var completeCount = 0;
        var join = new JoinNode(
            new ActionNode((ref FlowContext _) => { completeCount++; return NodeStatus.Success; }),
            new ActionNode((ref FlowContext _) => { completeCount++; return NodeStatus.Running; })
        );

        var ctx = CreateContext();

        // まだRunning
        Assert.Equal(NodeStatus.Running, join.Tick(ref ctx));
        Assert.Equal(2, completeCount);
    }
}

public class RandomSelectorNodeTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void RandomSelectorNode_SelectsOne()
    {
        var random = new RandomSelectorNode(
            42, // 固定シード
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure)
        );

        var ctx = CreateContext();
        var result = random.Tick(ref ctx);
        Assert.True(result == NodeStatus.Success || result == NodeStatus.Failure);
    }

    [Fact]
    public void RandomSelectorNode_Empty()
    {
        var random = new RandomSelectorNode();
        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, random.Tick(ref ctx));
    }

    [Fact]
    public void RandomSelectorNode_Running()
    {
        var callCount = 0;
        var random = new RandomSelectorNode(
            42,
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Running; })
        );

        var ctx = CreateContext();

        // 1回目
        Assert.Equal(NodeStatus.Running, random.Tick(ref ctx));
        Assert.Equal(1, callCount);

        // 2回目（同じノードが継続）
        Assert.Equal(NodeStatus.Running, random.Tick(ref ctx));
        Assert.Equal(2, callCount);
    }
}
