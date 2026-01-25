using Xunit;
using static Tomato.FlowTree.Flow;

namespace Tomato.FlowTree.Tests;

/// <summary>
/// サブツリー呼び出しとコールスタック関連のテスト。
///
/// FlowTreeは自己再帰（同じツリーへの再帰呼び出し）と相互再帰をサポートしています。
/// 各ノードは呼び出し深度ごとに状態を管理することで、
/// 同じノードインスタンスが異なる深度で同時に実行可能です。
/// </summary>
public class RecursionTests
{
    // =========================================================================
    // サブツリーチェーンテスト
    // =========================================================================

    [Fact]
    public void SubTreeChain_ExecutesInOrder()
    {
        // A → B → C のチェーン呼び出し
        var state = new OrderState { Order = "" };

        var treeC = new FlowTree("C");
        var treeB = new FlowTree("B");
        var treeA = new FlowTree("A");

        treeC.Build(state, 
                Action<OrderState>(s =>
                {
                    s.Order += "C";
                    return NodeStatus.Success;
                })
            );

        treeB.Build(state, 
                Sequence(
                    Action<OrderState>(s =>
                    {
                        s.Order += "B";
                        return NodeStatus.Success;
                    }),
                    SubTree(treeC)
                )
            );

        treeA
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Sequence(
                    Action<OrderState>(s =>
                    {
                        s.Order += "A";
                        return NodeStatus.Success;
                    }),
                    SubTree(treeB)
                )
            );

        var status = treeA.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("ABC", state.Order);
    }

    [Fact]
    public void SubTreeChain_DeepNesting()
    {
        // 10段階のチェーン呼び出し
        var state = new CounterState { Counter = 0 };
        const int depth = 10;

        var trees = new FlowTree[depth];
        for (int i = 0; i < depth; i++)
            trees[i] = new FlowTree($"Tree{i + 1}");

        // 最深部のツリー
        trees[depth - 1].Build(state, 
                Action<CounterState>(s =>
                {
                    s.Counter++;
                    return NodeStatus.Success;
                })
            );

        // 各レベルのツリーを作成（N → N+1 を呼ぶ）
        for (int i = depth - 2; i >= 0; i--)
        {
            var next = trees[i + 1];
            trees[i].Build(state, 
                    Sequence(
                        Action<CounterState>(s =>
                        {
                            s.Counter++;
                            return NodeStatus.Success;
                        }),
                        SubTree(next)
                    )
                );
        }

        trees[0].WithCallStack(new FlowCallStack(32));
        var status = trees[0].Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(depth, state.Counter);
    }

    // =========================================================================
    // 自己再帰テスト
    // =========================================================================

    [Fact]
    public void SelfRecursion_Factorial_WorksCorrectly()
    {
        // 階乗計算パターン: n! = n * (n-1)!、終了条件: n <= 1
        var state = new FactorialState { Counter = 5, Result = 0 };

        var factorialTree = new FlowTree("Factorial");
        factorialTree
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Selector(
                    // 終了条件: counter <= 0 なら Success
                    Sequence(
                        Condition<FactorialState>(s => s.Counter <= 0),
                        Action<FactorialState>(s =>
                        {
                            if (s.Result == 0)
                                s.Result = 1;
                            return NodeStatus.Success;
                        })
                    ),
                    // 再帰: counter-- して自己呼び出し
                    Sequence(
                        Action<FactorialState>(s =>
                        {
                            var counter = s.Counter;
                            var result = s.Result;
                            if (result == 0) result = 1;
                            s.Result = result * counter;
                            s.Counter = counter - 1;
                            return NodeStatus.Success;
                        }),
                        SubTree(factorialTree)
                    )
                )
            );

        var status = factorialTree.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(120, state.Result); // 5! = 120
    }

    [Fact]
    public void SelfRecursion_CountDown_TerminatesCorrectly()
    {
        // シンプルなカウントダウン再帰: counter > 0 なら counter-- して再帰
        var state = new CountdownState { Counter = 3, Log = "" };

        var countdownTree = new FlowTree("Countdown");
        countdownTree
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Selector(
                    // 終了条件
                    Sequence(
                        Condition<CountdownState>(s => s.Counter <= 0),
                        Action<CountdownState>(s =>
                        {
                            s.Log += "Done";
                            return NodeStatus.Success;
                        })
                    ),
                    // 再帰
                    Sequence(
                        Action<CountdownState>(s =>
                        {
                            s.Log += s.Counter.ToString();
                            s.Counter--;
                            return NodeStatus.Success;
                        }),
                        SubTree(countdownTree)
                    )
                )
            );

        var status = countdownTree.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("321Done", state.Log);
    }

    [Fact]
    public void MutualRecursion_PingPong_WorksCorrectly()
    {
        // A → B → A → B ... の相互再帰パターン
        var state = new PingPongState { Counter = 6, Log = "" };

        var treeA = new FlowTree("A");
        var treeB = new FlowTree("B");

        // TreeA: "A" を記録してカウンタデクリメント、counter > 0 なら TreeB を呼ぶ
        treeA
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Sequence(
                    Action<PingPongState>(s =>
                    {
                        s.Log += "A";
                        s.Counter--;
                        return NodeStatus.Success;
                    }),
                    Selector(
                        Sequence(
                            Condition<PingPongState>(s => s.Counter > 0),
                            SubTree(treeB)
                        ),
                        Action(static () => NodeStatus.Success)
                    )
                )
            );

        // TreeB: "B" を記録してカウンタデクリメント、counter > 0 なら TreeA を呼ぶ
        treeB.Build(state, 
                Sequence(
                    Action<PingPongState>(s =>
                    {
                        s.Log += "B";
                        s.Counter--;
                        return NodeStatus.Success;
                    }),
                    Selector(
                        Sequence(
                            Condition<PingPongState>(s => s.Counter > 0),
                            SubTree(treeA)
                        ),
                        Action(static () => NodeStatus.Success)
                    )
                )
            );

        var status = treeA.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("ABABAB", state.Log);
    }

    // =========================================================================
    // コールスタック深度制限テスト
    // =========================================================================

    [Fact]
    public void CallStackDepthLimit_BlocksExcessiveNesting()
    {
        // 深度制限を超えるチェーンはFailureを返す
        const int maxDepth = 5;
        const int chainLength = 10;

        var trees = new FlowTree[chainLength];
        for (int i = 0; i < chainLength; i++)
            trees[i] = new FlowTree($"Tree{i + 1}");

        // 最後のツリー
        trees[chainLength - 1].Build(Action(static () => NodeStatus.Success));

        // チェーンを作成
        for (int i = chainLength - 2; i >= 0; i--)
        {
            trees[i].Build(SubTree(trees[i + 1]));
        }

        trees[0]
            .WithCallStack(new FlowCallStack(maxDepth))
            .WithMaxCallDepth(maxDepth);

        var status = trees[0].Tick(0.016f);

        // 深度制限によりFailure
        Assert.Equal(NodeStatus.Failure, status);
    }

    [Fact]
    public void CallStackDepthLimit_AllowsWithinLimit()
    {
        // 深度制限内のチェーンは成功する
        const int maxDepth = 10;
        const int chainLength = 5;

        var trees = new FlowTree[chainLength];
        for (int i = 0; i < chainLength; i++)
            trees[i] = new FlowTree($"Tree{i + 1}");

        trees[chainLength - 1].Build(Action(static () => NodeStatus.Success));

        for (int i = chainLength - 2; i >= 0; i--)
        {
            trees[i].Build(SubTree(trees[i + 1]));
        }

        trees[0]
            .WithCallStack(new FlowCallStack(maxDepth))
            .WithMaxCallDepth(maxDepth);

        var status = trees[0].Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
    }

    // =========================================================================
    // 反復による擬似再帰テスト
    // =========================================================================

    [Fact]
    public void IterativeRecursion_WithExternalLoop()
    {
        // 外部ループを使った擬似再帰パターン
        var state = new CounterState { Counter = 0 };

        var iterativeTree = new FlowTree("Iterative");
        iterativeTree.Build(state, 
                new RepeatUntilSuccessNode(
                    new SequenceNode(
                        new ActionNode<CounterState>(s =>
                        {
                            s.Counter++;
                            return NodeStatus.Success;
                        }),
                        new YieldNode(),
                        new ConditionNode<CounterState>(s => s.Counter >= 5)
                    )
                )
            );

        // 外部ループでTickを繰り返す
        NodeStatus status;
        int iterations = 0;
        do
        {
            status = iterativeTree.Tick(0.016f);
            iterations++;
        } while (status == NodeStatus.Running && iterations < 100);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(5, state.Counter);
    }

    [Fact]
    public void IterativeRecursion_WithRetry()
    {
        // RetryNodeを使った擬似再帰パターン
        var state = new CounterState { Counter = 0 };

        var retryTree = new FlowTree("Retry");
        retryTree.Build(state, 
                new RetryNode(10, new SequenceNode(
                    new ActionNode<CounterState>(s =>
                    {
                        s.Counter++;
                        return NodeStatus.Success;
                    }),
                    new ConditionNode<CounterState>(s => s.Counter >= 5)
                ))
            );

        // RetryNodeはRunningを返すので、外部ループが必要
        NodeStatus status;
        int iterations = 0;
        do
        {
            status = retryTree.Tick(0.016f);
            iterations++;
        } while (status == NodeStatus.Running && iterations < 100);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(5, state.Counter);
    }

    // =========================================================================
    // Running状態の継続テスト
    // =========================================================================

    [Fact]
    public void SubTree_RunningContinuation_PreservesState()
    {
        // SubTreeがRunningを返した後、次のTickで継続される
        var state = new PhaseState { Phase = 0 };

        var subTree = new FlowTree("SubTree");
        subTree.Build(state, 
                Sequence(
                    Action<PhaseState>(s =>
                    {
                        s.Phase++;
                        return NodeStatus.Success;
                    }),
                    Yield(),
                    Action<PhaseState>(s =>
                    {
                        s.Phase++;
                        return NodeStatus.Success;
                    })
                )
            );

        var mainTree = new FlowTree("Main");
        mainTree
            .WithCallStack(new FlowCallStack(32))
            .Build(state, SubTree(subTree));

        // 1回目のTick: phase=1, YieldでRunning
        var status1 = mainTree.Tick(0.016f);
        Assert.Equal(NodeStatus.Running, status1);
        Assert.Equal(1, state.Phase);

        // 2回目のTick: YieldNode完了後、phase=2, Success
        var status2 = mainTree.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status2);
        Assert.Equal(2, state.Phase);
    }

    [Fact]
    public void SubTree_DeepRunningContinuation()
    {
        // 深いチェーンでRunningが正しく伝播される
        var state = new StageState { Stage = 0 };

        var treeC = new FlowTree("C");
        var treeB = new FlowTree("B");
        var treeA = new FlowTree("A");

        // TreeC: Yieldを含む
        treeC.Build(state, 
                Sequence(
                    Action<StageState>(s =>
                    {
                        s.Stage++;
                        return NodeStatus.Success;
                    }),
                    Yield(),
                    Action<StageState>(s =>
                    {
                        s.Stage++;
                        return NodeStatus.Success;
                    })
                )
            );

        // TreeB: TreeCを呼ぶ
        treeB.Build(state, 
                Sequence(
                    Action<StageState>(s =>
                    {
                        s.Stage++;
                        return NodeStatus.Success;
                    }),
                    SubTree(treeC),
                    Action<StageState>(s =>
                    {
                        s.Stage++;
                        return NodeStatus.Success;
                    })
                )
            );

        // TreeA: TreeBを呼ぶ
        treeA
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Sequence(
                    Action<StageState>(s =>
                    {
                        s.Stage++;
                        return NodeStatus.Success;
                    }),
                    SubTree(treeB),
                    Action<StageState>(s =>
                    {
                        s.Stage++;
                        return NodeStatus.Success;
                    })
                )
            );

        // 1回目: A(1) → B(2) → C(3) → Yield → Running
        var status1 = treeA.Tick(0.016f);
        Assert.Equal(NodeStatus.Running, status1);
        Assert.Equal(3, state.Stage);

        // 2回目: C(4) → B(5) → A(6) → Success
        var status2 = treeA.Tick(0.016f);
        Assert.Equal(NodeStatus.Success, status2);
        Assert.Equal(6, state.Stage);
    }

    // State classes
    private class OrderState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public string Order { get; set; } = "";
    }

    private class CounterState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Counter { get; set; }
    }

    private class FactorialState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Counter { get; set; }
        public int Result { get; set; }
    }

    private class CountdownState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Counter { get; set; }
        public string Log { get; set; } = "";
    }

    private class PingPongState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Counter { get; set; }
        public string Log { get; set; } = "";
    }

    private class PhaseState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Phase { get; set; }
    }

    private class StageState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Stage { get; set; }
    }
}
