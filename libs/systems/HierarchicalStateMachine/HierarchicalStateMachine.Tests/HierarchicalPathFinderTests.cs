using System.Linq;
using Tomato.HierarchicalStateMachine;
using Xunit;

namespace HierarchicalStateMachine.Tests;

public class HierarchicalPathFinderTests
{
    private class TestContext
    {
        public bool BlockTransition { get; set; }
        public int CostMultiplier { get; set; } = 1;
    }

    private class TestState : StateBase<TestContext>
    {
        public TestState(StateId id) : base(id) { }
    }

    private StateGraph<TestContext> CreateSimpleGraph()
    {
        // A -> B -> C -> D
        return new StateGraph<TestContext>()
            .AddState(new TestState("A"))
            .AddState(new TestState("B"))
            .AddState(new TestState("C"))
            .AddState(new TestState("D"))
            .AddTransition(new Transition<TestContext>("A", "B", 1f))
            .AddTransition(new Transition<TestContext>("B", "C", 1f))
            .AddTransition(new Transition<TestContext>("C", "D", 1f));
    }

    private StateGraph<TestContext> CreateGraphWithMultiplePaths()
    {
        //     B (cost 1)
        //    / \
        // A      D
        //    \ /
        //     C (cost 10)
        return new StateGraph<TestContext>()
            .AddState(new TestState("A"))
            .AddState(new TestState("B"))
            .AddState(new TestState("C"))
            .AddState(new TestState("D"))
            .AddTransition(new Transition<TestContext>("A", "B", 1f))
            .AddTransition(new Transition<TestContext>("A", "C", 1f))
            .AddTransition(new Transition<TestContext>("B", "D", 1f))
            .AddTransition(new Transition<TestContext>("C", "D", 10f));
    }

    [Fact]
    public void FindPath_SimpleLinearPath_FindsPath()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPath("A", "D", context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.True(result.IsValid);
        Assert.Equal(3, result.Transitions.Count);
        Assert.Equal(4, result.States.Count);
        Assert.Equal("A", result.States[0].Value);
        Assert.Equal("D", result.States[3].Value);
    }

    [Fact]
    public void FindPath_StartEqualsGoal_ReturnsEmptyPath()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPath("A", "A", context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.True(result.IsEmpty);
        Assert.Single(result.States);
        Assert.Equal("A", result.States[0].Value);
    }

    [Fact]
    public void FindPath_InvalidStart_ReturnsInvalidStart()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPath("X", "D", context);

        Assert.Equal(PathfindingResult.InvalidStart, result.Result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FindPath_InvalidGoal_ReturnsInvalidGoal()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPath("A", "X", context);

        Assert.Equal(PathfindingResult.InvalidGoal, result.Result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FindPath_NoPathExists_ReturnsNotFound()
    {
        var graph = new StateGraph<TestContext>()
            .AddState(new TestState("A"))
            .AddState(new TestState("B"));
        // No transitions

        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPath("A", "B", context);

        Assert.Equal(PathfindingResult.NotFound, result.Result);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FindPath_MultiplePaths_FindsShortestPath()
    {
        var graph = CreateGraphWithMultiplePaths();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPath("A", "D", context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.Equal(2f, result.TotalCost);
        Assert.Equal(3, result.States.Count);
        Assert.Equal("A", result.States[0].Value);
        Assert.Equal("B", result.States[1].Value);
        Assert.Equal("D", result.States[2].Value);
    }

    [Fact]
    public void FindPath_WithCondition_RespectsCondition()
    {
        var graph = new StateGraph<TestContext>()
            .AddState(new TestState("A"))
            .AddState(new TestState("B"))
            .AddState(new TestState("C"))
            .AddTransition(new Transition<TestContext>("A", "B", 1f, ctx => !ctx.BlockTransition))
            .AddTransition(new Transition<TestContext>("A", "C", 10f));

        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext { BlockTransition = true };

        var result = finder.FindPath("A", "C", context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.Equal("C", result.States[1].Value);
    }

    [Fact]
    public void FindPath_WithDynamicCost_UsesDynamicCost()
    {
        var graph = new StateGraph<TestContext>()
            .AddState(new TestState("A"))
            .AddState(new TestState("B"))
            .AddState(new TestState("C"))
            .AddTransition(new Transition<TestContext>("A", "B", ctx => ctx.CostMultiplier * 1f))
            .AddTransition(new Transition<TestContext>("A", "C", 5f))
            .AddTransition(new Transition<TestContext>("B", "C", 1f));

        var finder = new HierarchicalPathFinder<TestContext>(graph);

        // Low cost multiplier - go through B
        var context1 = new TestContext { CostMultiplier = 1 };
        var result1 = finder.FindPath("A", "C", context1);
        Assert.Equal(3, result1.States.Count);
        Assert.Equal("B", result1.States[1].Value);

        // High cost multiplier - go directly to C
        var context2 = new TestContext { CostMultiplier = 100 };
        var result2 = finder.FindPath("A", "C", context2);
        Assert.Equal(2, result2.States.Count);
        Assert.Equal("C", result2.States[1].Value);
    }

    [Fact]
    public void FindPath_WithAnyState_UsesAnyStateTransitions()
    {
        var graph = new StateGraph<TestContext>()
            .AddState(new TestState("A"))
            .AddState(new TestState("B"))
            .AddState(new TestState("Emergency"))
            .AddTransition(new Transition<TestContext>("A", "B", 10f))
            .AddTransition(new Transition<TestContext>(StateId.Any, "Emergency", 1f));

        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPath("A", "Emergency", context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.Equal(1f, result.TotalCost);
        Assert.Equal(2, result.States.Count);
    }

    [Fact]
    public void FindPath_Timeout_ReturnsTimeout()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();
        var options = new PathfindingOptions { MaxIterations = 1 };

        var result = finder.FindPath("A", "D", context, options);

        Assert.Equal(PathfindingResult.Timeout, result.Result);
    }

    [Fact]
    public void FindPath_TimeoutWithPartialPath_ReturnsPartialPath()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();
        var options = new PathfindingOptions { MaxIterations = 2, AllowPartialPath = true };

        var result = finder.FindPath("A", "D", context, options);

        Assert.Equal(PathfindingResult.Timeout, result.Result);
        Assert.True(result.States.Count > 0);
    }

    [Fact]
    public void FindPathToAny_FindsClosestGoal()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPathToAny("A", new[] { new StateId("C"), new StateId("D") }, context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.Equal("C", result.States.Last().Value);
        Assert.Equal(2f, result.TotalCost);
    }

    [Fact]
    public void FindPathToAny_StartIsGoal_ReturnsImmediately()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPathToAny("A", new[] { new StateId("A"), new StateId("D") }, context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.True(result.IsEmpty);
        Assert.Single(result.States);
    }

    [Fact]
    public void FindPathToAny_NoGoals_ReturnsInvalidGoal()
    {
        var graph = CreateSimpleGraph();
        var finder = new HierarchicalPathFinder<TestContext>(graph);
        var context = new TestContext();

        var result = finder.FindPathToAny("A", System.Array.Empty<StateId>(), context);

        Assert.Equal(PathfindingResult.InvalidGoal, result.Result);
    }

    [Fact]
    public void FindPath_WithHeuristic_UsesHeuristic()
    {
        var graph = CreateGraphWithMultiplePaths();
        var heuristic = new DelegateHeuristic<TestContext>((current, goal, ctx) =>
        {
            // Simple heuristic
            return 0f;
        });
        var finder = new HierarchicalPathFinder<TestContext>(graph, heuristic);
        var context = new TestContext();

        var result = finder.FindPath("A", "D", context);

        Assert.Equal(PathfindingResult.Found, result.Result);
        Assert.Equal(2f, result.TotalCost);
    }
}
