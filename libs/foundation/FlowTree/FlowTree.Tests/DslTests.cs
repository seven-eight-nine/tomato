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
        tree.Build(
            Sequence(
                Action(() => { callCount++; return NodeStatus.Success; }),
                Action(() => { callCount++; return NodeStatus.Success; })
            )
        );

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_Do_Stateless()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build(
            Sequence(
                Do(() => callCount++),
                Do(() => callCount++)
            )
        );

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_Do_WithState()
    {
        var state = new GameState { Score = 0 };

        var tree = new FlowTree();
        tree.Build(state, 
                Sequence(
                    Do<GameState>(s => s.Score += 10),
                    Do<GameState>(s => s.Score += 20)
                )
            );

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(30, state.Score);
    }

    [Fact]
    public void Builder_Do_MixedWithAction()
    {
        var state = new GameState { Score = 0 };
        bool actionCalled = false;

        var tree = new FlowTree();
        tree.Build(state, 
                Sequence(
                    Do<GameState>(s => s.Score = 100),
                    Action<GameState>(s =>
                    {
                        actionCalled = true;
                        return s.Score > 50 ? NodeStatus.Success : NodeStatus.Failure;
                    })
                )
            );

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(100, state.Score);
        Assert.True(actionCalled);
    }

    [Fact]
    public void Builder_NestedComposites()
    {
        var executed = new bool[3];

        var tree = new FlowTree();
        tree.Build(
            Sequence(
                Selector(
                    Action(() => { executed[0] = true; return NodeStatus.Failure; }),
                    Action(() => { executed[1] = true; return NodeStatus.Success; })
                ),
                Action(() => { executed[2] = true; return NodeStatus.Success; })
            )
        );

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
        tree.Build(
            Retry(2,
                Action(() =>
                {
                    callCount++;
                    return callCount < 2 ? NodeStatus.Failure : NodeStatus.Success;
                })
            )
        );

        // 1回目: Failure -> Running
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        // 2回目: Success
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(2, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Timeout()
    {
        var tree = new FlowTree();
        tree.Build(
            Timeout(0.5f,
                Action(static () => NodeStatus.Running)
            )
        );

        Assert.Equal(NodeStatus.Running, tree.Tick(0.3f));
        Assert.Equal(NodeStatus.Failure, tree.Tick(0.3f)); // タイムアウト
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Delay()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build(
            Delay(0.5f,
                Action(() => { callCount++; return NodeStatus.Success; })
            )
        );

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
        tree.Build(
            Repeat(3, Do(() => callCount++))
        );

        // 子がSuccessを即座に返すので、1回のTickで3回全部実行される
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_RepeatUntilFail()
    {
        int callCount = 0;

        var tree = new FlowTree();
        tree.Build(
            RepeatUntilFail(
                Action(() =>
                {
                    callCount++;
                    return callCount < 3 ? NodeStatus.Success : NodeStatus.Failure;
                })
            )
        );

        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f)); // 1回目
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f)); // 2回目
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f)); // 3回目でFailure -> Success
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Inverter()
    {
        var tree = new FlowTree();
        tree.Build(
            Inverter(Action(static () => NodeStatus.Success))
        );

        Assert.Equal(NodeStatus.Failure, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Succeeder()
    {
        var tree = new FlowTree();
        tree.Build(
            Succeeder(Action(static () => NodeStatus.Failure))
        );

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Failer()
    {
        var tree = new FlowTree();
        tree.Build(
            Failer(Action(static () => NodeStatus.Success))
        );

        Assert.Equal(NodeStatus.Failure, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_WithDecorators_ScopeBased_Nested()
    {
        int callCount = 0;

        // Retry内にSequenceを含む複雑なパターン
        // maxRetries=2なので最大3回実行できる（初回+2回リトライ）
        var tree = new FlowTree();
        tree.Build(
            Retry(2,
                Sequence(
                    Do(() => callCount++),
                    Action(() => callCount < 3 ? NodeStatus.Failure : NodeStatus.Success)
                )
            )
        );

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
        tree.Build(state, 
                Repeat(3, Do<GameState>(s => s.Score += 10))
            );

        // 1回のTickで3回実行される
        tree.Tick(0.016f);
        Assert.Equal(30, state.Score);
    }

    [Fact]
    public void Builder_WithSubTree()
    {
        var executed = new bool[2];

        var subTree = new FlowTree("SubTree");
        subTree.Build(
            Action(() => { executed[1] = true; return NodeStatus.Success; })
        );

        var mainTree = new FlowTree("MainTree");
        mainTree
            .WithCallStack(new FlowCallStack(16))
            .Build(
                Sequence(
                    Action(() => { executed[0] = true; return NodeStatus.Success; }),
                    SubTree(subTree)
                )
            );

        Assert.Equal(NodeStatus.Success, mainTree.Tick(0.016f));
        Assert.True(executed[0]);
        Assert.True(executed[1]);
    }

    [Fact]
    public void Builder_Wait()
    {
        var tree = new FlowTree();
        tree.Build(Wait(0.5f));

        Assert.Equal(NodeStatus.Running, tree.Tick(0.3f));
        Assert.Equal(NodeStatus.Success, tree.Tick(0.3f)); // 0.6秒経過
    }

    [Fact]
    public void Builder_SingleAction()
    {
        var tree = new FlowTree();
        tree.Build(Action(static () => NodeStatus.Success));

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_NoRoot_ThrowsOnTick()
    {
        var tree = new FlowTree();
        Assert.Throws<System.InvalidOperationException>(() => tree.Tick(0.016f));
    }

    [Fact]
    public void Builder_GenericState()
    {
        var state = new GameState { Score = 100 };
        var tree = new FlowTree();
        tree.Build(state, 
                Sequence(
                    Action<GameState>(s =>
                    {
                        s.Score += 10;
                        return NodeStatus.Success;
                    }),
                    Condition<GameState>(s => s.Score > 100)
                )
            );

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
        tree.Build(
            Sequence(
                Action(static () => NodeStatus.Success)
            )
        );

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

        var guarded = Guard<TestState>(
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
        patrolFlow.Build(Success);
        attackFlow.Build(Success);
        fleeFlow.Build(Success);

        var state = new AIState();

        // AI行動選択ツリー（計画書の例を再現）
        var aiTree = Tree("AI Behavior");
        aiTree
            .WithCallStack(new FlowCallStack(16))
            .Build(state, 
                Selector(
                    Guard<AIState>(s => s.IsLowHealth, SubTree(fleeFlow)),
                    Guard<AIState>(s => s.HasTarget, SubTree(attackFlow)),
                    SubTree(patrolFlow)
                )
            );

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
        tree.Build(state, 
                Retry(2,
                    Timeout(10.0f,
                        Sequence(
                            Do<GameLoopState>(s => s.EventLog.Add("Sequence Start")),
                            Selector(
                                Sequence(
                                    Condition<GameLoopState>(s => s.FrameCount > 5),
                                    Do<GameLoopState>(s => s.EventLog.Add("Branch A"))
                                ),
                                Sequence(
                                    RepeatUntilFail(
                                        Inverter(
                                            Action<GameLoopState>(s =>
                                            {
                                                actionCount++;
                                                s.FrameCount++;
                                                // 3回目でSuccessを返す（Inverterで反転→Failure→RepeatUntilFail終了）
                                                return actionCount >= 3 ? NodeStatus.Success : NodeStatus.Failure;
                                            })
                                        )
                                    ),
                                    Do<GameLoopState>(s => s.EventLog.Add("Branch B"))
                                )
                            ),
                            Do<GameLoopState>(s => s.EventLog.Add("Sequence End"))
                        )
                    )
                )
            );

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
            .Build(state, 
                Selector(
                    // 終了条件: カウントが0以下
                    Sequence(
                        Condition<GameLoopState>(s => s.FrameCount <= 0),
                        Do<GameLoopState>(s => log.Add(-1)) // 終了マーカー
                    ),
                    // 再帰: カウントを減らして自己呼び出し
                    Sequence(
                        Do<GameLoopState>(s =>
                        {
                            log.Add(s.FrameCount);
                            s.FrameCount--;
                        }),
                        SubTree(countdown) // 自己再帰
                    )
                )
            );

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
            .Build(state, 
                Selector(
                    Sequence(
                        Condition<GameLoopState>(s => s.FrameCount >= 6),
                        Success
                    ),
                    Sequence(
                        Do<GameLoopState>(s =>
                        {
                            log.Add("Ping");
                            s.FrameCount++;
                        }),
                        SubTree(pongTree)
                    )
                )
            );

        pongTree
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Selector(
                    Sequence(
                        Condition<GameLoopState>(s => s.FrameCount >= 6),
                        Success
                    ),
                    Sequence(
                        Do<GameLoopState>(s =>
                        {
                            log.Add("Pong");
                            s.FrameCount++;
                        }),
                        SubTree(pingTree)
                    )
                )
            );

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
        childTree.Build(new BattleState(),
                Sequence(
                    Do<BattleState>(s =>
                    {
                        // 子Stateでバトル処理
                        s.EnemyHealth -= 30;
                        s.TurnCount++;
                    }),
                    Action<BattleState>(s =>
                    {
                        // 親Stateにスコアを加算
                        var parent = (GameLoopState)s.Parent!;
                        parent.Score += 100 * s.TurnCount;
                        return NodeStatus.Success;
                    })
                )
            );

        var mainTree = new FlowTree("Main");
        mainTree
            .WithCallStack(new FlowCallStack(16))
            .Build(parentState, 
                Sequence(
                    Do<GameLoopState>(s => s.EventLog.Add("Start")),
                    // 3回バトルを実行（State注入）
                    Repeat(3,
                        SubTree<GameLoopState, BattleState>(childTree, p => new BattleState())
                    ),
                    Do<GameLoopState>(s => s.EventLog.Add("End"))
                )
            );

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
        easyTree.Build(state, Do<GameLoopState>(s => s.Score += 10));

        var normalTree = new FlowTree("Normal");
        normalTree.Build(state, Do<GameLoopState>(s => s.Score += 50));

        var hardTree = new FlowTree("Hard");
        hardTree.Build(state, Do<GameLoopState>(s => s.Score += 100));

        var mainTree = new FlowTree("DynamicSelect");
        mainTree
            .WithCallStack(new FlowCallStack(16))
            .Build(state, 
                Repeat(3,
                    Sequence(
                        SubTree<GameLoopState>(s =>
                        {
                            // スコアに応じて難易度を選択
                            if (s.Score >= 100) return hardTree;
                            if (s.Score >= 30) return normalTree;
                            return easyTree;
                        }),
                        Do<GameLoopState>(s => s.FrameCount++)
                    )
                )
            );

        var status = mainTree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);
        // 1回目: Easy(+10) -> Score=10
        // 2回目: Easy(+10) -> Score=20
        // 3回目: Easy(+10) -> Score=30
        Assert.Equal(30, state.Score);
        Assert.Equal(3, state.FrameCount);
    }

    [Fact]
    public void Complex_ScopeHandlers_TrackingExecution()
    {
        // Scopeハンドラで実行追跡
        var state = new GameLoopState();

        var tree = new FlowTree("ScopeTracking");
        tree.Build(state,
                Sequence(
                    Scope<GameLoopState>(
                        s => s.EventLog.Add("Action1:Enter"),
                        (s, result) => s.EventLog.Add($"Action1:Exit({result})"),
                        Do<GameLoopState>(s => s.Score += 10)
                    ),
                    Scope<GameLoopState>(
                        s => s.EventLog.Add("Action2:Enter"),
                        (s, result) => s.EventLog.Add($"Action2:Exit({result})"),
                        Do<GameLoopState>(s => s.Score += 20)
                    )
                )
            );

        var status = tree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(30, state.Score);

        // すべてのコールバックがログされていることを確認
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
        tree.Build(state, 
                Retry(3,
                    Timeout(1.0f,
                        Sequence(
                            Do<NetworkState>(s => s.ConnectionAttempts++),
                            // 3回目で接続成功
                            Action<NetworkState>(s =>
                            {
                                if (s.ConnectionAttempts >= 3)
                                {
                                    s.IsConnected = true;
                                    return NodeStatus.Success;
                                }
                                return NodeStatus.Failure; // 失敗してリトライ
                            })
                        )
                    )
                )
            );

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
        gameLoopTree.Build(state, 
                RepeatUntilFail(
                    Action<GameLoopState>(s =>
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
                )
            );

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
        battleTree.Build(state, 
                RepeatUntilFail(
                    Sequence(
                        // 勝敗判定
                        Inverter(
                            Selector(
                                Condition<BattleState>(s => s.PlayerHealth <= 0),
                                Condition<BattleState>(s => s.EnemyHealth <= 0)
                            )
                        ),
                        // ターン処理
                        Selector(
                            // プレイヤーターン
                            Sequence(
                                Condition<BattleState>(s => s.IsPlayerTurn),
                                Do<BattleState>(s =>
                                {
                                    s.EnemyHealth -= 20;
                                    s.LastAction = "PlayerAttack";
                                    s.IsPlayerTurn = false;
                                    s.TurnCount++;
                                })
                            ),
                            // 敵ターン
                            Sequence(
                                Condition<BattleState>(s => !s.IsPlayerTurn),
                                Do<BattleState>(s =>
                                {
                                    s.PlayerHealth -= 15;
                                    s.LastAction = "EnemyAttack";
                                    s.IsPlayerTurn = true;
                                })
                            )
                        )
                    )
                )
            );

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
    public void Complex_Join_ResourceLoading()
    {
        // 並列ロード + 全完了待機
        int frameCount = 0;
        var loadedResources = new List<string>();
        var eventLog = new List<string>();

        var tree = new FlowTree("ResourceLoading");
        tree.Build(
            Sequence(
                Do(() => eventLog.Add("LoadStart")),
                Join(
                    // テクスチャロード（2フレーム目で完了）
                    Action(() =>
                    {
                        if (frameCount < 2) return NodeStatus.Running;
                        if (!loadedResources.Contains("Textures"))
                            loadedResources.Add("Textures");
                        return NodeStatus.Success;
                    }),
                    // サウンドロード（3フレーム目で完了）
                    Action(() =>
                    {
                        if (frameCount < 3) return NodeStatus.Running;
                        if (!loadedResources.Contains("Sounds"))
                            loadedResources.Add("Sounds");
                        return NodeStatus.Success;
                    }),
                    // データロード（即座に完了）
                    Do(() =>
                    {
                        if (!loadedResources.Contains("Data"))
                            loadedResources.Add("Data");
                    })
                ),
                Do(() => eventLog.Add("LoadComplete"))
            )
        );

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
        tree.Build(
            Race(
                // 遅いタスク（5フレーム）
                Action(() =>
                {
                    if (frameCount < 5) return NodeStatus.Running;
                    winner = "SlowTask";
                    return NodeStatus.Success;
                }),
                // 速いタスク（2フレーム）
                Action(() =>
                {
                    if (frameCount < 2) return NodeStatus.Running;
                    winner = "FastTask";
                    return NodeStatus.Success;
                })
            )
        );

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
        tree.Build(
            ShuffledSelector(
                Action(() => { tried.Add(1); return NodeStatus.Failure; }),
                Action(() => { tried.Add(2); return NodeStatus.Failure; }),
                Action(() => { tried.Add(3); return NodeStatus.Failure; })
            )
        );

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
        tree.Build(
            WeightedRandomSelector(
                (70.0f, Action(() => { selections["Common"]++; return NodeStatus.Success; })),
                (25.0f, Action(() => { selections["Rare"]++; return NodeStatus.Success; })),
                (5.0f, Action(() => { selections["Legendary"]++; return NodeStatus.Success; }))
            )
        );

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
        tree.Build(state, 
                RoundRobin(
                    Do<GameLoopState>(s => selections.Add("A")),
                    Do<GameLoopState>(s => selections.Add("B")),
                    Do<GameLoopState>(s => selections.Add("C"))
                )
            );

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
        titleScene.Build(state, 
                Sequence(
                    Do<GameLoopState>(s => s.EventLog.Add("Title:Show")),
                    Do<GameLoopState>(s => s.EventLog.Add("Title:Hide"))
                )
            );

        // ゲーム画面（親Stateを使用）
        gameScene.Build(state, 
                Sequence(
                    Do<GameLoopState>(s => s.EventLog.Add("Game:Start")),
                    Repeat(3, Do<GameLoopState>(s => s.FrameCount++)),
                    Do<GameLoopState>(s =>
                    {
                        s.Score += 1000;
                        s.EventLog.Add("Game:End");
                    })
                )
            );

        // リザルト画面
        resultScene.Build(state, 
                Sequence(
                    Do<GameLoopState>(s =>
                    {
                        s.EventLog.Add($"Result:Score={s.Score}");
                        s.WantsToContinue = s.RetryCount < 1;
                        s.RetryCount++;
                    })
                )
            );

        // メインフロー
        var mainFlow = new FlowTree("Main");
        mainFlow
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Sequence(
                    // タイトル
                    SubTree(titleScene),
                    // ゲームループ
                    RepeatUntilFail(
                        Sequence(
                            SubTree(gameScene),
                            SubTree(resultScene),
                            Condition<GameLoopState>(s => s.WantsToContinue)
                        )
                    ),
                    Do<GameLoopState>(s => s.EventLog.Add("Exit"))
                )
            );

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

/// <summary>
/// FlowBuilder&lt;T&gt;を使った型推論テスト。
/// b.Do(s => ...) のように明示的な型パラメータなしで記述できることを確認。
/// </summary>
public class FlowBuilderTypeInferenceTests
{
    private class TestState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Value { get; set; }
        public bool IsEnabled { get; set; }
        public List<string> Log { get; } = new();
    }

    [Fact]
    public void FlowBuilder_Do_TypeInference()
    {
        var state = new TestState { Value = 0 };

        var tree = new FlowTree();
        tree.Build(state, b => b.Sequence(
            b.Do(s => s.Value += 10),  // 型パラメータ不要
            b.Do(s => s.Value += 20)
        ));

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(30, state.Value);
    }

    [Fact]
    public void FlowBuilder_Action_TypeInference()
    {
        var state = new TestState { Value = 0 };

        var tree = new FlowTree();
        tree.Build(state, b => b.Action(s =>
        {
            s.Value = 42;
            return NodeStatus.Success;
        }));

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(42, state.Value);
    }

    [Fact]
    public void FlowBuilder_Condition_TypeInference()
    {
        var state = new TestState { IsEnabled = true };

        var tree = new FlowTree();
        tree.Build(state, b => b.Sequence(
            b.Condition(s => s.IsEnabled),  // 型パラメータ不要
            b.Do(s => s.Value = 100)
        ));

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(100, state.Value);
    }

    [Fact]
    public void FlowBuilder_WaitUntil_TypeInference()
    {
        var state = new TestState { IsEnabled = false };

        var tree = new FlowTree();
        tree.Build(state, b => b.Sequence(
            b.WaitUntil(s => s.IsEnabled),  // 型パラメータ不要
            b.Do(s => s.Value = 200)
        ));

        // まだ有効化されていない
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));

        // 有効化
        state.IsEnabled = true;
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(200, state.Value);
    }

    [Fact]
    public void FlowBuilder_Guard_TypeInference()
    {
        var state = new TestState { IsEnabled = true };

        var tree = new FlowTree();
        tree.Build(state, b => b.Sequence(
            b.Guard(s => s.IsEnabled, b.Do(s => s.Value = 300)),  // 型推論
            b.Do(s => s.Log.Add("Done"))
        ));

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(300, state.Value);
        Assert.Contains("Done", state.Log);
    }

    [Fact]
    public void FlowBuilder_Scope_TypeInference()
    {
        var state = new TestState();

        var tree = new FlowTree();
        tree.Build(state, b => b.Scope(
            s => s.Log.Add("Enter"),
            (s, result) => s.Log.Add($"Exit:{result}"),
            b.Do(s => s.Value = 100)
        ));

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(100, state.Value);
        Assert.Contains("Enter", state.Log);
        Assert.Contains("Exit:Success", state.Log);
    }

    [Fact]
    public void FlowBuilder_ComplexTree_AllTypesInferred()
    {
        var state = new TestState { Value = 0 };

        var tree = new FlowTree();
        tree.Build(state, b => b.Sequence(
            b.Do(s => s.Log.Add("Start")),
            b.Selector(
                b.Sequence(
                    b.Condition(s => s.Value > 100),
                    b.Do(s => s.Log.Add("Branch A"))
                ),
                b.Sequence(
                    b.Do(s => s.Value = 50),
                    b.Do(s => s.Log.Add("Branch B"))
                )
            ),
            b.Scope(
                s => s.Log.Add("Scope:Enter"),
                (s, _) => s.Log.Add("Scope:Exit"),
                b.Guard(
                    s => s.Value >= 50,
                    b.Do(s => s.Log.Add("Guarded"))
                )
            ),
            b.Do(s => s.Log.Add("End"))
        ));

        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(50, state.Value);
        Assert.Equal(new[] { "Start", "Branch B", "Scope:Enter", "Guarded", "Scope:Exit", "End" }, state.Log);
    }

    [Fact]
    public void FlowBuilder_MixedWithStatelessNodes()
    {
        var state = new TestState();

        var tree = new FlowTree();
        tree.Build(state, b => b.Sequence(
            b.Do(s => s.Log.Add("Typed")),  // 型付き
            b.Wait(0.1f),                    // ステートレス
            b.Repeat(2, b.Do(s => s.Value++)),  // 型付き + ステートレスデコレータ
            b.Timeout(5.0f, b.Do(s => s.Log.Add("Inside Timeout")))
        ));

        // Wait中
        Assert.Equal(NodeStatus.Running, tree.Tick(0.05f));
        Assert.Single(state.Log);

        // Wait完了、Repeat + Timeout も同一 Tick で完了
        Assert.Equal(NodeStatus.Success, tree.Tick(0.1f));
        Assert.Equal(2, state.Value);
        Assert.Contains("Inside Timeout", state.Log);
    }

    [Fact]
    public void FlowBuilder_SubTree_TypeInference()
    {
        var state = new TestState();

        var subTree = new FlowTree();
        subTree.Build(state, b => b.Do(s => s.Value = 999));

        var mainTree = new FlowTree();
        mainTree
            .WithCallStack(new FlowCallStack(16))
            .Build(state, b => b.Sequence(
                b.SubTree(s => subTree),  // 動的SubTree、型推論
                b.Do(s => s.Log.Add($"Value={s.Value}"))
            ));

        Assert.Equal(NodeStatus.Success, mainTree.Tick(0.016f));
        Assert.Equal(999, state.Value);
        Assert.Contains("Value=999", state.Log);
    }
}
