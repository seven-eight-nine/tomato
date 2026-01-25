using Xunit;
using static Tomato.FlowTree.Flow;

namespace Tomato.FlowTree.Tests;

public class SubTreeTests
{
    [Fact]
    public void SubTreeNode_DirectReference()
    {
        var childTree = new FlowTree();
        childTree.Build(Action(static () => NodeStatus.Success));

        var mainTree = new FlowTree();
        mainTree
            .WithCallStack(new FlowCallStack(32))
            .Build(SubTree(childTree));

        Assert.Equal(NodeStatus.Success, mainTree.Tick(0.016f));
    }

    [Fact]
    public void SubTreeNode_UnbuiltTree()
    {
        var childTree = new FlowTree(); // SetRoot()呼び出しなし

        var mainTree = new FlowTree();
        mainTree
            .WithCallStack(new FlowCallStack(32))
            .Build(SubTree(childTree));

        // 構築されていないツリーはFailure
        Assert.Equal(NodeStatus.Failure, mainTree.Tick(0.016f));
    }

    [Fact]
    public void SubTreeNode_Running()
    {
        int callCount = 0;

        var childTree = new FlowTree();
        childTree.Build(
            Sequence(
                Action(() => { callCount++; return NodeStatus.Success; }),
                Action(() => { callCount++; return NodeStatus.Running; }),
                Action(() => { callCount++; return NodeStatus.Success; })
            )
        );

        var mainTree = new FlowTree();
        mainTree
            .WithCallStack(new FlowCallStack(32))
            .Build(SubTree(childTree));

        // 1回目: Running
        Assert.Equal(NodeStatus.Running, mainTree.Tick(0.016f));
        Assert.Equal(2, callCount);

        // 2回目: まだRunning（Sequenceの2番目がRunningを返し続ける）
        Assert.Equal(NodeStatus.Running, mainTree.Tick(0.016f));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void SubTreeNode_NestedSubTrees()
    {
        var executed = new bool[3];

        // Tree 3: 葉ノード
        var tree3 = new FlowTree();
        tree3.Build(Action(() => { executed[2] = true; return NodeStatus.Success; }));

        // Tree 2: Tree 3を呼ぶ
        var tree2 = new FlowTree();
        tree2.Build(
            Sequence(
                Action(() => { executed[1] = true; return NodeStatus.Success; }),
                SubTree(tree3)
            )
        );

        // Tree 1: Tree 2を呼ぶ
        var tree1 = new FlowTree();
        tree1
            .WithCallStack(new FlowCallStack(32))
            .Build(
                Sequence(
                    Action(() => { executed[0] = true; return NodeStatus.Success; }),
                    SubTree(tree2)
                )
            );

        Assert.Equal(NodeStatus.Success, tree1.Tick(0.016f));

        Assert.True(executed[0]);
        Assert.True(executed[1]);
        Assert.True(executed[2]);
    }

    [Fact]
    public void SubTreeNode_CallStackOverflow()
    {
        // 自己再帰的なツリー（終了条件なし）
        var recursiveTree = new FlowTree();
        recursiveTree
            .WithCallStack(new FlowCallStack(5))
            .WithMaxCallDepth(5)
            .Build(
                Sequence(
                    Action(static () => NodeStatus.Success),
                    SubTree(recursiveTree) // 自己呼び出し
                )
            );

        // スタックオーバーフローでFailure
        Assert.Equal(NodeStatus.Failure, recursiveTree.Tick(0.016f));
    }

    [Fact]
    public void SubTreeNode_SelfRecursion()
    {
        // 終了条件付き自己再帰
        var state = new CounterState { Counter = 5 };

        var countdown = new FlowTree();
        countdown
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Selector(
                    // 終了条件: counter <= 0
                    Sequence(
                        Condition<CounterState>(s => s.Counter <= 0),
                        Success
                    ),
                    // 再帰: counter-- して自己呼び出し
                    Sequence(
                        Action<CounterState>(s =>
                        {
                            s.Counter--;
                            return NodeStatus.Success;
                        }),
                        SubTree(countdown)
                    )
                )
            );

        var status = countdown.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(0, state.Counter);
    }

    [Fact]
    public void SubTreeNode_MutualRecursion()
    {
        // 相互再帰: ping → pong → ping → ...
        var state = new PingPongState { Counter = 6, Log = "" };

        var ping = new FlowTree("ping");
        var pong = new FlowTree("pong");

        ping
            .WithCallStack(new FlowCallStack(32))
            .Build(state, 
                Sequence(
                    Action<PingPongState>(s =>
                    {
                        s.Log += "P";
                        s.Counter--;
                        return NodeStatus.Success;
                    }),
                    Selector(
                        Sequence(
                            Condition<PingPongState>(s => s.Counter > 0),
                            SubTree(pong)
                        ),
                        Success
                    )
                )
            );

        pong.Build(state, 
                Sequence(
                    Action<PingPongState>(s =>
                    {
                        s.Log += "O";
                        s.Counter--;
                        return NodeStatus.Success;
                    }),
                    Selector(
                        Sequence(
                            Condition<PingPongState>(s => s.Counter > 0),
                            SubTree(ping)
                        ),
                        Success
                    )
                )
            );

        var status = ping.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("POPOPO", state.Log);
    }

    // =====================================================
    // State Injection Tests
    // =====================================================

    [Fact]
    public void SubTreeNode_StateInjection_InjectsNewState()
    {
        var parentState = new ParentState { ParentValue = 10 };
        var childTree = new FlowTree();
        childTree.Build(new ChildState(),
            Action<ChildState>(s =>
            {
                // 子のStateを使用
                s.ChildValue = 42;
                // 親のStateにもアクセス可能
                var parent = (ParentState)s.Parent!;
                parent.ParentValue += s.ChildValue;
                return NodeStatus.Success;
            })
        );

        var mainTree = new FlowTree();
        mainTree
            .WithCallStack(new FlowCallStack(32))
            .Build(parentState, 
                SubTree<ParentState, ChildState>(childTree, p => new ChildState())
            );

        var status = mainTree.Tick(0.016f);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(52, parentState.ParentValue); // 10 + 42
    }

    [Fact]
    public void SubTreeNode_StateInjection_ParentIsSet()
    {
        var parentState = new ParentState { ParentValue = 100 };
        IFlowState? capturedParent = null;

        var childTree = new FlowTree();
        childTree.Build(new ChildState(),
            Action<ChildState>(s =>
            {
                capturedParent = s.Parent;
                return NodeStatus.Success;
            })
        );

        var mainTree = new FlowTree();
        mainTree
            .WithCallStack(new FlowCallStack(32))
            .Build(parentState, 
                SubTree<ParentState, ChildState>(childTree, p => new ChildState())
            );

        mainTree.Tick(0.016f);

        Assert.NotNull(capturedParent);
        Assert.Same(parentState, capturedParent);
    }

    [Fact]
    public void SubTreeNode_StateInjection_DynamicTree()
    {
        var parentState = new ParentState { ParentValue = 5 };
        var childTree = new FlowTree();
        childTree.Build(new ChildState(),
            Action<ChildState>(s =>
            {
                var parent = (ParentState)s.Parent!;
                parent.ParentValue *= 2;
                return NodeStatus.Success;
            })
        );

        var mainTree = new FlowTree();
        mainTree
            .WithCallStack(new FlowCallStack(32))
            .Build(parentState, 
                SubTree<ParentState, ChildState>(
                    p => childTree,  // 動的ツリー
                    p => new ChildState { ChildValue = p.ParentValue }  // 親の値を使って子を初期化
                )
            );

        mainTree.Tick(0.016f);

        Assert.Equal(10, parentState.ParentValue); // 5 * 2
    }

    private class ParentState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int ParentValue { get; set; }
    }

    private class ChildState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int ChildValue { get; set; }
    }

    private class CounterState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Counter { get; set; }
    }

    private class PingPongState : IFlowState
    {
        public IFlowState? Parent { get; set; }
        public int Counter { get; set; }
        public string Log { get; set; } = "";
    }
}
