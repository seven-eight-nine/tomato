using Tomato.HierarchicalStateMachine;
using Xunit;

namespace HierarchicalStateMachine.Tests;

public class HierarchicalStateTests
{
    private class TestContext
    {
        public int EnterCount { get; set; }
        public int ExitCount { get; set; }
        public int UpdateCount { get; set; }
    }

    private class TrackingState : StateBase<TestContext>
    {
        public TrackingState(StateId id) : base(id) { }

        public override void OnEnter(TestContext context) => context.EnterCount++;
        public override void OnExit(TestContext context) => context.ExitCount++;
        public override void OnUpdate(TestContext context, float deltaTime) => context.UpdateCount++;
    }

    [Fact]
    public void Constructor_WithoutSubGraph_HasNoSubGraph()
    {
        var state = new HierarchicalState<TestContext>("Parent");

        Assert.False(state.HasSubGraph);
        Assert.Null(state.SubGraph);
        Assert.Null(state.InitialSubStateId);
    }

    [Fact]
    public void Constructor_WithSubGraph_HasSubGraph()
    {
        var subGraph = new StateGraph<TestContext>();
        subGraph.AddState(new TrackingState("Child1"));
        subGraph.AddState(new TrackingState("Child2"));

        var state = new HierarchicalState<TestContext>("Parent", subGraph, "Child1");

        Assert.True(state.HasSubGraph);
        Assert.Same(subGraph, state.SubGraph);
        Assert.Equal("Child1", state.InitialSubStateId!.Value.Value);
    }

    [Fact]
    public void OnEnter_WithSubGraph_EntersInitialSubState()
    {
        var context = new TestContext();
        var subGraph = new StateGraph<TestContext>();
        subGraph.AddState(new TrackingState("Child1"));
        subGraph.AddState(new TrackingState("Child2"));

        var state = new HierarchicalState<TestContext>("Parent", subGraph, "Child1");
        state.OnEnter(context);

        Assert.Equal("Child1", state.CurrentSubStateId!.Value.Value);
        Assert.Equal(1, context.EnterCount);
    }

    [Fact]
    public void OnExit_WithSubGraph_ExitsCurrentSubState()
    {
        var context = new TestContext();
        var subGraph = new StateGraph<TestContext>();
        subGraph.AddState(new TrackingState("Child1"));

        var state = new HierarchicalState<TestContext>("Parent", subGraph, "Child1");
        state.OnEnter(context);
        state.OnExit(context);

        Assert.Null(state.CurrentSubStateId);
        Assert.Equal(1, context.ExitCount);
    }

    [Fact]
    public void OnUpdate_WithSubGraph_UpdatesCurrentSubState()
    {
        var context = new TestContext();
        var subGraph = new StateGraph<TestContext>();
        subGraph.AddState(new TrackingState("Child1"));

        var state = new HierarchicalState<TestContext>("Parent", subGraph, "Child1");
        state.OnEnter(context);
        state.OnUpdate(context, 0.016f);

        Assert.Equal(1, context.UpdateCount);
    }

    [Fact]
    public void EnterSubState_ChangesCurrentSubState()
    {
        var context = new TestContext();
        var subGraph = new StateGraph<TestContext>();
        subGraph.AddState(new TrackingState("Child1"));
        subGraph.AddState(new TrackingState("Child2"));

        var state = new HierarchicalState<TestContext>("Parent", subGraph, "Child1");
        state.OnEnter(context);

        state.EnterSubState("Child2", context);

        Assert.Equal("Child2", state.CurrentSubStateId!.Value.Value);
        Assert.Equal(2, context.EnterCount); // Child1 + Child2
        Assert.Equal(1, context.ExitCount);  // Child1
    }

    [Fact]
    public void ExitSubState_ClearsCurrentSubState()
    {
        var context = new TestContext();
        var subGraph = new StateGraph<TestContext>();
        subGraph.AddState(new TrackingState("Child1"));

        var state = new HierarchicalState<TestContext>("Parent", subGraph, "Child1");
        state.OnEnter(context);
        state.ExitSubState(context);

        Assert.Null(state.CurrentSubStateId);
        Assert.Equal(1, context.ExitCount);
    }

    [Fact]
    public void EnterSubState_WithoutSubGraph_DoesNothing()
    {
        var context = new TestContext();
        var state = new HierarchicalState<TestContext>("Parent");

        // Should not throw
        state.EnterSubState("NonExistent", context);
        state.ExitSubState(context);

        Assert.Null(state.CurrentSubStateId);
    }
}
