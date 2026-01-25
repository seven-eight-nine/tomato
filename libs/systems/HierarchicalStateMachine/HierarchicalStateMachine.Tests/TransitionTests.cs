using System;
using Tomato.HierarchicalStateMachine;
using Xunit;

namespace HierarchicalStateMachine.Tests;

public class TransitionTests
{
    private class TestContext
    {
        public int Value { get; set; }
        public bool AllowTransition { get; set; } = true;
    }

    [Fact]
    public void Constructor_StaticCost_SetsProperties()
    {
        var transition = new Transition<TestContext>("A", "B", 5f);

        Assert.Equal("A", transition.From.Value);
        Assert.Equal("B", transition.To.Value);
        Assert.Equal(5f, transition.BaseCost);
        Assert.False(transition.HasDynamicCost);
        Assert.False(transition.HasCondition);
    }

    [Fact]
    public void Constructor_DefaultCost_IsOne()
    {
        var transition = new Transition<TestContext>("A", "B");

        Assert.Equal(1f, transition.BaseCost);
    }

    [Fact]
    public void CanTransition_NoCondition_ReturnsTrue()
    {
        var transition = new Transition<TestContext>("A", "B");
        var context = new TestContext();

        Assert.True(transition.CanTransition(context));
    }

    [Fact]
    public void CanTransition_WithCondition_EvaluatesCondition()
    {
        var transition = new Transition<TestContext>("A", "B", 1f, ctx => ctx.AllowTransition);
        var context = new TestContext { AllowTransition = true };

        Assert.True(transition.CanTransition(context));

        context.AllowTransition = false;
        Assert.False(transition.CanTransition(context));
    }

    [Fact]
    public void GetCost_StaticCost_ReturnsBaseCost()
    {
        var transition = new Transition<TestContext>("A", "B", 5f);
        var context = new TestContext();

        Assert.Equal(5f, transition.GetCost(context));
    }

    [Fact]
    public void GetCost_DynamicCost_EvaluatesDynamicCost()
    {
        var transition = new Transition<TestContext>("A", "B", ctx => ctx.Value * 2f);
        var context = new TestContext { Value = 10 };

        Assert.Equal(20f, transition.GetCost(context));
        Assert.True(transition.HasDynamicCost);
    }

    [Fact]
    public void Constructor_ConditionAndDynamicCost_BothWork()
    {
        var transition = new Transition<TestContext>(
            "A", "B",
            ctx => ctx.AllowTransition,
            ctx => ctx.Value * 2f);

        var context = new TestContext { AllowTransition = true, Value = 5 };

        Assert.True(transition.HasCondition);
        Assert.True(transition.HasDynamicCost);
        Assert.True(transition.CanTransition(context));
        Assert.Equal(10f, transition.GetCost(context));

        context.AllowTransition = false;
        Assert.False(transition.CanTransition(context));
    }

    [Fact]
    public void Constructor_NullCondition_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Transition<TestContext>("A", "B", 1f, null!));
    }

    [Fact]
    public void Constructor_NullDynamicCost_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Transition<TestContext>("A", "B", (Func<TestContext, float>)null!));
    }
}
