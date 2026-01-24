using System.Collections.Generic;
using Xunit;

namespace Tomato.FlowTree.Tests;

public class CompositeNodeTests
{
    [Fact]
    public void SequenceNode_AllSuccess()
    {
        var sequence = new SequenceNode(
            new ActionNode(static () => NodeStatus.Success),
            new ActionNode(static () => NodeStatus.Success),
            new ActionNode(static () => NodeStatus.Success)
        );

        var ctx = new FlowContext();
        var result = sequence.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, result);
    }

    [Fact]
    public void SequenceNode_FirstFailure()
    {
        var executed = new bool[3];

        var sequence = new SequenceNode(
            new ActionNode(() => { executed[0] = true; return NodeStatus.Success; }),
            new ActionNode(() => { executed[1] = true; return NodeStatus.Failure; }),
            new ActionNode(() => { executed[2] = true; return NodeStatus.Success; })
        );

        var ctx = new FlowContext();
        var result = sequence.Tick(ref ctx);

        Assert.Equal(NodeStatus.Failure, result);
        Assert.True(executed[0]);
        Assert.True(executed[1]);
        Assert.False(executed[2]); // 3番目は実行されない
    }

    [Fact]
    public void SequenceNode_Running()
    {
        int callCount = 0;

        var sequence = new SequenceNode(
            new ActionNode(() => { callCount++; return NodeStatus.Success; }),
            new ActionNode(() => { callCount++; return NodeStatus.Running; }),
            new ActionNode(() => { callCount++; return NodeStatus.Success; })
        );

        var ctx = new FlowContext();

        // 1回目のTick
        var result = sequence.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(2, callCount);

        // 2回目のTick（Runningから再開）
        result = sequence.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(3, callCount); // 2番目のノードから再開
    }

    [Fact]
    public void SelectorNode_FirstSuccess()
    {
        var executed = new bool[3];

        var selector = new SelectorNode(
            new ActionNode(() => { executed[0] = true; return NodeStatus.Failure; }),
            new ActionNode(() => { executed[1] = true; return NodeStatus.Success; }),
            new ActionNode(() => { executed[2] = true; return NodeStatus.Success; })
        );

        var ctx = new FlowContext();
        var result = selector.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, result);
        Assert.True(executed[0]);
        Assert.True(executed[1]);
        Assert.False(executed[2]); // 3番目は実行されない
    }

    [Fact]
    public void SelectorNode_AllFailure()
    {
        var selector = new SelectorNode(
            new ActionNode(static () => NodeStatus.Failure),
            new ActionNode(static () => NodeStatus.Failure),
            new ActionNode(static () => NodeStatus.Failure)
        );

        var ctx = new FlowContext();
        var result = selector.Tick(ref ctx);

        Assert.Equal(NodeStatus.Failure, result);
    }

    [Fact]
    public void SelectorNode_Running()
    {
        int callCount = 0;

        var selector = new SelectorNode(
            new ActionNode(() => { callCount++; return NodeStatus.Failure; }),
            new ActionNode(() => { callCount++; return NodeStatus.Running; }),
            new ActionNode(() => { callCount++; return NodeStatus.Success; })
        );

        var ctx = new FlowContext();

        // 1回目のTick
        var result = selector.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(2, callCount);

        // 2回目のTick（Runningから再開）
        result = selector.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result);
        Assert.Equal(3, callCount); // 2番目のノードから再開
    }

    [Fact]
    public void SequenceNode_Reset()
    {
        int index = 0;
        var sequence = new SequenceNode(
            new ActionNode(() => { index = 1; return NodeStatus.Running; }),
            new ActionNode(() => { index = 2; return NodeStatus.Success; })
        );

        var ctx = new FlowContext();
        sequence.Tick(ref ctx); // Running状態
        Assert.Equal(1, index);

        sequence.Reset();
        sequence.Tick(ref ctx);
        Assert.Equal(1, index); // リセット後は最初から
    }

    // =====================================================
    // ShuffledSelectorNode Tests
    // =====================================================

    [Fact]
    public void ShuffledSelectorNode_ExecutesAllBeforeRepeat()
    {
        var executed = new List<int>();
        var node = new ShuffledSelectorNode(42,
            new ActionNode(() => { executed.Add(0); return NodeStatus.Success; }),
            new ActionNode(() => { executed.Add(1); return NodeStatus.Success; }),
            new ActionNode(() => { executed.Add(2); return NodeStatus.Success; })
        );

        var ctx = new FlowContext();

        // 3回実行すると全ての子が1回ずつ実行される
        for (int i = 0; i < 3; i++)
        {
            node.Tick(ref ctx);
        }

        Assert.Equal(3, executed.Count);
        Assert.Contains(0, executed);
        Assert.Contains(1, executed);
        Assert.Contains(2, executed);
    }

    [Fact]
    public void ShuffledSelectorNode_ReshufflesAfterFullCycle()
    {
        var executed = new List<int>();
        var node = new ShuffledSelectorNode(42,
            new ActionNode(() => { executed.Add(0); return NodeStatus.Success; }),
            new ActionNode(() => { executed.Add(1); return NodeStatus.Success; })
        );

        var ctx = new FlowContext();

        // 4回実行（2サイクル）
        for (int i = 0; i < 4; i++)
        {
            node.Tick(ref ctx);
        }

        Assert.Equal(4, executed.Count);
    }

    [Fact]
    public void ShuffledSelectorNode_ContinuesRunningChild()
    {
        int runningCalls = 0;
        var node = new ShuffledSelectorNode(42,
            new ActionNode(() =>
            {
                runningCalls++;
                return runningCalls < 2 ? NodeStatus.Running : NodeStatus.Success;
            }),
            new ActionNode(() => NodeStatus.Success)
        );

        var ctx = new FlowContext();

        // 1回目: Running
        var result1 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result1);

        // 2回目: 同じ子を継続して Success
        var result2 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, result2);

        Assert.Equal(2, runningCalls);
    }

    [Fact]
    public void ShuffledSelectorNode_EmptyChildren_ReturnsFailure()
    {
        var node = new ShuffledSelectorNode();
        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));
    }

    // =====================================================
    // WeightedRandomSelectorNode Tests
    // =====================================================

    [Fact]
    public void WeightedRandomSelectorNode_SelectsBasedOnWeight()
    {
        var selected = new int[3];
        var node = new WeightedRandomSelectorNode(42,
            (1.0f, new ActionNode(() => { selected[0]++; return NodeStatus.Success; })),
            (2.0f, new ActionNode(() => { selected[1]++; return NodeStatus.Success; })),
            (1.0f, new ActionNode(() => { selected[2]++; return NodeStatus.Success; }))
        );

        var ctx = new FlowContext();

        // 多数回実行して分布を確認
        for (int i = 0; i < 100; i++)
        {
            node.Tick(ref ctx);
        }

        // 全て実行されているはず
        Assert.True(selected[0] > 0);
        Assert.True(selected[1] > 0);
        Assert.True(selected[2] > 0);
        // 重み2のものが最も多く選ばれるはず（確率的）
        Assert.True(selected[1] > selected[0] || selected[1] > selected[2]);
    }

    [Fact]
    public void WeightedRandomSelectorNode_ZeroWeightNeverSelected()
    {
        var selected = new int[2];
        var node = new WeightedRandomSelectorNode(42,
            (0.0f, new ActionNode(() => { selected[0]++; return NodeStatus.Success; })),
            (1.0f, new ActionNode(() => { selected[1]++; return NodeStatus.Success; }))
        );

        var ctx = new FlowContext();

        for (int i = 0; i < 50; i++)
        {
            node.Tick(ref ctx);
        }

        Assert.Equal(0, selected[0]);
        Assert.Equal(50, selected[1]);
    }

    [Fact]
    public void WeightedRandomSelectorNode_ContinuesRunningChild()
    {
        int runningCalls = 0;
        var node = new WeightedRandomSelectorNode(42,
            (1.0f, new ActionNode(() =>
            {
                runningCalls++;
                return runningCalls < 2 ? NodeStatus.Running : NodeStatus.Success;
            }))
        );

        var ctx = new FlowContext();

        var result1 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result1);

        var result2 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, result2);

        Assert.Equal(2, runningCalls);
    }

    [Fact]
    public void WeightedRandomSelectorNode_EmptyChildren_ReturnsFailure()
    {
        var node = new WeightedRandomSelectorNode();
        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));
    }

    // =====================================================
    // RoundRobinSelectorNode Tests
    // =====================================================

    [Fact]
    public void RoundRobinSelectorNode_ExecutesInOrder()
    {
        var executed = new List<int>();
        var node = new RoundRobinSelectorNode(
            new ActionNode(() => { executed.Add(0); return NodeStatus.Success; }),
            new ActionNode(() => { executed.Add(1); return NodeStatus.Success; }),
            new ActionNode(() => { executed.Add(2); return NodeStatus.Success; })
        );

        var ctx = new FlowContext();

        // 6回実行（2サイクル）
        for (int i = 0; i < 6; i++)
        {
            node.Tick(ref ctx);
        }

        Assert.Equal(new[] { 0, 1, 2, 0, 1, 2 }, executed);
    }

    [Fact]
    public void RoundRobinSelectorNode_ContinuesRunningChild()
    {
        var executed = new List<int>();
        int runningCalls = 0;
        var node = new RoundRobinSelectorNode(
            new ActionNode(() =>
            {
                executed.Add(0);
                runningCalls++;
                return runningCalls < 2 ? NodeStatus.Running : NodeStatus.Success;
            }),
            new ActionNode(() => { executed.Add(1); return NodeStatus.Success; })
        );

        var ctx = new FlowContext();

        // 1回目: Running（子0）
        var result1 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Running, result1);
        Assert.Single(executed);

        // 2回目: Success（子0を継続）
        var result2 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, result2);
        Assert.Equal(2, executed.Count);

        // 3回目: Success（子1へ進む）
        var result3 = node.Tick(ref ctx);
        Assert.Equal(NodeStatus.Success, result3);
        Assert.Equal(new[] { 0, 0, 1 }, executed);
    }

    [Fact]
    public void RoundRobinSelectorNode_PositionPersistsAcrossReset()
    {
        var executed = new List<int>();
        var node = new RoundRobinSelectorNode(
            new ActionNode(() => { executed.Add(0); return NodeStatus.Success; }),
            new ActionNode(() => { executed.Add(1); return NodeStatus.Success; })
        );

        var ctx = new FlowContext();

        node.Tick(ref ctx); // 子0
        node.Tick(ref ctx); // 子1

        node.Reset();

        node.Tick(ref ctx); // 子0（リセット後も位置は維持）
        Assert.Equal(new[] { 0, 1, 0 }, executed);
    }

    [Fact]
    public void RoundRobinSelectorNode_EmptyChildren_ReturnsFailure()
    {
        var node = new RoundRobinSelectorNode();
        var ctx = new FlowContext();
        Assert.Equal(NodeStatus.Failure, node.Tick(ref ctx));
    }
}
