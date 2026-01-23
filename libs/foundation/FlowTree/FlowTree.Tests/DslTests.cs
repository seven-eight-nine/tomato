using Xunit;
using static Tomato.FlowTree.Flow;

namespace Tomato.FlowTree.Tests;

public class FlowTreeBuilderTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void Builder_SimpleSequence()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Sequence()
                .Action((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
                .Action((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
            .End()
            .Complete();

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, tree.Tick(ref ctx));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_NestedComposites()
    {
        var executed = new bool[3];

        var tree = new FlowTree();
        tree.Build()
            .Sequence()
                .Selector()
                    .Action((ref FlowContext _) => { executed[0] = true; return NodeStatus.Failure; })
                    .Action((ref FlowContext _) => { executed[1] = true; return NodeStatus.Success; })
                .End()
                .Action((ref FlowContext _) => { executed[2] = true; return NodeStatus.Success; })
            .End()
            .Complete();

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, tree.Tick(ref ctx));
        Assert.True(executed[0]);
        Assert.True(executed[1]);
        Assert.True(executed[2]);
    }

    [Fact]
    public void Builder_WithDecorators()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Retry(2, new ActionNode((ref FlowContext _) =>
            {
                callCount++;
                return callCount < 2 ? NodeStatus.Failure : NodeStatus.Success;
            }))
            .Complete();

        var ctx = CreateContext();

        // 1回目: Failure → Running
        Assert.Equal(NodeStatus.Running, tree.Tick(ref ctx));
        // 2回目: Success
        Assert.Equal(NodeStatus.Success, tree.Tick(ref ctx));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_WithSubTree()
    {
        var executed = new bool[2];

        var subTree = new FlowTree("SubTree");
        subTree.Build()
            .Action((ref FlowContext _) => { executed[1] = true; return NodeStatus.Success; })
            .Complete();

        var mainTree = new FlowTree("MainTree");
        mainTree.Build()
            .Sequence()
                .Action((ref FlowContext _) => { executed[0] = true; return NodeStatus.Success; })
                .SubTree(subTree)
            .End()
            .Complete();

        var ctx = FlowContext.Create(
            new Blackboard(),
            new FlowCallStack(16)
        );

        Assert.Equal(NodeStatus.Success, mainTree.Tick(ref ctx));
        Assert.True(executed[0]);
        Assert.True(executed[1]);
    }

    [Fact]
    public void Builder_Wait()
    {
        var tree = new FlowTree();
        tree.Build()
            .Wait(0.5f)
            .Complete();

        var ctx = FlowContext.Create(new Blackboard(), 0.3f);

        Assert.Equal(NodeStatus.Running, tree.Tick(ref ctx));
        Assert.Equal(NodeStatus.Success, tree.Tick(ref ctx)); // 0.6秒経過
    }

    [Fact]
    public void Builder_SingleAction()
    {
        var tree = new FlowTree();
        tree.Build()
            .Action(static (ref FlowContext _) => NodeStatus.Success)
            .Complete();

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, tree.Tick(ref ctx));
    }

    [Fact]
    public void Builder_UnclosedComposite_ThrowsOnComplete()
    {
        var tree = new FlowTree();
        var builder = tree.Build()
            .Sequence()
                .Action(static (ref FlowContext _) => NodeStatus.Success);

        Assert.Throws<System.InvalidOperationException>(() => builder.Complete());
    }

    [Fact]
    public void Builder_NoRoot_ThrowsOnComplete()
    {
        var tree = new FlowTree();
        var builder = tree.Build();
        Assert.Throws<System.InvalidOperationException>(() => builder.Complete());
    }
}

public class FlowShorthandTests
{
    private static FlowContext CreateContext()
    {
        return FlowContext.Create(new Blackboard());
    }

    [Fact]
    public void Flow_TreeCreation()
    {
        var tree = Tree("TestTree");
        tree.Build()
            .Sequence()
                .Action(static (ref FlowContext _) => NodeStatus.Success)
            .End()
            .Complete();

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, tree.Tick(ref ctx));
        Assert.Equal("TestTree", tree.Name);
    }

    [Fact]
    public void Flow_Sequence()
    {
        var sequence = Sequence(
            Action(static (ref FlowContext _) => NodeStatus.Success),
            Action(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, sequence.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Selector()
    {
        var selector = Selector(
            Action(static (ref FlowContext _) => NodeStatus.Failure),
            Action(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, selector.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Parallel()
    {
        var parallel = Parallel(
            Action(static (ref FlowContext _) => NodeStatus.Success),
            Action(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, parallel.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Race()
    {
        var race = Race(
            Action(static (ref FlowContext _) => NodeStatus.Running),
            Action(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, race.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Join()
    {
        var join = Join(
            Action(static (ref FlowContext _) => NodeStatus.Success),
            Action(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, join.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Decorators()
    {
        var ctx = CreateContext();

        var inverted = Inverter(Action(static (ref FlowContext _) => NodeStatus.Success));
        Assert.Equal(NodeStatus.Failure, inverted.Tick(ref ctx));

        var succeeded = Succeeder(Action(static (ref FlowContext _) => NodeStatus.Failure));
        Assert.Equal(NodeStatus.Success, succeeded.Tick(ref ctx));

        var failed = Failer(Action(static (ref FlowContext _) => NodeStatus.Success));
        Assert.Equal(NodeStatus.Failure, failed.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Guard()
    {
        var key = new BlackboardKey<bool>(1);

        var guarded = Guard(
            (ref FlowContext ctx) => ctx.Blackboard.GetBool(key),
            Action(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, guarded.Tick(ref ctx));

        ctx.Blackboard.SetBool(key, true);
        Assert.Equal(NodeStatus.Success, guarded.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Retry()
    {
        int count = 0;
        var retried = Retry(2, Action((ref FlowContext _) =>
        {
            count++;
            return count < 2 ? NodeStatus.Failure : NodeStatus.Success;
        }));

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Running, retried.Tick(ref ctx)); // 失敗→リトライ
        Assert.Equal(NodeStatus.Success, retried.Tick(ref ctx)); // 成功
    }

    [Fact]
    public void Flow_Timeout()
    {
        var timeout = Timeout(0.5f, Action(static (ref FlowContext _) => NodeStatus.Running));

        var ctx = FlowContext.Create(new Blackboard(), 0.6f);
        Assert.Equal(NodeStatus.Failure, timeout.Tick(ref ctx)); // タイムアウト
    }

    [Fact]
    public void Flow_Delay()
    {
        int callCount = 0;
        var delayed = Delay(0.5f, Action((ref FlowContext _) => { callCount++; return NodeStatus.Success; }));

        var ctx = FlowContext.Create(new Blackboard(), 0.3f);
        Assert.Equal(NodeStatus.Running, delayed.Tick(ref ctx)); // 遅延中
        Assert.Equal(0, callCount);

        Assert.Equal(NodeStatus.Success, delayed.Tick(ref ctx)); // 遅延完了
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Flow_SuccessFailure()
    {
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Success, Success.Tick(ref ctx));
        Assert.Equal(NodeStatus.Failure, Failure.Tick(ref ctx));
    }

    [Fact]
    public void Flow_ComplexTree()
    {
        // 計画書にあるような複雑なツリーの例
        var patrolFlow = new FlowTree("Patrol");
        var attackFlow = new FlowTree("Attack");
        var fleeFlow = new FlowTree("Flee");

        // ダミー実装
        patrolFlow.Build().Success().Complete();
        attackFlow.Build().Success().Complete();
        fleeFlow.Build().Success().Complete();

        var isLowHealthKey = new BlackboardKey<bool>(1);
        var hasTargetKey = new BlackboardKey<bool>(2);

        // AI行動選択ツリー（計画書の例を再現）
        var aiTree = Tree("AI Behavior");
        aiTree.Build()
            .Selector()
                .Guard((ref FlowContext ctx) => ctx.Blackboard.GetBool(isLowHealthKey),
                    new SubTreeNode(fleeFlow))
                .Guard((ref FlowContext ctx) => ctx.Blackboard.GetBool(hasTargetKey),
                    new SubTreeNode(attackFlow))
                .SubTree(patrolFlow)
            .End()
            .Complete();

        Assert.Equal("AI Behavior", aiTree.Name);
    }
}
