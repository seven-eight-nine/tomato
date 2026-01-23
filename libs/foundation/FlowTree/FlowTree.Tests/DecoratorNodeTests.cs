using Xunit;

namespace Tomato.FlowTree.Tests;

public class DecoratorNodeTests
{
    private static FlowContext CreateContext(float deltaTime = 0.1f)
    {
        var ctx = FlowContext.Create(new Blackboard(), deltaTime);
        return ctx;
    }

    [Fact]
    public void InverterNode_InvertsSuccess()
    {
        var inverter = new InverterNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, inverter.Tick(ref ctx));
    }

    [Fact]
    public void InverterNode_InvertsFailure()
    {
        var inverter = new InverterNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, inverter.Tick(ref ctx));
    }

    [Fact]
    public void InverterNode_PassesRunning()
    {
        var inverter = new InverterNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Running, inverter.Tick(ref ctx));
    }

    [Fact]
    public void SucceederNode_AlwaysSuccess()
    {
        var succeeder = new SucceederNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Failure)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, succeeder.Tick(ref ctx));
    }

    [Fact]
    public void FailerNode_AlwaysFailure()
    {
        var failer = new FailerNode(
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, failer.Tick(ref ctx));
    }

    [Fact]
    public void RepeatNode_RepeatsCount()
    {
        int callCount = 0;
        var repeat = new RepeatNode(3,
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, repeat.Tick(ref ctx));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void RepeatNode_StopsOnFailure()
    {
        int callCount = 0;
        var repeat = new RepeatNode(3,
            new ActionNode((ref FlowContext _) =>
            {
                callCount++;
                return callCount == 2 ? NodeStatus.Failure : NodeStatus.Success;
            })
        );

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, repeat.Tick(ref ctx));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void RepeatUntilFailNode_RepeatsUntilFail()
    {
        int callCount = 0;
        var repeat = new RepeatUntilFailNode(
            new ActionNode((ref FlowContext _) =>
            {
                callCount++;
                return callCount < 3 ? NodeStatus.Success : NodeStatus.Failure;
            })
        );

        var ctx = CreateContext();

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
            new ActionNode((ref FlowContext _) =>
            {
                callCount++;
                return callCount < 3 ? NodeStatus.Failure : NodeStatus.Success;
            })
        );

        var ctx = CreateContext();

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
            new ActionNode((ref FlowContext _) =>
            {
                callCount++;
                return callCount < 3 ? NodeStatus.Failure : NodeStatus.Success;
            })
        );

        var ctx = CreateContext();

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
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Failure; })
        );

        var ctx = CreateContext();

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
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running)
        );

        var ctx = CreateContext(0.2f);

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
            new ActionNode(static (ref FlowContext _) => NodeStatus.Success)
        );

        var ctx = CreateContext(0.1f);
        Assert.Equal(NodeStatus.Success, timeout.Tick(ref ctx));
    }

    [Fact]
    public void DelayNode_DelaysExecution()
    {
        int callCount = 0;
        var delay = new DelayNode(0.5f,
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
        );

        var ctx = CreateContext(0.2f);

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
        var key = new BlackboardKey<bool>(1);
        var callCount = 0;

        var guard = new GuardNode(
            (ref FlowContext ctx) => ctx.Blackboard.GetBool(key),
            new ActionNode((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
        );

        var ctx = CreateContext();

        // 条件がfalse
        Assert.Equal(NodeStatus.Failure, guard.Tick(ref ctx));
        Assert.Equal(0, callCount);

        // 条件をtrueに
        ctx.Blackboard.SetBool(key, true);
        Assert.Equal(NodeStatus.Success, guard.Tick(ref ctx));
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void GuardNode_ConditionCheckedOnlyAtStart()
    {
        var key = new BlackboardKey<bool>(1);

        var guard = new GuardNode(
            (ref FlowContext ctx) => ctx.Blackboard.GetBool(key),
            new ActionNode(static (ref FlowContext _) => NodeStatus.Running)
        );

        var ctx = CreateContext();
        ctx.Blackboard.SetBool(key, true);

        // 条件がtrue → Running
        Assert.Equal(NodeStatus.Running, guard.Tick(ref ctx));

        // 条件をfalseに（実行中は再チェックされない）
        ctx.Blackboard.SetBool(key, false);
        Assert.Equal(NodeStatus.Running, guard.Tick(ref ctx));
    }
}

public class LeafNodeAdditionalTests
{
    private static FlowContext CreateContext(float deltaTime = 0.1f)
    {
        var ctx = FlowContext.Create(new Blackboard(), deltaTime);
        return ctx;
    }

    [Fact]
    public void WaitNode_Waits()
    {
        var wait = new WaitNode(0.5f);
        var ctx = CreateContext(0.2f);

        Assert.Equal(NodeStatus.Running, wait.Tick(ref ctx));
        Assert.Equal(NodeStatus.Running, wait.Tick(ref ctx));
        Assert.Equal(NodeStatus.Success, wait.Tick(ref ctx)); // 0.6秒
    }

    [Fact]
    public void YieldNode_YieldsOnce()
    {
        var yield = new YieldNode();
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Running, yield.Tick(ref ctx));
        Assert.Equal(NodeStatus.Success, yield.Tick(ref ctx));
    }

    [Fact]
    public void SuccessNode_AlwaysSuccess()
    {
        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, SuccessNode.Instance.Tick(ref ctx));
    }

    [Fact]
    public void FailureNode_AlwaysFailure()
    {
        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Failure, FailureNode.Instance.Tick(ref ctx));
    }
}
