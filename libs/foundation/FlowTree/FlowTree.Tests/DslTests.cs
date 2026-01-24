using Xunit;
using static Tomato.FlowTree.Flow;

namespace Tomato.FlowTree.Tests;

public class FlowTreeBuilderTests
{
    [Fact]
    public void Builder_SimpleSequence()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Sequence()
                .Action(() => { callCount++; return NodeStatus.Success; })
                .Action(() => { callCount++; return NodeStatus.Success; })
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
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
                    .Action(() => { executed[0] = true; return NodeStatus.Failure; })
                    .Action(() => { executed[1] = true; return NodeStatus.Success; })
                .End()
                .Action(() => { executed[2] = true; return NodeStatus.Success; })
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
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
            .Retry(2, new ActionNode(() =>
            {
                callCount++;
                return callCount < 2 ? NodeStatus.Failure : NodeStatus.Success;
            }))
            .Complete();

        // 1回目: Failure → Running
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        // 2回目: Success
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_WithSubTree()
    {
        var executed = new bool[2];

        var subTree = new FlowTree("SubTree");
        subTree.Build()
            .Action(() => { executed[1] = true; return NodeStatus.Success; })
            .Complete();

        var mainTree = new FlowTree("MainTree");
        mainTree
            .WithCallStack(new FlowCallStack(16))
            .Build()
            .Sequence()
                .Action(() => { executed[0] = true; return NodeStatus.Success; })
                .SubTree(subTree)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, mainTree.Tick(0.016f));
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

        Assert.Equal(NodeStatus.Running, tree.Tick(0.3f));
        Assert.Equal(NodeStatus.Success, tree.Tick(0.3f)); // 0.6秒経過
    }

    [Fact]
    public void Builder_SingleAction()
    {
        var tree = new FlowTree();
        tree.Build()
            .Action(static () => NodeStatus.Success)
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_UnclosedComposite_ThrowsOnComplete()
    {
        var tree = new FlowTree();
        var builder = tree.Build()
            .Sequence()
                .Action(static () => NodeStatus.Success);

        Assert.Throws<System.InvalidOperationException>(() => builder.Complete());
    }

    [Fact]
    public void Builder_NoRoot_ThrowsOnComplete()
    {
        var tree = new FlowTree();
        var builder = tree.Build();
        Assert.Throws<System.InvalidOperationException>(() => builder.Complete());
    }

    [Fact]
    public void Builder_GenericState()
    {
        var state = new GameState { Score = 100 };
        var tree = new FlowTree();
        tree.Build(state)
            .Sequence()
                .Action(s =>
                {
                    s.Score += 10;
                    return NodeStatus.Success;
                })
                .Condition(s => s.Score > 100)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(110, state.Score);
    }

    private class GameState
    {
        public int Score { get; set; }
    }
}

public class FlowShorthandTests
{
    [Fact]
    public void Flow_TreeCreation()
    {
        var tree = Tree("TestTree");
        tree.Build()
            .Sequence()
                .Action(static () => NodeStatus.Success)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal("TestTree", tree.Name);
    }

    [Fact]
    public void Flow_Sequence()
    {
        var sequence = Sequence(
            Action(static () => NodeStatus.Success),
            Action(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Success, sequence.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Selector()
    {
        var selector = Selector(
            Action(static () => NodeStatus.Failure),
            Action(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Success, selector.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Parallel()
    {
        var parallel = Parallel(
            Action(static () => NodeStatus.Success),
            Action(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Success, parallel.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Race()
    {
        var race = Race(
            Action(static () => NodeStatus.Running),
            Action(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Success, race.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Join()
    {
        var join = Join(
            Action(static () => NodeStatus.Success),
            Action(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Success, join.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Decorators()
    {
        var ctx = new FlowContext();

        var inverted = Inverter(Action(static () => NodeStatus.Success));
        Assert.Equal(NodeStatus.Failure, inverted.Tick(ref ctx));

        var succeeded = Succeeder(Action(static () => NodeStatus.Failure));
        Assert.Equal(NodeStatus.Success, succeeded.Tick(ref ctx));

        var failed = Failer(Action(static () => NodeStatus.Success));
        Assert.Equal(NodeStatus.Failure, failed.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Guard_WithState()
    {
        var state = new TestState { IsEnabled = false };

        var guarded = new GuardNode<TestState>(
            s => s.IsEnabled,
            Action(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext { State = state };
        Assert.Equal(NodeStatus.Failure, guarded.Tick(ref ctx));

        state.IsEnabled = true;
        guarded.Reset();
        Assert.Equal(NodeStatus.Success, guarded.Tick(ref ctx));
    }

    [Fact]
    public void Flow_Retry()
    {
        int count = 0;
        var retried = Retry(2, Action(() =>
        {
            count++;
            return count < 2 ? NodeStatus.Failure : NodeStatus.Success;
        }));

        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Running, retried.Tick(ref ctx)); // 失敗→リトライ
        Assert.Equal(NodeStatus.Success, retried.Tick(ref ctx)); // 成功
    }

    [Fact]
    public void Flow_Timeout()
    {
        var timeout = Timeout(0.5f, Action(static () => NodeStatus.Running));

        var ctx = new FlowContext { DeltaTime = 0.6f };
        Assert.Equal(NodeStatus.Failure, timeout.Tick(ref ctx)); // タイムアウト
    }

    [Fact]
    public void Flow_Delay()
    {
        int callCount = 0;
        var delayed = Delay(0.5f, Action(() => { callCount++; return NodeStatus.Success; }));

        var ctx = new FlowContext { DeltaTime = 0.3f };
        Assert.Equal(NodeStatus.Running, delayed.Tick(ref ctx)); // 遅延中
        Assert.Equal(0, callCount);

        Assert.Equal(NodeStatus.Success, delayed.Tick(ref ctx)); // 遅延完了
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Flow_SuccessFailure()
    {
        var ctx = new FlowContext();

        Assert.Equal(NodeStatus.Success, Success.Tick(ref ctx));
        Assert.Equal(NodeStatus.Failure, Failure.Tick(ref ctx));
    }

    [Fact]
    public void Flow_ComplexTree_WithState()
    {
        // 計画書にあるような複雑なツリーの例
        var patrolFlow = new FlowTree("Patrol");
        var attackFlow = new FlowTree("Attack");
        var fleeFlow = new FlowTree("Flee");

        // ダミー実装
        patrolFlow.Build().Success().Complete();
        attackFlow.Build().Success().Complete();
        fleeFlow.Build().Success().Complete();

        var state = new AIState();

        // AI行動選択ツリー（計画書の例を再現）
        var aiTree = Tree("AI Behavior");
        aiTree
            .WithCallStack(new FlowCallStack(16))
            .Build(state)
            .Selector()
                .Guard(s => s.IsLowHealth,
                    new SubTreeNode(fleeFlow))
                .Guard(s => s.HasTarget,
                    new SubTreeNode(attackFlow))
                .SubTree(patrolFlow)
            .End()
            .Complete();

        Assert.Equal("AI Behavior", aiTree.Name);

        // 初期状態: パトロール
        Assert.Equal(NodeStatus.Success, aiTree.Tick(0.016f));

        // ターゲット発見: 攻撃
        state.HasTarget = true;
        aiTree.Reset();
        Assert.Equal(NodeStatus.Success, aiTree.Tick(0.016f));

        // 体力低下: 逃走
        state.IsLowHealth = true;
        aiTree.Reset();
        Assert.Equal(NodeStatus.Success, aiTree.Tick(0.016f));
    }

    private class TestState
    {
        public bool IsEnabled { get; set; }
    }

    private class AIState
    {
        public bool IsLowHealth { get; set; }
        public bool HasTarget { get; set; }
    }
}
