using Xunit;

namespace Tomato.FlowTree.Tests;

public class DecoratorNodeTests
{
    [Fact]
    public void InverterNode_InvertsSuccess()
    {
        var inverter = new InverterNode(
            new ActionNode(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Failure, inverter.Tick(ref ctx));
    }

    [Fact]
    public void InverterNode_InvertsFailure()
    {
        var inverter = new InverterNode(
            new ActionNode(static () => NodeStatus.Failure)
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Success, inverter.Tick(ref ctx));
    }

    [Fact]
    public void InverterNode_PassesRunning()
    {
        var inverter = new InverterNode(
            new ActionNode(static () => NodeStatus.Running)
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Running, inverter.Tick(ref ctx));
    }

    [Fact]
    public void SucceederNode_AlwaysSuccess()
    {
        var succeeder = new SucceederNode(
            new ActionNode(static () => NodeStatus.Failure)
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Success, succeeder.Tick(ref ctx));
    }

    [Fact]
    public void FailerNode_AlwaysFailure()
    {
        var failer = new FailerNode(
            new ActionNode(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Failure, failer.Tick(ref ctx));
    }

    [Fact]
    public void RepeatNode_RepeatsCount()
    {
        int callCount = 0;
        var repeat = new RepeatNode(3,
            new ActionNode(() => { callCount++; return NodeStatus.Success; })
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Success, repeat.Tick(ref ctx));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void RepeatNode_StopsOnFailure()
    {
        int callCount = 0;
        var repeat = new RepeatNode(3,
            new ActionNode(() =>
            {
                callCount++;
                return callCount == 2 ? NodeStatus.Failure : NodeStatus.Success;
            })
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Failure, repeat.Tick(ref ctx));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void RepeatUntilFailNode_RepeatsUntilFail()
    {
        int callCount = 0;
        var repeat = new RepeatUntilFailNode(
            new ActionNode(() =>
            {
                callCount++;
                return callCount < 3 ? NodeStatus.Success : NodeStatus.Failure;
            })
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目: Running（Successで継続）
        Assert.Equal(NodeStatus.Running, repeat.Tick(ref ctx));
        Assert.Equal(1, callCount);

        // 2回目: Running
        Assert.Equal(NodeStatus.Running, repeat.Tick(ref ctx));
        Assert.Equal(2, callCount);

        // 3回目: Success（Failureで終了）
        Assert.Equal(NodeStatus.Success, repeat.Tick(ref ctx));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void RepeatUntilSuccessNode_RepeatsUntilSuccess()
    {
        int callCount = 0;
        var repeat = new RepeatUntilSuccessNode(
            new ActionNode(() =>
            {
                callCount++;
                return callCount < 3 ? NodeStatus.Failure : NodeStatus.Success;
            })
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目: Running（Failureで継続）
        Assert.Equal(NodeStatus.Running, repeat.Tick(ref ctx));
        Assert.Equal(1, callCount);

        // 2回目: Running
        Assert.Equal(NodeStatus.Running, repeat.Tick(ref ctx));
        Assert.Equal(2, callCount);

        // 3回目: Success
        Assert.Equal(NodeStatus.Success, repeat.Tick(ref ctx));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void RetryNode_RetriesOnFailure()
    {
        int callCount = 0;
        var retry = new RetryNode(2,
            new ActionNode(() =>
            {
                callCount++;
                return callCount < 3 ? NodeStatus.Failure : NodeStatus.Success;
            })
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目失敗 → Running
        Assert.Equal(NodeStatus.Running, retry.Tick(ref ctx));
        Assert.Equal(1, callCount);

        // 2回目失敗 → Running
        Assert.Equal(NodeStatus.Running, retry.Tick(ref ctx));
        Assert.Equal(2, callCount);

        // 3回目成功
        Assert.Equal(NodeStatus.Success, retry.Tick(ref ctx));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void RetryNode_FailsAfterMaxRetries()
    {
        int callCount = 0;
        var retry = new RetryNode(2,
            new ActionNode(() => { callCount++; return NodeStatus.Failure; })
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目失敗
        Assert.Equal(NodeStatus.Running, retry.Tick(ref ctx));
        Assert.Equal(1, callCount);

        // 2回目失敗
        Assert.Equal(NodeStatus.Running, retry.Tick(ref ctx));
        Assert.Equal(2, callCount);

        // 3回目（リトライ上限超過）
        Assert.Equal(NodeStatus.Failure, retry.Tick(ref ctx));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void TimeoutNode_Timeouts()
    {
        var timeout = new TimeoutNode(0.5f,
            new ActionNode(static () => NodeStatus.Running)
        );

        var ctx = new FlowContext { DeltaTime = 0.2f };

        // 0.2秒経過
        Assert.Equal(NodeStatus.Running, timeout.Tick(ref ctx));

        // 0.4秒経過
        Assert.Equal(NodeStatus.Running, timeout.Tick(ref ctx));

        // 0.6秒経過 → タイムアウト
        Assert.Equal(NodeStatus.Failure, timeout.Tick(ref ctx));
    }

    [Fact]
    public void TimeoutNode_CompletesBeforeTimeout()
    {
        var timeout = new TimeoutNode(1.0f,
            new ActionNode(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Success, timeout.Tick(ref ctx));
    }

    [Fact]
    public void DelayNode_DelaysExecution()
    {
        int callCount = 0;
        var delay = new DelayNode(0.5f,
            new ActionNode(() => { callCount++; return NodeStatus.Success; })
        );

        var ctx = new FlowContext { DeltaTime = 0.2f };

        // 0.2秒経過（遅延中）
        Assert.Equal(NodeStatus.Running, delay.Tick(ref ctx));
        Assert.Equal(0, callCount);

        // 0.4秒経過（遅延中）
        Assert.Equal(NodeStatus.Running, delay.Tick(ref ctx));
        Assert.Equal(0, callCount);

        // 0.6秒経過（遅延完了、子ノード実行）
        Assert.Equal(NodeStatus.Success, delay.Tick(ref ctx));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void GuardNode_ExecutesWhenConditionTrue()
    {
        var state = new TestState { IsEnabled = false };
        var callCount = 0;

        var guard = new GuardNode<TestState>(
            s => s.IsEnabled,
            new ActionNode(() => { callCount++; return NodeStatus.Success; })
        );

        var ctx = new FlowContext { State = state, DeltaTime = 0.1f };

        // 条件がfalse
        Assert.Equal(NodeStatus.Failure, guard.Tick(ref ctx));
        Assert.Equal(0, callCount);

        // 条件をtrueに
        state.IsEnabled = true;
        Assert.Equal(NodeStatus.Success, guard.Tick(ref ctx));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void GuardNode_ConditionCheckedOnlyAtStart()
    {
        var state = new TestState { IsEnabled = true };

        var guard = new GuardNode<TestState>(
            s => s.IsEnabled,
            new ActionNode(static () => NodeStatus.Running)
        );

        var ctx = new FlowContext { State = state, DeltaTime = 0.1f };

        // 条件がtrue → Running
        Assert.Equal(NodeStatus.Running, guard.Tick(ref ctx));

        // 条件をfalseに（実行中は再チェックされない）
        state.IsEnabled = false;
        Assert.Equal(NodeStatus.Running, guard.Tick(ref ctx));
    }

    // =====================================================
    // EventNode Tests
    // =====================================================

    [Fact]
    public void EventNode_OnEnterFiredOnFirstTick()
    {
        int enterCount = 0;
        var eventNode = new EventNode(
            new ActionNode(static () => NodeStatus.Success),
            onEnter: () => { enterCount++; }
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目: OnEnterが発火
        eventNode.Tick(ref ctx);
        Assert.Equal(1, enterCount);

        // 2回目: 新しい実行なのでOnEnterが発火
        eventNode.Tick(ref ctx);
        Assert.Equal(2, enterCount);
    }

    [Fact]
    public void EventNode_OnEnterFiredOnceWhileRunning()
    {
        int enterCount = 0;
        int tickCount = 0;
        var eventNode = new EventNode(
            new ActionNode(() =>
            {
                tickCount++;
                return tickCount < 3 ? NodeStatus.Running : NodeStatus.Success;
            }),
            onEnter: () => { enterCount++; }
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目: Running、OnEnterが発火
        eventNode.Tick(ref ctx);
        Assert.Equal(1, enterCount);

        // 2回目: Running中、OnEnterは発火しない
        eventNode.Tick(ref ctx);
        Assert.Equal(1, enterCount);

        // 3回目: Success、OnEnterは発火しない
        eventNode.Tick(ref ctx);
        Assert.Equal(1, enterCount);
    }

    [Fact]
    public void EventNode_OnExitFiredOnCompletion()
    {
        int exitCount = 0;
        NodeStatus? exitResult = null;
        var eventNode = new EventNode(
            new ActionNode(static () => NodeStatus.Success),
            onExit: result => { exitCount++; exitResult = result; }
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        eventNode.Tick(ref ctx);
        Assert.Equal(1, exitCount);
        Assert.Equal(NodeStatus.Success, exitResult);
    }

    [Fact]
    public void EventNode_OnExitNotFiredWhileRunning()
    {
        int exitCount = 0;
        int tickCount = 0;
        var eventNode = new EventNode(
            new ActionNode(() =>
            {
                tickCount++;
                return tickCount < 2 ? NodeStatus.Running : NodeStatus.Failure;
            }),
            onExit: _ => { exitCount++; }
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目: Running、OnExitは発火しない
        eventNode.Tick(ref ctx);
        Assert.Equal(0, exitCount);

        // 2回目: Failure、OnExitが発火
        eventNode.Tick(ref ctx);
        Assert.Equal(1, exitCount);
    }

    [Fact]
    public void EventNode_BothEventsWork()
    {
        int enterCount = 0;
        int exitCount = 0;
        var eventNode = new EventNode(
            new ActionNode(static () => NodeStatus.Success),
            onEnter: () => { enterCount++; },
            onExit: _ => { exitCount++; }
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };

        eventNode.Tick(ref ctx);
        Assert.Equal(1, enterCount);
        Assert.Equal(1, exitCount);
    }

    [Fact]
    public void EventNode_PassesThroughChildStatus()
    {
        var eventNode = new EventNode(
            new ActionNode(static () => NodeStatus.Failure)
        );

        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Failure, eventNode.Tick(ref ctx));
    }

    // =====================================================
    // Event DSL (Fluent) Tests
    // =====================================================

    [Fact]
    public void EventDsl_WrapsNextNode()
    {
        int enterCount = 0;
        int exitCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Event(
                onEnter: () => { enterCount++; },
                onExit: _ => { exitCount++; })
            .Action(static () => NodeStatus.Success)
            .Complete();

        tree.Tick(0.016f);

        Assert.Equal(1, enterCount);
        Assert.Equal(1, exitCount);
    }

    [Fact]
    public void EventDsl_WrapsCompositeNode()
    {
        int enterCount = 0;
        int actionCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Event(onEnter: () => { enterCount++; })
            .Sequence()
                .Action(() => { actionCount++; return NodeStatus.Success; })
                .Action(() => { actionCount++; return NodeStatus.Success; })
            .End()
            .Complete();

        tree.Tick(0.016f);

        Assert.Equal(1, enterCount);
        Assert.Equal(2, actionCount);
    }

    [Fact]
    public void EventDsl_WorksWithRunningNode()
    {
        int enterCount = 0;
        int exitCount = 0;
        int tickCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Event(
                onEnter: () => { enterCount++; },
                onExit: _ => { exitCount++; })
            .Action(() =>
            {
                tickCount++;
                return tickCount < 3 ? NodeStatus.Running : NodeStatus.Success;
            })
            .Complete();

        // 1回目: Running、OnEnter発火
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        Assert.Equal(1, enterCount);
        Assert.Equal(0, exitCount);

        // 2回目: Running、イベント発火なし
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        Assert.Equal(1, enterCount);
        Assert.Equal(0, exitCount);

        // 3回目: Success、OnExit発火
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(1, enterCount);
        Assert.Equal(1, exitCount);
    }

    // =====================================================
    // Generic Event Tests
    // =====================================================

    [Fact]
    public void EventNode_Generic_OnEnterWithState()
    {
        var state = new TestState { Counter = 0 };
        var eventNode = new EventNode<TestState>(
            new ActionNode(static () => NodeStatus.Success),
            onEnter: s => { s.Counter++; }
        );

        var ctx = new FlowContext { State = state, DeltaTime = 0.1f };

        eventNode.Tick(ref ctx);
        Assert.Equal(1, state.Counter);

        eventNode.Tick(ref ctx);
        Assert.Equal(2, state.Counter);
    }

    [Fact]
    public void EventDsl_Generic_WithState()
    {
        var state = new TestState { Counter = 0 };

        var tree = new FlowTree();
        tree.Build(state)
            .Event(
                onEnter: s => { s.Counter++; },
                onExit: (s, _) => { s.Counter += 10; })
            .Action(static () => NodeStatus.Success)
            .Complete();

        tree.Tick(0.016f);

        Assert.Equal(11, state.Counter);
    }

    private class TestState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public bool IsEnabled { get; set; }
        public int Counter { get; set; }
    }
}

public class LeafNodeAdditionalTests
{
    [Fact]
    public void WaitNode_Waits()
    {
        var wait = new WaitNode(0.5f);
        var ctx = new FlowContext { DeltaTime = 0.2f };

        Assert.Equal(NodeStatus.Running, wait.Tick(ref ctx));
        Assert.Equal(NodeStatus.Running, wait.Tick(ref ctx));
        Assert.Equal(NodeStatus.Success, wait.Tick(ref ctx)); // 0.6秒
    }

    [Fact]
    public void YieldNode_YieldsOnce()
    {
        var yield = new YieldNode();
        var ctx = new FlowContext { DeltaTime = 0.1f };

        Assert.Equal(NodeStatus.Running, yield.Tick(ref ctx));
        Assert.Equal(NodeStatus.Success, yield.Tick(ref ctx));
    }

    [Fact]
    public void SuccessNode_AlwaysSuccess()
    {
        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Success, SuccessNode.Instance.Tick(ref ctx));
    }

    [Fact]
    public void FailureNode_AlwaysFailure()
    {
        var ctx = new FlowContext { DeltaTime = 0.1f };
        Assert.Equal(NodeStatus.Failure, FailureNode.Instance.Tick(ref ctx));
    }
}
