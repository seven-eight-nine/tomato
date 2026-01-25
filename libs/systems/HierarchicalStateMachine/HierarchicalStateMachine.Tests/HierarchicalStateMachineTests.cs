using System;
using Tomato.HierarchicalStateMachine;
using Xunit;

namespace HierarchicalStateMachine.Tests;

public class HierarchicalStateMachineTests
{
    private class TestContext
    {
        public int EnterCount { get; set; }
        public int ExitCount { get; set; }
        public int UpdateCount { get; set; }
        public string LastEnteredState { get; set; } = "";
        public string LastExitedState { get; set; } = "";
    }

    private class TrackingState : StateBase<TestContext>
    {
        public TrackingState(StateId id) : base(id) { }

        public override void OnEnter(TestContext context)
        {
            context.EnterCount++;
            context.LastEnteredState = Id.Value;
        }

        public override void OnExit(TestContext context)
        {
            context.ExitCount++;
            context.LastExitedState = Id.Value;
        }

        public override void OnUpdate(TestContext context, float deltaTime)
        {
            context.UpdateCount++;
        }
    }

    private StateGraph<TestContext> CreateSimpleGraph()
    {
        return new StateGraph<TestContext>()
            .AddState(new TrackingState("A"))
            .AddState(new TrackingState("B"))
            .AddState(new TrackingState("C"))
            .AddTransition(new Transition<TestContext>("A", "B", 1f))
            .AddTransition(new Transition<TestContext>("B", "C", 1f))
            .AddTransition(new Transition<TestContext>("A", "C", 3f));
    }

    [Fact]
    public void Initialize_SetsCurrentState()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);

        Assert.Equal("A", sm.CurrentStateId!.Value.Value);
        Assert.Equal(1, context.EnterCount);
        Assert.Equal("A", context.LastEnteredState);
    }

    [Fact]
    public void Initialize_InvalidState_ThrowsException()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        Assert.Throws<InvalidOperationException>(() => sm.Initialize("X", context));
    }

    [Fact]
    public void PlanPath_FindsValidPath()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        var path = sm.PlanPath("C", context);

        Assert.Equal(PathfindingResult.Found, path.Result);
        Assert.NotNull(sm.CurrentPath);
        Assert.Equal(0, sm.CurrentPathIndex);
    }

    [Fact]
    public void PlanPath_NotInitialized_ThrowsException()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        Assert.Throws<InvalidOperationException>(() => sm.PlanPath("C", context));
    }

    [Fact]
    public void ExecuteNextStep_TransitionsToNextState()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        sm.PlanPath("C", context);

        Assert.True(sm.ExecuteNextStep(context));
        Assert.Equal("B", sm.CurrentStateId!.Value.Value);
        Assert.Equal(1, sm.CurrentPathIndex);
        Assert.Equal("A", context.LastExitedState);
        Assert.Equal("B", context.LastEnteredState);
    }

    [Fact]
    public void ExecuteAllSteps_CompletesPath()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        sm.PlanPath("C", context);

        Assert.True(sm.ExecuteAllSteps(context));
        Assert.Equal("C", sm.CurrentStateId!.Value.Value);
        Assert.True(sm.IsPathComplete);
    }

    [Fact]
    public void ExecuteNextStep_NoPath_ReturnsFalse()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);

        Assert.False(sm.ExecuteNextStep(context));
    }

    [Fact]
    public void TransitionTo_ValidTransition_Succeeds()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        Assert.True(sm.TransitionTo("B", context));

        Assert.Equal("B", sm.CurrentStateId!.Value.Value);
        Assert.Null(sm.CurrentPath);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_ReturnsFalse()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        // No direct transition from A to non-existent state
        Assert.False(sm.TransitionTo("X", context));

        Assert.Equal("A", sm.CurrentStateId!.Value.Value);
    }

    [Fact]
    public void TransitionTo_NoDirectTransition_ReturnsFalse()
    {
        var graph = new StateGraph<TestContext>()
            .AddState(new TrackingState("A"))
            .AddState(new TrackingState("B"))
            .AddState(new TrackingState("C"))
            .AddTransition(new Transition<TestContext>("A", "B", 1f))
            .AddTransition(new Transition<TestContext>("B", "C", 1f));

        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        // No direct transition from A to C
        Assert.False(sm.TransitionTo("C", context));
    }

    [Fact]
    public void ForceTransitionTo_BypassesTransitionCheck()
    {
        var graph = new StateGraph<TestContext>()
            .AddState(new TrackingState("A"))
            .AddState(new TrackingState("B"))
            .AddState(new TrackingState("C"));
        // No transitions defined

        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        sm.ForceTransitionTo("C", context);

        Assert.Equal("C", sm.CurrentStateId!.Value.Value);
    }

    [Fact]
    public void ForceTransitionTo_InvalidState_ThrowsException()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);

        Assert.Throws<InvalidOperationException>(() => sm.ForceTransitionTo("X", context));
    }

    [Fact]
    public void Update_CallsCurrentStateUpdate()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        sm.Update(context, 0.016f);

        Assert.Equal(1, context.UpdateCount);
    }

    [Fact]
    public void ClearPath_ClearsCurrentPath()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        sm.PlanPath("C", context);
        sm.ClearPath();

        Assert.Null(sm.CurrentPath);
        Assert.Equal(0, sm.CurrentPathIndex);
    }

    [Fact]
    public void ReplanPath_ReplansToSameGoal()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        sm.PlanPath("C", context);
        sm.ExecuteNextStep(context); // Move to B

        var newPath = sm.ReplanPath(context);

        Assert.NotNull(newPath);
        Assert.Equal(PathfindingResult.Found, newPath!.Result);
        Assert.Equal("B", sm.CurrentStateId!.Value.Value);
    }

    [Fact]
    public void ReplanPath_NoExistingPath_ReturnsNull()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);

        var result = sm.ReplanPath(context);

        Assert.Null(result);
    }

    [Fact]
    public void PlanPathToAny_Works()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        sm.Initialize("A", context);
        var path = sm.PlanPathToAny(new[] { new StateId("B"), new StateId("C") }, context);

        Assert.Equal(PathfindingResult.Found, path.Result);
        // B is closer than C
        Assert.Equal("B", path.States[path.States.Count - 1].Value);
    }

    [Fact]
    public void Graph_ReturnsStateGraph()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);

        Assert.Same(graph, sm.Graph);
    }

    [Fact]
    public void CurrentState_ReturnsCorrectState()
    {
        var graph = CreateSimpleGraph();
        var sm = new HierarchicalStateMachine<TestContext>(graph);
        var context = new TestContext();

        Assert.Null(sm.CurrentState);

        sm.Initialize("A", context);

        Assert.NotNull(sm.CurrentState);
        Assert.Equal("A", sm.CurrentState!.Id.Value);
    }
}
