using Xunit;
using static Tomato.FlowTree.Flow;

namespace Tomato.FlowTree.Tests;

public class LeafNodeTests
{
    [Fact]
    public void ActionNode_ReturnsSuccess()
    {
        var node = new ActionNode(static () => NodeStatus.Success);
        var ctx = new FlowContext();

        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    [Fact]
    public void ActionNode_ReturnsFailure()
    {
        var node = new ActionNode(static () => NodeStatus.Failure);
        var ctx = new FlowContext();

        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));
    }

    [Fact]
    public void ActionNode_ReturnsRunning()
    {
        var node = new ActionNode(static () => NodeStatus.Running);
        var ctx = new FlowContext();

        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
    }

    [Fact]
    public void ActionNode_UsesState()
    {
        var state = new TestState();
        var node = new ActionNode<TestState>(s =>
        {
            s.Value = 42;
            return NodeStatus.Success;
        });

        var ctx = new FlowContext { State = state };
        node.Tick(ref ctx);

        Assert.Equal(42, state.Value);
    }

    [Fact]
    public void ConditionNode_True()
    {
        var node = new ConditionNode(static () => true);
        var ctx = new FlowContext();

        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    [Fact]
    public void ConditionNode_False()
    {
        var node = new ConditionNode(static () => false);
        var ctx = new FlowContext();

        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));
    }

    [Fact]
    public void ConditionNode_UsesState()
    {
        var state = new TestState { IsEnabled = false };
        var node = new ConditionNode<TestState>(s => s.IsEnabled);

        var ctx = new FlowContext { State = state };
        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));

        state.IsEnabled = true;
        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    // =====================================================
    // SubTreeNode (Dynamic) Tests
    // =====================================================

    [Fact]
    public void SubTreeNode_Dynamic_ExecutesProvidedTree()
    {
        int callCount = 0;
        var subTree = new FlowTree();
        subTree.Build(Action(() => { callCount++; return NodeStatus.Success; }));

        var node = new SubTreeNode(() => subTree);

        var ctx = new FlowContext();
        var result = node.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void SubTreeNode_Dynamic_ReturnsFailureForNullTree()
    {
        var node = new SubTreeNode(() => null);

        var ctx = new FlowContext();
        var result = node.Tick(ref ctx);

        Assert.Equal(NodeStatus.Failure, result);
    }

    [Fact]
    public void SubTreeNode_Dynamic_EvaluatesProviderOnEachStart()
    {
        int providerCallCount = 0;
        int actionCallCount = 0;
        var subTree = new FlowTree();
        subTree.Build(Action(() => { actionCallCount++; return NodeStatus.Success; }));

        var node = new SubTreeNode(() =>
        {
            providerCallCount++;
            return subTree;
        });

        var ctx = new FlowContext();

        // 1回目
        node.Tick(ref ctx);
        Assert.Equal(1, providerCallCount);

        // 2回目（前回Successで完了したので再評価）
        node.Tick(ref ctx);
        Assert.Equal(2, providerCallCount);
    }

    [Fact]
    public void SubTreeNode_Dynamic_ContinuesRunningTree()
    {
        int providerCallCount = 0;
        int tickCount = 0;
        var subTree = new FlowTree();
        subTree.Build(
            Action(() =>
            {
                tickCount++;
                return tickCount < 2 ? NodeStatus.Running : NodeStatus.Success;
            })
        );

        var node = new SubTreeNode(() =>
        {
            providerCallCount++;
            return subTree;
        });

        var ctx = new FlowContext();

        // 1回目: Running
        var result1 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result1);
        Assert.Equal(1, providerCallCount);

        // 2回目: Running中なのでプロバイダは再評価されない
        var result2 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, result2);
        Assert.Equal(1, providerCallCount);
    }

    [Fact]
    public void SubTreeNode_Dynamic_DifferentTreesOnEachCall()
    {
        int callCount = 0;
        var successTree = new FlowTree();
        successTree.Build(Success);
        var failureTree = new FlowTree();
        failureTree.Build(Failure);

        var node = new SubTreeNode(() =>
        {
            callCount++;
            return callCount == 1 ? successTree : failureTree;
        });

        var ctx = new FlowContext();

        // 1回目: successTree
        var result1 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, result1);

        // 2回目: failureTree
        var result2 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Failure, result2);
    }

    [Fact]
    public void SubTreeNode_Dynamic_Reset()
    {
        int providerCallCount = 0;
        int actionCallCount = 0;
        var subTree = new FlowTree();
        subTree.Build(
            Action(() =>
            {
                actionCallCount++;
                return NodeStatus.Running;
            })
        );

        var node = new SubTreeNode(() =>
        {
            providerCallCount++;
            return subTree;
        });

        var ctx = new FlowContext();

        // Running状態にする
        node.Tick(ref ctx);
        Assert.Equal(1, providerCallCount);

        // リセット
        node.Reset();

        // 次のTickでプロバイダが再評価される
        node.Tick(ref ctx);
        Assert.Equal(2, providerCallCount);
    }

    [Fact]
    public void SubTreeNode_Dynamic_Generic_WithState()
    {
        var state = new TestState { Value = 0 };
        var subTree = new FlowTree();
        subTree.Build(state, 
                Action<TestState>(s =>
                {
                    s.Value = 100;
                    return NodeStatus.Success;
                })
            );

        var node = new SubTreeNode<TestState>(s =>
        {
            // 状態に基づいてサブツリーを選択できる
            return subTree;
        });

        var ctx = new FlowContext { State = state };
        node.Tick(ref ctx);

        Assert.Equal(100, state.Value);
    }

    // =====================================================
    // WaitUntilNode Tests
    // =====================================================

    [Fact]
    public void WaitUntilNode_ReturnsRunningWhileConditionFalse()
    {
        bool conditionMet = false;
        var node = new WaitUntilNode(() => conditionMet);

        var ctx = new FlowContext();

        // まだ条件が満たされていない
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));

        // 条件が満たされた
        conditionMet = true;
        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    [Fact]
    public void WaitUntilNode_ReturnsSuccessImmediatelyWhenConditionTrue()
    {
        var node = new WaitUntilNode(() => true);

        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    [Fact]
    public void WaitUntilNode_Generic_UsesState()
    {
        var state = new TestState { IsEnabled = false };
        var node = new WaitUntilNode<TestState>(s => s.IsEnabled);

        var ctx = new FlowContext { State = state };

        // 条件が満たされていない
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));

        // 条件が満たされた
        state.IsEnabled = true;
        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
    }

    [Fact]
    public void WaitUntilNode_DslStateless()
    {
        bool loaded = false;

        var tree = new FlowTree();
        tree.Build(WaitUntil(() => loaded));

        // まだロードされていない
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));

        // ロード完了
        loaded = true;
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
    }

    [Fact]
    public void WaitUntilNode_DslWithState()
    {
        var state = new TestState { IsEnabled = false };

        var tree = new FlowTree();
        tree.Build(state, WaitUntil<TestState>(s => s.IsEnabled));

        // まだ有効化されていない
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));

        // 有効化
        state.IsEnabled = true;
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
    }

    [Fact]
    public void WaitUntilNode_WithInterval_SkipsCheckDuringInterval()
    {
        int checkCount = 0;
        bool conditionMet = false;

        var node = new WaitUntilNode(() =>
        {
            checkCount++;
            return conditionMet;
        }, interval: 0.5f);

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目: チェックされる
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
        Assert.Equal(1, checkCount);

        // 2-4回目: interval内なのでチェックされない (0.1+0.1+0.1+0.1 = 0.4 < 0.5)
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
        Assert.Equal(1, checkCount);

        // 条件を満たすが、まだinterval内なので前回の結果(Running)を維持
        conditionMet = true;
        Assert.Equal(NodeStatus.Running, node.Tick(ref ctx));
        Assert.Equal(1, checkCount);

        // interval経過 (0.5 >= 0.5)、再チェックされる
        Assert.Equal(NodeStatus.Success, node.Tick(ref ctx));
        Assert.Equal(2, checkCount);
    }

    [Fact]
    public void WaitUntilNode_WithInterval_ResetClearsState()
    {
        int checkCount = 0;
        var node = new WaitUntilNode(() =>
        {
            checkCount++;
            return false;
        }, interval: 1.0f);

        var ctx = new FlowContext { DeltaTime = 0.1f };

        // 1回目
        node.Tick(ref ctx);
        Assert.Equal(1, checkCount);

        // interval内
        node.Tick(ref ctx);
        Assert.Equal(1, checkCount);

        // リセット
        node.Reset();

        // リセット後は即座にチェック
        node.Tick(ref ctx);
        Assert.Equal(2, checkCount);
    }

    private class TestState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Value { get; set; }
        public bool IsEnabled { get; set; }
    }

    // =====================================================
    // ReturnNode Tests
    // =====================================================

    [Fact]
    public void ReturnNode_SetsReturnRequestedFlag()
    {
        var node = new ReturnNode(NodeStatus.Success);
        var ctx = new FlowContext();

        var status = node.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.True(ctx.ReturnRequested);
        Assert.Equal(NodeStatus.Success, ctx.ReturnStatus);
    }

    [Fact]
    public void ReturnNode_ReturnsSpecifiedStatus()
    {
        var successNode = new ReturnNode(NodeStatus.Success);
        var failureNode = new ReturnNode(NodeStatus.Failure);
        var ctx1 = new FlowContext();
        var ctx2 = new FlowContext();

        Assert.Equal(NodeStatus.Success, successNode.Tick(ref ctx1));
        Assert.Equal(NodeStatus.Failure, failureNode.Tick(ref ctx2));
    }

    [Fact]
    public void ReturnNode_TriggersScopeOnExit()
    {
        int exitCount = 0;
        NodeStatus? exitStatus = null;

        var tree = new FlowTree();
        tree.Build(
            Scope(
                null,
                result => { exitCount++; exitStatus = result; },
                Sequence(
                    Do(() => { /* 何かの処理 */ }),
                    ReturnSuccess()  // ここでReturnを呼ぶ
                )
            )
        );

        var status = tree.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(1, exitCount);  // onExitが発火された
        Assert.Equal(NodeStatus.Success, exitStatus);  // 子がSuccessを返した時点で発火
    }

    [Fact]
    public void ReturnNode_WorksInSubTree()
    {
        int exitCount = 0;

        var subTree = new FlowTree();
        subTree.Build(
            Scope(
                null,
                _ => exitCount++,
                Sequence(
                    Do(() => { }),
                    ReturnSuccess()
                )
            )
        );

        var mainTree = new FlowTree();
        mainTree.Build(
            Sequence(
                SubTree(subTree),
                Do(() => { })  // これも実行される
            )
        );

        var status = mainTree.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(1, exitCount);  // サブツリー内のonExitが発火された
    }

    [Fact]
    public void ReturnNode_DslFactories()
    {
        // ReturnSuccess
        var tree1 = new FlowTree();
        tree1.Build(ReturnSuccess());
        Assert.Equal(NodeStatus.Success, tree1.Tick(0.016f));

        // ReturnFailure
        var tree2 = new FlowTree();
        tree2.Build(ReturnFailure());
        Assert.Equal(NodeStatus.Failure, tree2.Tick(0.016f));

        // Return with status
        var tree3 = new FlowTree();
        tree3.Build(Return(NodeStatus.Failure));
        Assert.Equal(NodeStatus.Failure, tree3.Tick(0.016f));
    }

    [Fact]
    public void ReturnNode_FlowBuilder()
    {
        var state = new TestState();

        var tree = new FlowTree();
        tree.Build(state, b => b.Sequence(
            b.Do(s => s.Value = 42),
            b.ReturnSuccess()
        ));

        var status = tree.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(42, state.Value);
    }

    [Fact]
    public void ReturnNode_ResetsRunningNodesWithScopeFiring()
    {
        int enterCount = 0;
        int exitCount = 0;
        bool shouldReturn = false;

        var tree = new FlowTree();
        tree.Build(
            Scope(
                () => enterCount++,
                _ => exitCount++,
                Race(
                    // キャンセル条件が満たされたらReturn
                    Sequence(
                        WaitUntil(() => shouldReturn),
                        ReturnSuccess()
                    ),
                    // 通常はRunning（長時間処理のシミュレーション）
                    Action(() => NodeStatus.Running)
                )
            )
        );

        // 1回目: Running状態に入る（WaitUntilとActionが両方Running）
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        Assert.Equal(1, enterCount);
        Assert.Equal(0, exitCount);

        // 2回目: まだRunning
        Assert.Equal(NodeStatus.Running, tree.Tick(0.016f));
        Assert.Equal(1, enterCount);  // 再度enterは呼ばれない
        Assert.Equal(0, exitCount);

        // Return条件を満たす
        shouldReturn = true;

        // 3回目: WaitUntilが成功→ReturnSuccessで終了、ScopeのonExitが発火
        Assert.Equal(NodeStatus.Success, tree.Tick(0.016f));
        Assert.Equal(1, enterCount);
        Assert.Equal(1, exitCount);  // onExitが発火された
    }
}
