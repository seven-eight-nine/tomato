using System;
using System.Linq;
using Tomato.HierarchicalStateMachine;
using Xunit;

namespace HierarchicalStateMachine.Tests;

public class StateGraphTests
{
    private class TestContext { }

    private class TestState : StateBase<TestContext>
    {
        public TestState(StateId id) : base(id) { }
    }

    [Fact]
    public void AddState_ValidState_AddsSuccessfully()
    {
        var graph = new StateGraph<TestContext>();
        var state = new TestState("A");

        graph.AddState(state);

        Assert.True(graph.HasState("A"));
        Assert.Equal(1, graph.StateCount);
    }

    [Fact]
    public void AddState_DuplicateState_ThrowsException()
    {
        var graph = new StateGraph<TestContext>();
        graph.AddState(new TestState("A"));

        Assert.Throws<InvalidOperationException>(() =>
            graph.AddState(new TestState("A")));
    }

    [Fact]
    public void AddState_NullState_ThrowsException()
    {
        var graph = new StateGraph<TestContext>();

        Assert.Throws<ArgumentNullException>(() => graph.AddState(null!));
    }

    [Fact]
    public void AddTransition_ValidTransition_AddsSuccessfully()
    {
        var graph = new StateGraph<TestContext>();
        graph.AddState(new TestState("A"));
        graph.AddState(new TestState("B"));

        graph.AddTransition(new Transition<TestContext>("A", "B"));

        var transitions = graph.GetTransitionsFrom("A").ToList();
        Assert.Single(transitions);
        Assert.Equal("B", transitions[0].To.Value);
    }

    [Fact]
    public void AddTransition_InvalidSource_ThrowsException()
    {
        var graph = new StateGraph<TestContext>();
        graph.AddState(new TestState("B"));

        Assert.Throws<InvalidOperationException>(() =>
            graph.AddTransition(new Transition<TestContext>("A", "B")));
    }

    [Fact]
    public void AddTransition_InvalidTarget_ThrowsException()
    {
        var graph = new StateGraph<TestContext>();
        graph.AddState(new TestState("A"));

        Assert.Throws<InvalidOperationException>(() =>
            graph.AddTransition(new Transition<TestContext>("A", "B")));
    }

    [Fact]
    public void AddTransition_FromAnyState_AddsToAnyStateTransitions()
    {
        var graph = new StateGraph<TestContext>();
        graph.AddState(new TestState("A"));
        graph.AddState(new TestState("B"));
        graph.AddState(new TestState("C"));

        graph.AddTransition(new Transition<TestContext>(StateId.Any, "C"));

        Assert.Single(graph.AnyStateTransitions);
        Assert.Equal("C", graph.AnyStateTransitions[0].To.Value);
    }

    [Fact]
    public void GetTransitionsFrom_IncludesAnyStateTransitions()
    {
        var graph = new StateGraph<TestContext>();
        graph.AddState(new TestState("A"));
        graph.AddState(new TestState("B"));
        graph.AddState(new TestState("C"));

        graph.AddTransition(new Transition<TestContext>("A", "B"));
        graph.AddTransition(new Transition<TestContext>(StateId.Any, "C"));

        var transitions = graph.GetTransitionsFrom("A").ToList();

        Assert.Equal(2, transitions.Count);
        Assert.Contains(transitions, t => t.To == new StateId("B"));
        Assert.Contains(transitions, t => t.To == new StateId("C"));
    }

    [Fact]
    public void GetTransitionsFrom_ExcludesSelfTransitionFromAnyState()
    {
        var graph = new StateGraph<TestContext>();
        graph.AddState(new TestState("A"));
        graph.AddState(new TestState("B"));

        graph.AddTransition(new Transition<TestContext>(StateId.Any, "A"));

        var transitions = graph.GetTransitionsFrom("A").ToList();

        // A から A への Any State 遷移は除外される
        Assert.Empty(transitions);
    }

    [Fact]
    public void GetState_ExistingState_ReturnsState()
    {
        var graph = new StateGraph<TestContext>();
        var state = new TestState("A");
        graph.AddState(state);

        var result = graph.GetState("A");

        Assert.Same(state, result);
    }

    [Fact]
    public void GetState_NonExistingState_ReturnsNull()
    {
        var graph = new StateGraph<TestContext>();

        var result = graph.GetState("A");

        Assert.Null(result);
    }

    [Fact]
    public void FluentApi_Works()
    {
        var graph = new StateGraph<TestContext>()
            .AddState(new TestState("A"))
            .AddState(new TestState("B"))
            .AddState(new TestState("C"))
            .AddTransition(new Transition<TestContext>("A", "B"))
            .AddTransition(new Transition<TestContext>("B", "C"));

        Assert.Equal(3, graph.StateCount);
        Assert.Single(graph.GetTransitionsFrom("A").ToList());
        Assert.Single(graph.GetTransitionsFrom("B").ToList());
    }
}
