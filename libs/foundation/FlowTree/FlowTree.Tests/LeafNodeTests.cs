using Xunit;

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
    // DynamicSubTreeNode Tests
    // =====================================================

    [Fact]
    public void DynamicSubTreeNode_ExecutesProvidedTree()
    {
        int callCount = 0;
        var subTree = new FlowTree();
        subTree.Build()
            .Action(() => { callCount++; return NodeStatus.Success; })
            .Complete();

        var node = new DynamicSubTreeNode(() => subTree);

        var ctx = new FlowContext();
        var result = node.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void DynamicSubTreeNode_ReturnsFailureForNullTree()
    {
        var node = new DynamicSubTreeNode(() => null);

        var ctx = new FlowContext();
        var result = node.Tick(ref ctx);

        Assert.Equal(NodeStatus.Failure, result);
    }

    [Fact]
    public void DynamicSubTreeNode_EvaluatesProviderOnEachStart()
    {
        int providerCallCount = 0;
        int actionCallCount = 0;
        var subTree = new FlowTree();
        subTree.Build()
            .Action(() => { actionCallCount++; return NodeStatus.Success; })
            .Complete();

        var node = new DynamicSubTreeNode(() =>
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
    public void DynamicSubTreeNode_ContinuesRunningTree()
    {
        int providerCallCount = 0;
        int tickCount = 0;
        var subTree = new FlowTree();
        subTree.Build()
            .Action(() =>
            {
                tickCount++;
                return tickCount < 2 ? NodeStatus.Running : NodeStatus.Success;
            })
            .Complete();

        var node = new DynamicSubTreeNode(() =>
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
    public void DynamicSubTreeNode_DifferentTreesOnEachCall()
    {
        int callCount = 0;
        var successTree = new FlowTree();
        successTree.Build().Success().Complete();
        var failureTree = new FlowTree();
        failureTree.Build().Failure().Complete();

        var node = new DynamicSubTreeNode(() =>
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
    public void DynamicSubTreeNode_Reset()
    {
        int providerCallCount = 0;
        int actionCallCount = 0;
        var subTree = new FlowTree();
        subTree.Build()
            .Action(() =>
            {
                actionCallCount++;
                return NodeStatus.Running;
            })
            .Complete();

        var node = new DynamicSubTreeNode(() =>
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
    public void DynamicSubTreeNode_Generic_WithState()
    {
        var state = new TestState { Value = 0 };
        var subTree = new FlowTree();
        subTree.Build(state)
            .Action(s =>
            {
                s.Value = 100;
                return NodeStatus.Success;
            })
            .Complete();

        var node = new DynamicSubTreeNode<TestState>(s =>
        {
            // 状態に基づいてサブツリーを選択できる
            return subTree;
        });

        var ctx = new FlowContext { State = state };
        node.Tick(ref ctx);

        Assert.Equal(100, state.Value);
    }

    private class TestState
    {
        public int Value { get; set; }
        public bool IsEnabled { get; set; }
    }
}
