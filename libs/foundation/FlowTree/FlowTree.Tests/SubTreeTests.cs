using Xunit;

namespace Tomato.FlowTree.Tests;

public class SubTreeTests
{
    private static FlowContext CreateContext(FlowCallStack? callStack = null)
    {
        return FlowContext.Create(
            new Blackboard(),
            callStack ?? new FlowCallStack(32)
        );
    }

    [Fact]
    public void SubTreeNode_DirectReference()
    {
        var childTree = new FlowTree();
        childTree.Build()
            .Action(static (ref FlowContext _) => NodeStatus.Success)
            .Complete();

        var subTreeNode = new SubTreeNode(childTree);
        var ctx = CreateContext();

        Assert.Equal(NodeStatus.Success, subTreeNode.Tick(ref ctx));
    }

    [Fact]
    public void SubTreeNode_UnbuiltTree()
    {
        var childTree = new FlowTree(); // Build()呼び出しなし

        var subTreeNode = new SubTreeNode(childTree);
        var ctx = CreateContext();

        // 構築されていないツリーはFailure
        Assert.Equal(NodeStatus.Failure, subTreeNode.Tick(ref ctx));
    }

    [Fact]
    public void SubTreeNode_Running()
    {
        int callCount = 0;

        var childTree = new FlowTree();
        childTree.Build()
            .Sequence()
                .Action((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
                .Action((ref FlowContext _) => { callCount++; return NodeStatus.Running; })
                .Action((ref FlowContext _) => { callCount++; return NodeStatus.Success; })
            .End()
            .Complete();

        var subTreeNode = new SubTreeNode(childTree);
        var ctx = CreateContext();

        // 1回目: Running
        Assert.Equal(NodeStatus.Running, subTreeNode.Tick(ref ctx));
        Assert.Equal(2, callCount);

        // 2回目: まだRunning（Sequenceの2番目がRunningを返し続ける）
        Assert.Equal(NodeStatus.Running, subTreeNode.Tick(ref ctx));
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void SubTreeNode_NestedSubTrees()
    {
        var executed = new bool[3];

        // Tree 3: 葉ノード
        var tree3 = new FlowTree();
        tree3.Build()
            .Action((ref FlowContext _) => { executed[2] = true; return NodeStatus.Success; })
            .Complete();

        // Tree 2: Tree 3を呼ぶ
        var tree2 = new FlowTree();
        tree2.Build()
            .Sequence()
                .Action((ref FlowContext _) => { executed[1] = true; return NodeStatus.Success; })
                .SubTree(tree3)
            .End()
            .Complete();

        // Tree 1: Tree 2を呼ぶ
        var tree1 = new FlowTree();
        tree1.Build()
            .Sequence()
                .Action((ref FlowContext _) => { executed[0] = true; return NodeStatus.Success; })
                .SubTree(tree2)
            .End()
            .Complete();

        var ctx = CreateContext();
        Assert.Equal(NodeStatus.Success, tree1.Tick(ref ctx));

        Assert.True(executed[0]);
        Assert.True(executed[1]);
        Assert.True(executed[2]);
    }

    [Fact]
    public void SubTreeNode_CallStackOverflow()
    {
        // 自己再帰的なツリー（終了条件なし）
        var recursiveTree = new FlowTree();
        recursiveTree.Build()
            .Sequence()
                .Action(static (ref FlowContext _) => NodeStatus.Success)
                .SubTree(recursiveTree) // 自己呼び出し
            .End()
            .Complete();

        var ctx = FlowContext.Create(
            new Blackboard(),
            new FlowCallStack(5), // 小さなスタック
            maxCallDepth: 5
        );

        // スタックオーバーフローでFailure
        Assert.Equal(NodeStatus.Failure, recursiveTree.Tick(ref ctx));
    }

    [Fact]
    public void SubTreeNode_SelfRecursion()
    {
        // 終了条件付き自己再帰
        var counterKey = new BlackboardKey<int>(1);

        var countdown = new FlowTree();
        countdown.Build()
            .Selector()
                // 終了条件: counter <= 0
                .Sequence()
                    .Condition((ref FlowContext ctx) => ctx.Blackboard.GetInt(counterKey) <= 0)
                    .Success()
                .End()
                // 再帰: counter-- して自己呼び出し
                .Sequence()
                    .Action((ref FlowContext ctx) =>
                    {
                        var counter = ctx.Blackboard.GetInt(counterKey);
                        ctx.Blackboard.SetInt(counterKey, counter - 1);
                        return NodeStatus.Success;
                    })
                    .SubTree(countdown)
                .End()
            .End()
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(counterKey, 5);

        var status = countdown.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal(0, ctx.Blackboard.GetInt(counterKey));
    }

    [Fact]
    public void SubTreeNode_MutualRecursion()
    {
        // 相互再帰: ping → pong → ping → ...
        var counterKey = new BlackboardKey<int>(1);
        var logKey = new BlackboardKey<string>(2);

        var ping = new FlowTree("ping");
        var pong = new FlowTree("pong");

        ping.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var log = ctx.Blackboard.GetString(logKey, "") ?? "";
                    ctx.Blackboard.SetString(logKey, log + "P");
                    var counter = ctx.Blackboard.GetInt(counterKey);
                    ctx.Blackboard.SetInt(counterKey, counter - 1);
                    return NodeStatus.Success;
                })
                .Selector()
                    .Sequence()
                        .Condition((ref FlowContext ctx) => ctx.Blackboard.GetInt(counterKey) > 0)
                        .SubTree(pong)
                    .End()
                    .Success()
                .End()
            .End()
            .Complete();

        pong.Build()
            .Sequence()
                .Action((ref FlowContext ctx) =>
                {
                    var log = ctx.Blackboard.GetString(logKey, "") ?? "";
                    ctx.Blackboard.SetString(logKey, log + "O");
                    var counter = ctx.Blackboard.GetInt(counterKey);
                    ctx.Blackboard.SetInt(counterKey, counter - 1);
                    return NodeStatus.Success;
                })
                .Selector()
                    .Sequence()
                        .Condition((ref FlowContext ctx) => ctx.Blackboard.GetInt(counterKey) > 0)
                        .SubTree(ping)
                    .End()
                    .Success()
                .End()
            .End()
            .Complete();

        var ctx = CreateContext();
        ctx.Blackboard.SetInt(counterKey, 6);
        ctx.Blackboard.SetString(logKey, "");

        var status = ping.Tick(ref ctx);

        Assert.Equal(NodeStatus.Success, status);
        Assert.Equal("POPOPO", ctx.Blackboard.GetString(logKey));
    }
}
