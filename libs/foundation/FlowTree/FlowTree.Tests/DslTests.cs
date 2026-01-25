using System.Collections.Generic;
using System.Linq;
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
    public void Builder_Do_Stateless()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Sequence()
                .Do(() => callCount++)
                .Do(() => callCount++)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_Do_WithState()
    {
        var state = new GameState { Score = 0 };

        var tree = new FlowTree();
        tree.Build(state)
            .Sequence()
                .Do(s => s.Score += 10)
                .Do(s => s.Score += 20)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(30, state.Score);
    }

    [Fact]
    public void Builder_Do_MixedWithAction()
    {
        var state = new GameState { Score = 0 };
        bool actionCalled = false;

        var tree = new FlowTree();
        tree.Build(state)
            .Sequence()
                .Do(s => s.Score = 100)
                .Action(s =>
                {
                    actionCalled = true;
                    return s.Score > 50 ? NodeStatus.Success : NodeStatus.Failure;
                })
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(100, state.Score);
        Assert.True(actionCalled);
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
    public void Builder_WithDecorators_ScopeBased_Retry()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Retry(2)
                .Action(() =>
                {
                    callCount++;
                    return callCount < 2 ? NodeStatus.Failure : NodeStatus.Success;
                })
            .End()
            .Complete();

        // 1回目: Failure → Running
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        // 2回目: Success
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Timeout()
    {
        var tree = new FlowTree();
        tree.Build()
            .Timeout(0.5f)
                .Action(static () => NodeStatus.Running)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Running, tree.Tick(0.3f));
        Assert.Equal(NodeStatus.Failure, tree.Tick(0.3f)); // タイムアウト
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Delay()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Delay(0.5f)
                .Action(() => { callCount++; return NodeStatus.Success; })
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Running, tree.Tick(0.3f)); // 遅延中
        Assert.Equal(0, callCount);

        Assert.Equal(NodeStatus.Success, tree.Tick(0.3f)); // 遅延完了
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Repeat()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .Repeat(3)
                .Do(() => callCount++)
            .End()
            .Complete();

        // 子がSuccessを即座に返すので、1回のTickで3回全部実行される
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_RepeatUntilFail()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build()
            .RepeatUntilFail()
                .Action(() =>
                {
                    callCount++;
                    return callCount < 3 ? NodeStatus.Success : NodeStatus.Failure;
                })
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f)); // 1回目
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f)); // 2回目
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f)); // 3回目でFailure -> Success
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Inverter()
    {
        var tree = new FlowTree();
        tree.Build()
            .Inverter()
                .Action(static () => NodeStatus.Success)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Failure, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Succeeder()
    {
        var tree = new FlowTree();
        tree.Build()
            .Succeeder()
                .Action(static () => NodeStatus.Failure)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Failer()
    {
        var tree = new FlowTree();
        tree.Build()
            .Failer()
                .Action(static () => NodeStatus.Success)
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Failure, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Nested()
    {
        int callCount = 0;

        // Retry内にSequenceを含む複雑なパターン
        // maxRetries=2なので最大3回実行できる（初回+2回リトライ）
        var tree = new FlowTree();
        tree.Build()
            .Retry(2)
                .Sequence()
                    .Do(() => callCount++)
                    .Action(() => callCount < 3 ? NodeStatus.Failure : NodeStatus.Success)
                .End()
            .End()
            .Complete();

        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f)); // 1回目失敗、リトライ
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f)); // 2回目失敗、リトライ
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f)); // 3回目成功
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_WithState()
    {
        var state = new GameState { Score = 0 };

        var tree = new FlowTree();
        tree.Build(state)
            .Repeat(3)
                .Do(s => s.Score += 10)
            .End()
            .Complete();

        // 1回のTickで3回実行される
        tree.Tick(0.016f);
        Assert.Equal(30, state.Score);
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

    private class GameState : IFlowState
    {
        public IFlowState? Parent { get; set; }
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

    private class TestState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public bool IsEnabled { get; set; }
    }

    private class AIState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public bool IsLowHealth { get; set; }
        public bool HasTarget { get; set; }
    }
}

/// <summary>
/// 超複雑なケースのテスト
/// </summary>
public class ComplexFlowTreeTests
{
    // ===========================================
    // State定義
    // ===========================================

    private class GameLoopState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int FrameCount { get; set; }
        public int Score { get; set; }
        public bool IsGameOver { get; set; }
        public bool IsPaused { get; set; }
        public bool WantsToContinue { get; set; } = true;
        public int RetryCount { get; set; }
        public List<string> EventLog { get; } = new();
    }

    private class BattleState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int EnemyHealth { get; set; } = 100;
        public int PlayerHealth { get; set; } = 100;
        public int TurnCount { get; set; }
        public bool IsPlayerTurn { get; set; } = true;
        public string? LastAction { get; set; }
    }

    private class NetworkState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int ConnectionAttempts { get; set; }
        public bool IsConnected { get; set; }
        public bool HasData { get; set; }
        public float ElapsedTime { get; set; }
    }

    // ===========================================
    // 複雑なネストテスト
    // ===========================================

    [Fact]
    public void Complex_DeeplyNestedDecoratorsAndComposites()
    {
        // 深くネストしたデコレータとコンポジットの組み合わせ
        // Retry -> Timeout -> Sequence -> Selector -> RepeatUntilFail -> Inverter -> Action
        var state = new GameLoopState();
        int actionCount = 0;

        var tree = new FlowTree("DeepNest");
        tree.Build(state)
            .Retry(2)
                .Timeout(10.0f)
                    .Sequence()
                        .Do(s => s.EventLog.Add("Sequence Start"))
                        .Selector()
                            .Sequence()
                                .Condition(s => s.FrameCount > 5)
                                .Do(s => s.EventLog.Add("Branch A"))
                            .End()
                            .Sequence()
                                .RepeatUntilFail()
                                    .Inverter()
                                        .Action(s =>
                                        {
                                            actionCount++;
                                            s.FrameCount++;
                                            // 3回目でSuccessを返す（Inverterで反転→Failure→RepeatUntilFail終了）
                                            return actionCount >= 3 ? NodeStatus.Success : NodeStatus.Failure;
                                        })
                                    .End()
                                .End()
                                .Do(s => s.EventLog.Add("Branch B"))
                            .End()
                        .End()
                        .Do(s => s.EventLog.Add("Sequence End"))
                    .End()
                .End()
            .End()
            .Complete();

        // 実行
        var status = tree.Tick(0.016f);
        Assert.Equal(NodeStatus.Running, status);

        status = tree.Tick(0.016f);
        Assert.Equal(NodeStatus.Running, status);

        status = tree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);

        Assert.Equal(3, actionCount);
        Assert.Equal(3, state.FrameCount);
        Assert.Contains("Sequence Start", state.EventLog);
        Assert.Contains("Branch B", state.EventLog);
        Assert.Contains("Sequence End", state.EventLog);
    }

    [Fact]
    public void Complex_SelfRecursiveCountdown()
    {
        // 自己再帰を使ったカウントダウン
        var state = new GameLoopState { FrameCount = 5 };
        var log = new List<int>();

        var countdown = new FlowTree("Countdown");
        countdown
            .WithCallStack(new FlowCallStack(32))
            .Build(state)
            .Selector()
                // 終了条件: カウントが0以下
                .Sequence()
                    .Condition(s => s.FrameCount <= 0)
                    .Do(s => log.Add(-1)) // 終了マーカー
                .End()
                // 再帰: カウントを減らして自己呼び出し
                .Sequence()
                    .Do(s =>
                    {
                        log.Add(s.FrameCount);
                        s.FrameCount--;
                    })
                    .SubTree(countdown) // 自己再帰
                .End()
            .End()
            .Complete();

        var status = countdown.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(new[] { 5, 4, 3, 2, 1, -1 }, log);
    }

    [Fact]
    public void Complex_MutualRecursion_PingPong()
    {
        // 相互再帰: Ping-Pong
        var state = new GameLoopState { FrameCount = 0 };
        var log = new List<string>();

        var pingTree = new FlowTree("Ping");
        var pongTree = new FlowTree("Pong");

        pingTree
            .WithCallStack(new FlowCallStack(32))
            .Build(state)
            .Selector()
                .Sequence()
                    .Condition(s => s.FrameCount >= 6)
                    .Success()
                .End()
                .Sequence()
                    .Do(s =>
                    {
                        log.Add("Ping");
                        s.FrameCount++;
                    })
                    .SubTree(pongTree)
                .End()
            .End()
            .Complete();

        pongTree
            .WithCallStack(new FlowCallStack(32))
            .Build(state)
            .Selector()
                .Sequence()
                    .Condition(s => s.FrameCount >= 6)
                    .Success()
                .End()
                .Sequence()
                    .Do(s =>
                    {
                        log.Add("Pong");
                        s.FrameCount++;
                    })
                    .SubTree(pingTree)
                .End()
            .End()
            .Complete();

        var status = pingTree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(new[] { "Ping", "Pong", "Ping", "Pong", "Ping", "Pong" }, log);
    }

    [Fact]
    public void Complex_StateInjection_ParentChildCommunication()
    {
        // State注入: 親子間での通信
        var parentState = new GameLoopState { Score = 0 };

        var childTree = new FlowTree("Child");
        childTree.Build(new BattleState())
            .Sequence()
                .Do(s =>
                {
                    // 子Stateでバトル処理
                    s.EnemyHealth -= 30;
                    s.TurnCount++;
                })
                .Action(s =>
                {
                    // 親Stateにスコアを加算
                    var parent = (GameLoopState)s.Parent!;
                    parent.Score += 100 * s.TurnCount;
                    return NodeStatus.Success;
                })
            .End()
            .Complete();

        var mainTree = new FlowTree("Main");
        mainTree
            .WithCallStack(new FlowCallStack(16))
            .Build(parentState)
            .Sequence()
                .Do(s => s.EventLog.Add("Start"))
                // 3回バトルを実行（State注入）
                .Repeat(3)
                    .SubTree<BattleState>(childTree, p => new BattleState())
                .End()
                .Do(s => s.EventLog.Add("End"))
            .End()
            .Complete();

        var status = mainTree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(300, parentState.Score); // 100 + 100 + 100
        Assert.Contains("Start", parentState.EventLog);
        Assert.Contains("End", parentState.EventLog);
    }

    [Fact]
    public void Complex_DynamicSubTreeSelection()
    {
        // 動的サブツリー選択
        var state = new GameLoopState { Score = 0 };

        var easyTree = new FlowTree("Easy");
        easyTree.Build(state)
            .Do(s => s.Score += 10)
            .Complete();

        var normalTree = new FlowTree("Normal");
        normalTree.Build(state)
            .Do(s => s.Score += 50)
            .Complete();

        var hardTree = new FlowTree("Hard");
        hardTree.Build(state)
            .Do(s => s.Score += 100)
            .Complete();

        var mainTree = new FlowTree("DynamicSelect");
        mainTree
            .WithCallStack(new FlowCallStack(16))
            .Build(state)
            .Repeat(3)
                .Sequence()
                    .SubTree(s =>
                    {
                        // スコアに応じて難易度を選択
                        if (s.Score >= 100) return hardTree;
                        if (s.Score >= 30) return normalTree;
                        return easyTree;
                    })
                    .Do(s => s.FrameCount++)
                .End()
            .End()
            .Complete();

        var status = mainTree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);
        // 1回目: Easy(+10) -> Score=10
        // 2回目: Easy(+10) -> Score=20
        // 3回目: Easy(+10) -> Score=30 ではない！
        // Repeatは1Tickで全部実行されるので、
        // 1回目: Easy(+10) -> Score=10
        // 2回目: Easy(+10) -> Score=20
        // 3回目: Easy(+10) -> Score=30
        Assert.Equal(30, state.Score);
        Assert.Equal(3, state.FrameCount);
    }

    [Fact]
    public void Complex_EventHandlers_TrackingExecution()
    {
        // Eventハンドラで実行追跡
        var state = new GameLoopState();

        // シンプルなEvent追跡テスト（Event()の後は必ずリーフノード）
        var tree = new FlowTree("EventTracking");
        tree.Build(state)
            .Sequence()
                .Event(
                    onEnter: s => s.EventLog.Add("Action1:Enter"),
                    onExit: (s, result) => s.EventLog.Add($"Action1:Exit({result})"))
                .Do(s => s.Score += 10)
                .Event(
                    onEnter: s => s.EventLog.Add("Action2:Enter"),
                    onExit: (s, result) => s.EventLog.Add($"Action2:Exit({result})"))
                .Do(s => s.Score += 20)
            .End()
            .Complete();

        var status = tree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(30, state.Score);

        // すべてのイベントがログされていることを確認
        Assert.Contains("Action1:Enter", state.EventLog);
        Assert.Contains("Action1:Exit(Success)", state.EventLog);
        Assert.Contains("Action2:Enter", state.EventLog);
        Assert.Contains("Action2:Exit(Success)", state.EventLog);
    }

    [Fact]
    public void Complex_RetryWithTimeout_NetworkSimulation()
    {
        // ネットワーク接続シミュレーション: リトライ + タイムアウト
        var state = new NetworkState();

        var tree = new FlowTree("NetworkConnect");
        tree.Build(state)
            .Retry(3)
                .Timeout(1.0f)
                    .Sequence()
                        .Do(s => s.ConnectionAttempts++)
                        // 3回目で接続成功
                        .Action(s =>
                        {
                            if (s.ConnectionAttempts >= 3)
                            {
                                s.IsConnected = true;
                                return NodeStatus.Success;
                            }
                            return NodeStatus.Failure; // 失敗してリトライ
                        })
                    .End()
                .End()
            .End()
            .Complete();

        // 1回目: 失敗 -> Running (リトライ)
        Assert.Equal(NodeStatus.Running, tree.Tick(0.1f));
        Assert.Equal(1, state.ConnectionAttempts);

        // 2回目: 失敗 -> Running (リトライ)
        Assert.Equal(NodeStatus.Running, tree.Tick(0.1f));
        Assert.Equal(2, state.ConnectionAttempts);

        // 3回目: 成功
        Assert.Equal(NodeStatus.Success, tree.Tick(0.1f));
        Assert.Equal(3, state.ConnectionAttempts);
        Assert.True(state.IsConnected);
    }

    [Fact]
    public void Complex_GameLoop_WithPauseAndRetry()
    {
        // ゲームループ: ポーズ対応、リトライ機能付き
        var state = new GameLoopState();

        var gameLoopTree = new FlowTree("GameLoop");
        gameLoopTree.Build(state)
            .RepeatUntilFail()
                .Action(s =>
                {
                    // ポーズ中は待機
                    if (s.IsPaused)
                        return NodeStatus.Running;

                    // ゲームオーバー処理
                    if (s.IsGameOver)
                    {
                        if (s.WantsToContinue)
                        {
                            s.IsGameOver = false;
                            s.RetryCount++;
                            return NodeStatus.Success; // ループ継続
                        }
                        return NodeStatus.Failure; // ループ終了
                    }

                    // 通常処理
                    s.FrameCount++;
                    s.Score += 10;
                    return NodeStatus.Success; // ループ継続
                })
            .End()
            .Complete();

        // 通常フレーム実行
        Assert.Equal(NodeStatus.Running, gameLoopTree.Tick(0.016f));
        Assert.Equal(1, state.FrameCount);

        Assert.Equal(NodeStatus.Running, gameLoopTree.Tick(0.016f));
        Assert.Equal(2, state.FrameCount);

        // ポーズ
        state.IsPaused = true;
        Assert.Equal(NodeStatus.Running, gameLoopTree.Tick(0.016f));
        Assert.Equal(2, state.FrameCount); // 進まない

        // ポーズ解除
        state.IsPaused = false;
        Assert.Equal(NodeStatus.Running, gameLoopTree.Tick(0.016f));
        Assert.Equal(3, state.FrameCount);

        // ゲームオーバー → リトライ
        state.IsGameOver = true;
        Assert.Equal(NodeStatus.Running, gameLoopTree.Tick(0.016f));
        Assert.Equal(1, state.RetryCount);
        Assert.False(state.IsGameOver);
        Assert.Equal(3, state.FrameCount); // フレームは進まない

        // 継続
        Assert.Equal(NodeStatus.Running, gameLoopTree.Tick(0.016f));
        Assert.Equal(4, state.FrameCount);

        // ゲームオーバー → 終了
        state.IsGameOver = true;
        state.WantsToContinue = false;
        Assert.Equal(NodeStatus.Success, gameLoopTree.Tick(0.016f));
    }

    [Fact]
    public void Complex_TurnBasedBattle()
    {
        // ターン制バトル
        var state = new BattleState
        {
            PlayerHealth = 100,
            EnemyHealth = 50
        };

        var battleTree = new FlowTree("Battle");
        battleTree.Build(state)
            .RepeatUntilFail()
                .Sequence()
                    // 勝敗判定
                    .Inverter()
                        .Selector()
                            .Condition(s => s.PlayerHealth <= 0)
                            .Condition(s => s.EnemyHealth <= 0)
                        .End()
                    .End()
                    // ターン処理
                    .Selector()
                        // プレイヤーターン
                        .Sequence()
                            .Condition(s => s.IsPlayerTurn)
                            .Do(s =>
                            {
                                s.EnemyHealth -= 20;
                                s.LastAction = "PlayerAttack";
                                s.IsPlayerTurn = false;
                                s.TurnCount++;
                            })
                        .End()
                        // 敵ターン
                        .Sequence()
                            .Condition(s => !s.IsPlayerTurn)
                            .Do(s =>
                            {
                                s.PlayerHealth -= 15;
                                s.LastAction = "EnemyAttack";
                                s.IsPlayerTurn = true;
                            })
                        .End()
                    .End()
                .End()
            .End()
            .Complete();

        // バトル実行
        while (battleTree.Tick(0.016f) == NodeStatus.Running)
        {
            // バトル継続
        }

        // 敵を倒したはず (50HP / 20ダメージ = 3ターン)
        Assert.True(state.EnemyHealth <= 0);
        Assert.Equal(3, state.TurnCount);
    }

    [Fact]
    public void Complex_ParallelWithJoin_ResourceLoading()
    {
        // 並列ロード + 全完了待機
        int frameCount = 0;
        var loadedResources = new List<string>();
        var eventLog = new List<string>();

        var tree = new FlowTree("ResourceLoading");
        tree.Build()
            .Sequence()
                .Do(() => eventLog.Add("LoadStart"))
                .Join()
                    // テクスチャロード（2フレーム目で完了）
                    .Action(() =>
                    {
                        if (frameCount < 2) return NodeStatus.Running;
                        if (!loadedResources.Contains("Textures"))
                            loadedResources.Add("Textures");
                        return NodeStatus.Success;
                    })
                    // サウンドロード（3フレーム目で完了）
                    .Action(() =>
                    {
                        if (frameCount < 3) return NodeStatus.Running;
                        if (!loadedResources.Contains("Sounds"))
                            loadedResources.Add("Sounds");
                        return NodeStatus.Success;
                    })
                    // データロード（即座に完了）
                    .Do(() =>
                    {
                        if (!loadedResources.Contains("Data"))
                            loadedResources.Add("Data");
                    })
                .End()
                .Do(() => eventLog.Add("LoadComplete"))
            .End()
            .Complete();

        // フレーム0
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        frameCount++;
        Assert.Single(loadedResources); // Dataのみ

        // フレーム1
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        frameCount++;
        Assert.Single(loadedResources); // まだDataのみ

        // フレーム2
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        frameCount++;
        Assert.Equal(2, loadedResources.Count); // Data, Textures

        // フレーム3
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(3, loadedResources.Count); // 全部
        Assert.Contains("LoadComplete", eventLog);
    }

    [Fact]
    public void Complex_RaceWithTimeout_FirstWins()
    {
        // Race: 最初に完了した方が勝ち
        int frameCount = 0;
        string winner = "";

        var tree = new FlowTree("Race");
        tree.Build()
            .Race()
                // 遅いタスク（5フレーム）
                .Action(() =>
                {
                    if (frameCount < 5) return NodeStatus.Running;
                    winner = "SlowTask";
                    return NodeStatus.Success;
                })
                // 速いタスク（2フレーム）
                .Action(() =>
                {
                    if (frameCount < 2) return NodeStatus.Running;
                    winner = "FastTask";
                    return NodeStatus.Success;
                })
            .End()
            .Complete();

        // フレーム0
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        frameCount++;

        // フレーム1
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        frameCount++;

        // フレーム2 - FastTaskが勝利
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal("FastTask", winner);
    }

    [Fact]
    public void Complex_ShuffledSelector_NoRepeatUntilExhausted()
    {
        // ShuffledSelector: 1Tickで1つの子を試す（シャッフル順）
        // 全選択肢を一巡するまで同じものを選ばない
        var tried = new List<int>();

        var tree = new FlowTree("Shuffle");
        tree.Build()
            .ShuffledSelector()
                .Action(() => { tried.Add(1); return NodeStatus.Failure; })
                .Action(() => { tried.Add(2); return NodeStatus.Failure; })
                .Action(() => { tried.Add(3); return NodeStatus.Failure; })
            .End()
            .Complete();

        // 各Tickで1つの子が試される
        tree.Tick(0.016f);
        Assert.Equal(1, tried.Count);

        tree.Tick(0.016f);
        Assert.Equal(2, tried.Count);

        tree.Tick(0.016f);
        Assert.Equal(3, tried.Count);

        // 3つ全てが試されたはず（シャッフルされた順番で重複なし）
        Assert.Contains(1, tried);
        Assert.Contains(2, tried);
        Assert.Contains(3, tried);

        // 4回目のTickでは最初からやり直し（再シャッフル）
        tree.Tick(0.016f);
        Assert.Equal(4, tried.Count);
    }

    [Fact]
    public void Complex_WeightedRandom_ProbabilisticSelection()
    {
        // WeightedRandomSelector: 重み付きランダム選択
        var selections = new Dictionary<string, int>
        {
            ["Common"] = 0,
            ["Rare"] = 0,
            ["Legendary"] = 0
        };

        var tree = new FlowTree("WeightedRandom");
        tree.Build()
            .WeightedRandomSelector()
                .Weighted(70.0f, new ActionNode(() => { selections["Common"]++; return NodeStatus.Success; }))
                .Weighted(25.0f, new ActionNode(() => { selections["Rare"]++; return NodeStatus.Success; }))
                .Weighted(5.0f, new ActionNode(() => { selections["Legendary"]++; return NodeStatus.Success; }))
            .End()
            .Complete();

        // 1000回実行
        for (int i = 0; i < 1000; i++)
        {
            tree.Tick(0.016f);
            tree.Reset();
        }

        // 大まかな確率チェック（完全な確率は保証できないが傾向は確認）
        Assert.True(selections["Common"] > selections["Rare"]);
        Assert.True(selections["Rare"] > selections["Legendary"]);
        Assert.True(selections["Common"] > 500); // 70%なら700前後のはず
    }

    [Fact]
    public void Complex_RoundRobin_CyclesThrough()
    {
        // RoundRobin: 順番に選択
        var state = new GameLoopState();
        var selections = new List<string>();

        var tree = new FlowTree("RoundRobin");
        tree.Build(state)
            .RoundRobin()
                .Do(s => selections.Add("A"))
                .Do(s => selections.Add("B"))
                .Do(s => selections.Add("C"))
            .End()
            .Complete();

        // 6回実行
        for (int i = 0; i < 6; i++)
        {
            tree.Tick(0.016f);
            tree.Reset();
        }

        // A, B, C, A, B, C の順
        Assert.Equal(new[] { "A", "B", "C", "A", "B", "C" }, selections);
    }

    [Fact]
    public void Complex_AllFeaturesIntegration()
    {
        // 全機能統合テスト: 複雑なゲームシーン遷移
        var state = new GameLoopState { WantsToContinue = true };

        var titleScene = new FlowTree("Title");
        var gameScene = new FlowTree("Game");
        var resultScene = new FlowTree("Result");

        // タイトル画面
        titleScene.Build(state)
            .Sequence()
                .Do(s => s.EventLog.Add("Title:Show"))
                .Do(s => s.EventLog.Add("Title:Hide"))
            .End()
            .Complete();

        // ゲーム画面（親Stateを使用）
        gameScene.Build(state)
            .Sequence()
                .Do(s => s.EventLog.Add("Game:Start"))
                .Repeat(3)
                    .Do(s => s.FrameCount++)
                .End()
                .Do(s =>
                {
                    s.Score += 1000;
                    s.EventLog.Add("Game:End");
                })
            .End()
            .Complete();

        // リザルト画面
        resultScene.Build(state)
            .Sequence()
                .Do(s =>
                {
                    s.EventLog.Add($"Result:Score={s.Score}");
                    s.WantsToContinue = s.RetryCount < 1;
                    s.RetryCount++;
                })
            .End()
            .Complete();

        // メインフロー
        var mainFlow = new FlowTree("Main");
        mainFlow
            .WithCallStack(new FlowCallStack(32))
            .Build(state)
            .Sequence()
                // タイトル
                .SubTree(titleScene)
                // ゲームループ
                .RepeatUntilFail()
                    .Sequence()
                        .SubTree(gameScene)
                        .SubTree(resultScene)
                        .Condition(s => s.WantsToContinue)
                    .End()
                .End()
                .Do(s => s.EventLog.Add("Exit"))
            .End()
            .Complete();

        // SubTreeは複数Tickが必要な場合がある
        NodeStatus status;
        int tickCount = 0;
        do
        {
            status = mainFlow.Tick(0.016f);
            tickCount++;
        } while (status == NodeStatus.Running && tickCount < 100);

        Assert.Equal(NodeStatus.Success, status);

        // 実行順序を確認
        var expected = new[]
        {
            "Title:Show",
            "Title:Hide",
            "Game:Start",
            "Game:End",
            "Result:Score=1000",
            "Game:Start",
            "Game:End",
            "Result:Score=2000",
            "Exit"
        };
        Assert.Equal(expected, state.EventLog);
        Assert.Equal(2000, state.Score);
    }
}
