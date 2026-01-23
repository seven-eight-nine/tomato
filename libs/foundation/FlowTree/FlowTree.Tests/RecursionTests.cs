using Xunit;

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
    private static FlowContext CreateContext(int maxDepth = 32)
    {
        return FlowContext.Create(
            new Blackboard(64),
            new FlowCallStack(maxDepth),
            deltaTime: 0.016f,
            maxCallDepth: maxDepth
        );
    }

    // =========================================================================
    // サブツリーチェーンテスト
    // =========================================================================

    [Fact]
    public void SubTreeChain_ExecutesInOrder()
    {
        // A → B → C のチェーン呼び出し
        var orderKey = new BlackboardKey<string>(1);

        var treeC = new FlowTree("C");
        var treeB = new FlowTree("B");
        var treeA = new FlowTree("A");

        treeC.Build()
            .Action((ref FlowContext ctx) =>
            {
                var order = ctx.Blackboard.GetString(orderKey, "") ?? "";
                ctx.Blackboard.SetString(orderKey, order + "C");
                return NodeStatus.Success;
            })
            .Complete();

        treeB.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var order = ctx.Blackboard.GetString(orderKey, "") ?? "";
                    ctx.Blackboard.SetString(orderKey, order + "B");
                    return NodeStatus.Success;
                })
                .SubTree(treeC)
            .End()
            .Complete();

        treeA.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var order = ctx.Blackboard.GetString(orderKey, "") ?? "";
                    ctx.Blackboard.SetString(orderKey, order + "A");
                    return NodeStatus.Success;
                })
                .SubTree(treeB)
            .End()
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetString(orderKey, "");

        var status = treeA.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("ABC", ctx.Blackboard.GetString(orderKey));
    }

    [Fact]
    public void SubTreeChain_DeepNesting()
    {
        // 10段階のチェーン呼び出し
        var counterKey = new BlackboardKey<int>(1);
        const int depth = 10;

        var trees = new FlowTree[depth];
        for (int i = 0; i < depth; i++)
            trees[i] = new FlowTree($"Tree{i + 1}");

        // 最深部のツリー
        trees[depth - 1].Build()
            .Action((ref FlowContext ctx) =>
            {
                var counter = ctx.Blackboard.GetInt(counterKey);
                ctx.Blackboard.SetInt(counterKey, counter + 1);
                return NodeStatus.Success;
            })
            .Complete();

        // 各レベルのツリーを作成（N → N+1 を呼ぶ）
        for (int i = depth - 2; i >= 0; i--)
        {
            var next = trees[i + 1];
            trees[i].Build()
                .Sequence()
                    .Action((ref FlowContext ctx) =>
                    {
                        var counter = ctx.Blackboard.GetInt(counterKey);
                        ctx.Blackboard.SetInt(counterKey, counter + 1);
                        return NodeStatus.Success;
                    })
                    .SubTree(next)
                .End()
                .Complete();
        }

        var ctx = CreateContext(maxDepth: 32);
        ctx.Blackboard.SetInt(counterKey, 0);

        var status = trees[0].Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(depth, ctx.Blackboard.GetInt(counterKey));
    }

    // =========================================================================
    // 自己再帰テスト
    // =========================================================================

    [Fact]
    public void SelfRecursion_Factorial_WorksCorrectly()
    {
        // 階乗計算パターン: n! = n * (n-1)!、終了条件: n <= 1
        var counterKey = new BlackboardKey<int>(1);
        var resultKey = new BlackboardKey<int>(2);

        var factorialTree = new FlowTree("Factorial");
        factorialTree.Build()
            .Selector()
                // 終了条件: counter <= 0 なら Success
                .Sequence()
                    .Condition((ref FlowContext ctx) =>
                        ctx.Blackboard.GetInt(counterKey) <= 0)
                    .Action((ref FlowContext ctx) =>
                    {
                        if (ctx.Blackboard.GetInt(resultKey) == 0)
                            ctx.Blackboard.SetInt(resultKey, 1);
                        return NodeStatus.Success;
                    })
                .End()
                // 再帰: counter-- して自己呼び出し
                .Sequence()
                    .Action((ref FlowContext ctx) =>
                    {
                        var counter = ctx.Blackboard.GetInt(counterKey);
                        var result = ctx.Blackboard.GetInt(resultKey);
                        if (result == 0) result = 1;
                        ctx.Blackboard.SetInt(resultKey, result * counter);
                        ctx.Blackboard.SetInt(counterKey, counter - 1);
                        return NodeStatus.Success;
                    })
                    .SubTree(factorialTree)
                .End()
            .End()
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(counterKey, 5); // 5!
        ctx.Blackboard.SetInt(resultKey, 0);

        var status = factorialTree.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(120, ctx.Blackboard.GetInt(resultKey)); // 5! = 120
    }

    [Fact]
    public void SelfRecursion_CountDown_TerminatesCorrectly()
    {
        // シンプルなカウントダウン再帰: counter > 0 なら counter-- して再帰
        var counterKey = new BlackboardKey<int>(1);
        var logKey = new BlackboardKey<string>(2);

        var countdownTree = new FlowTree("Countdown");
        countdownTree.Build()
            .Selector()
                // 終了条件
                .Sequence()
                    .Condition((ref FlowContext ctx) =>
                        ctx.Blackboard.GetInt(counterKey) <= 0)
                    .Action((ref FlowContext ctx) =>
                    {
                        var log = ctx.Blackboard.GetString(logKey, "") ?? "";
                        ctx.Blackboard.SetString(logKey, log + "Done");
                        return NodeStatus.Success;
                    })
                .End()
                // 再帰
                .Sequence()
                    .Action((ref FlowContext ctx) =>
                    {
                        var counter = ctx.Blackboard.GetInt(counterKey);
                        var log = ctx.Blackboard.GetString(logKey, "") ?? "";
                        ctx.Blackboard.SetString(logKey, log + counter.ToString());
                        ctx.Blackboard.SetInt(counterKey, counter - 1);
                        return NodeStatus.Success;
                    })
                    .SubTree(countdownTree)
                .End()
            .End()
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(counterKey, 3);
        ctx.Blackboard.SetString(logKey, "");

        var status = countdownTree.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("321Done", ctx.Blackboard.GetString(logKey));
    }

    [Fact]
    public void MutualRecursion_PingPong_WorksCorrectly()
    {
        // A → B → A → B ... の相互再帰パターン
        var counterKey = new BlackboardKey<int>(1);
        var logKey = new BlackboardKey<string>(2);

        var treeA = new FlowTree("A");
        var treeB = new FlowTree("B");

        // TreeA: "A" を記録してカウンタデクリメント、counter > 0 なら TreeB を呼ぶ
        treeA.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var log = ctx.Blackboard.GetString(logKey, "") ?? "";
                    ctx.Blackboard.SetString(logKey, log + "A");
                    var counter = ctx.Blackboard.GetInt(counterKey);
                    ctx.Blackboard.SetInt(counterKey, counter - 1);
                    return NodeStatus.Success;
                })
                .Selector()
                    .Sequence()
                        .Condition((ref FlowContext ctx) =>
                            ctx.Blackboard.GetInt(counterKey) > 0)
                        .SubTree(treeB)
                    .End()
                    .Action(static (ref FlowContext _) => NodeStatus.Success)
                .End()
            .End()
            .Complete();

        // TreeB: "B" を記録してカウンタデクリメント、counter > 0 なら TreeA を呼ぶ
        treeB.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var log = ctx.Blackboard.GetString(logKey, "") ?? "";
                    ctx.Blackboard.SetString(logKey, log + "B");
                    var counter = ctx.Blackboard.GetInt(counterKey);
                    ctx.Blackboard.SetInt(counterKey, counter - 1);
                    return NodeStatus.Success;
                })
                .Selector()
                    .Sequence()
                        .Condition((ref FlowContext ctx) =>
                            ctx.Blackboard.GetInt(counterKey) > 0)
                        .SubTree(treeA)
                    .End()
                    .Action(static (ref FlowContext _) => NodeStatus.Success)
                .End()
            .End()
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(counterKey, 6);
        ctx.Blackboard.SetString(logKey, "");

        var status = treeA.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("ABABAB", ctx.Blackboard.GetString(logKey));
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
        trees[chainLength - 1].Build()
            .Action(static (ref FlowContext _) => NodeStatus.Success)
            .Complete();

        // チェーンを作成
        for (int i = chainLength - 2; i >= 0; i--)
        {
            trees[i].Build()
                .SubTree(trees[i + 1])
                .Complete();
        }

        var ctx = CreateContext(maxDepth: maxDepth);
        var status = trees[0].Tick(ref ctx);

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

        trees[chainLength - 1].Build()
            .Action(static (ref FlowContext _) => NodeStatus.Success)
            .Complete();

        for (int i = chainLength - 2; i >= 0; i--)
        {
            trees[i].Build()
                .SubTree(trees[i + 1])
                .Complete();
        }

        var ctx = CreateContext(maxDepth: maxDepth);
        var status = trees[0].Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
    }

    // =========================================================================
    // 反復による擬似再帰テスト
    // =========================================================================

    [Fact]
    public void IterativeRecursion_WithExternalLoop()
    {
        // 外部ループを使った擬似再帰パターン
        var counterKey = new BlackboardKey<int>(1);

        var iterativeTree = new FlowTree("Iterative");
        iterativeTree.Build()
            .Node(new RepeatUntilSuccessNode(
                new SequenceNode(
                    new ActionNode((ref FlowContext ctx) =>
                    {
                        var counter = ctx.Blackboard.GetInt(counterKey);
                        ctx.Blackboard.SetInt(counterKey, counter + 1);
                        return NodeStatus.Success;
                    }),
                    new YieldNode(),
                    new ConditionNode((ref FlowContext ctx) =>
                        ctx.Blackboard.GetInt(counterKey) >= 5)
                )
            ))
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(counterKey, 0);

        // 外部ループでTickを繰り返す
        NodeStatus status;
        int iterations = 0;
        do
        {
            status = iterativeTree.Tick(ref ctx);
            iterations++;
        } while (status == NodeStatus.Running && iterations < 100);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(5, ctx.Blackboard.GetInt(counterKey));
    }

    [Fact]
    public void IterativeRecursion_WithRetry()
    {
        // RetryNodeを使った擬似再帰パターン
        var counterKey = new BlackboardKey<int>(1);

        var retryTree = new FlowTree("Retry");
        retryTree.Build()
            .Node(new RetryNode(10, new SequenceNode(
                new ActionNode((ref FlowContext ctx) =>
                {
                    var counter = ctx.Blackboard.GetInt(counterKey);
                    ctx.Blackboard.SetInt(counterKey, counter + 1);
                    return NodeStatus.Success;
                }),
                new ConditionNode((ref FlowContext ctx) =>
                    ctx.Blackboard.GetInt(counterKey) >= 5)
            )))
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(counterKey, 0);

        // RetryNodeはRunningを返すので、外部ループが必要
        NodeStatus status;
        int iterations = 0;
        do
        {
            status = retryTree.Tick(ref ctx);
            iterations++;
        } while (status == NodeStatus.Running && iterations < 100);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(5, ctx.Blackboard.GetInt(counterKey));
    }

    // =========================================================================
    // Running状態の継続テスト
    // =========================================================================

    [Fact]
    public void SubTree_RunningContinuation_PreservesState()
    {
        // SubTreeがRunningを返した後、次のTickで継続される
        var phaseKey = new BlackboardKey<int>(1);

        var subTree = new FlowTree("SubTree");
        subTree.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var phase = ctx.Blackboard.GetInt(phaseKey);
                    ctx.Blackboard.SetInt(phaseKey, phase + 1);
                    return NodeStatus.Success;
                })
                .Yield()
                .Action((ref FlowContext ctx) =>
                {
                    var phase = ctx.Blackboard.GetInt(phaseKey);
                    ctx.Blackboard.SetInt(phaseKey, phase + 1);
                    return NodeStatus.Success;
                })
            .End()
            .Complete();

        var mainTree = new FlowTree("Main");
        mainTree.Build()
            .SubTree(subTree)
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(phaseKey, 0);

        // 1回目のTick: phase=1, YieldでRunning
        var status1 = mainTree.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, status1);
        Assert.Equal(1, ctx.Blackboard.GetInt(phaseKey));

        // 2回目のTick: YieldNode完了後、phase=2, Success
        var status2 = mainTree.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, status2);
        Assert.Equal(2, ctx.Blackboard.GetInt(phaseKey));
    }

    [Fact]
    public void SubTree_DeepRunningContinuation()
    {
        // 深いチェーンでRunningが正しく伝播される
        var stageKey = new BlackboardKey<int>(1);

        var treeC = new FlowTree("C");
        var treeB = new FlowTree("B");
        var treeA = new FlowTree("A");

        // TreeC: Yieldを含む
        treeC.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var stage = ctx.Blackboard.GetInt(stageKey);
                    ctx.Blackboard.SetInt(stageKey, stage + 1);
                    return NodeStatus.Success;
                })
                .Yield()
                .Action((ref FlowContext ctx) =>
                {
                    var stage = ctx.Blackboard.GetInt(stageKey);
                    ctx.Blackboard.SetInt(stageKey, stage + 1);
                    return NodeStatus.Success;
                })
            .End()
            .Complete();

        // TreeB: TreeCを呼ぶ
        treeB.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var stage = ctx.Blackboard.GetInt(stageKey);
                    ctx.Blackboard.SetInt(stageKey, stage + 1);
                    return NodeStatus.Success;
                })
                .SubTree(treeC)
                .Action((ref FlowContext ctx) =>
                {
                    var stage = ctx.Blackboard.GetInt(stageKey);
                    ctx.Blackboard.SetInt(stageKey, stage + 1);
                    return NodeStatus.Success;
                })
            .End()
            .Complete();

        // TreeA: TreeBを呼ぶ
        treeA.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var stage = ctx.Blackboard.GetInt(stageKey);
                    ctx.Blackboard.SetInt(stageKey, stage + 1);
                    return NodeStatus.Success;
                })
                .SubTree(treeB)
                .Action((ref FlowContext ctx) =>
                {
                    var stage = ctx.Blackboard.GetInt(stageKey);
                    ctx.Blackboard.SetInt(stageKey, stage + 1);
                    return NodeStatus.Success;
                })
            .End()
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(stageKey, 0);

        // 1回目: A(1) → B(2) → C(3) → Yield → Running
        var status1 = treeA.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, status1);
        Assert.Equal(3, ctx.Blackboard.GetInt(stageKey));

        // 2回目: C(4) → B(5) → A(6) → Success
        var status2 = treeA.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, status2);
        Assert.Equal(6, ctx.Blackboard.GetInt(stageKey));
    }
}
