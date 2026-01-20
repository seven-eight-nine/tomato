using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Tomato.ReconciliationSystem;
using Tomato.CommandGenerator;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem.Tests;

internal static class TestExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item) where T : IEquatable<T>
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Equals(item))
                return i;
        }
        return -1;
    }
}

/// <summary>
/// DependencyResolver テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] DependencyResolverを作成できる
/// - [x] 依存関係のない単一Entityはそのまま返す
/// - [x] 依存先が先に処理される順序を返す
/// - [x] 複雑な依存関係でも正しい順序を返す
/// - [x] 循環依存を検出した場合はnullを返す
/// - [x] 独立したEntityは任意の順序で返される
/// </summary>
public class DependencyResolverTests
{
    private readonly MockArena _arena = new();

    [Fact]
    public void DependencyResolver_ShouldBeCreatable()
    {
        var graph = new DependencyGraph();
        var resolver = new DependencyResolver(graph);

        Assert.NotNull(resolver);
    }

    [Fact]
    public void ComputeOrder_SingleEntity_ShouldReturnSingleEntity()
    {
        var graph = new DependencyGraph();
        var resolver = new DependencyResolver(graph);
        var entity = _arena.CreateHandle(1);

        var order = resolver.ComputeOrder(new[] { entity });

        Assert.NotNull(order);
        Assert.Single(order);
        Assert.Equal(entity, order[0]);
    }

    [Fact]
    public void ComputeOrder_SimpleDependency_ShouldReturnDependencyFirst()
    {
        // rider -> horse の場合、horse -> rider の順序
        var graph = new DependencyGraph();
        var rider = _arena.CreateHandle(1);
        var horse = _arena.CreateHandle(2);

        graph.AddDependency(rider, horse);

        var resolver = new DependencyResolver(graph);
        var order = resolver.ComputeOrder(new[] { rider, horse });

        Assert.NotNull(order);
        Assert.Equal(2, order.Count);

        // horseが先
        var horseIndex = order.IndexOf(horse);
        var riderIndex = order.IndexOf(rider);
        Assert.True(horseIndex < riderIndex, "Dependency (horse) should come before dependent (rider)");
    }

    [Fact]
    public void ComputeOrder_ChainedDependencies_ShouldReturnCorrectOrder()
    {
        // A -> B -> C の場合、C -> B -> A の順序
        var graph = new DependencyGraph();
        var a = _arena.CreateHandle(1);
        var b = _arena.CreateHandle(2);
        var c = _arena.CreateHandle(3);

        graph.AddDependency(a, b);
        graph.AddDependency(b, c);

        var resolver = new DependencyResolver(graph);
        var order = resolver.ComputeOrder(new[] { a, b, c });

        Assert.NotNull(order);
        Assert.Equal(3, order.Count);

        var aIndex = order.IndexOf(a);
        var bIndex = order.IndexOf(b);
        var cIndex = order.IndexOf(c);

        Assert.True(cIndex < bIndex, "C should come before B");
        Assert.True(bIndex < aIndex, "B should come before A");
    }

    [Fact]
    public void ComputeOrder_CircularDependency_ShouldReturnNull()
    {
        // A -> B -> C -> A の循環
        var graph = new DependencyGraph();
        var a = _arena.CreateHandle(1);
        var b = _arena.CreateHandle(2);
        var c = _arena.CreateHandle(3);

        graph.AddDependency(a, b);
        graph.AddDependency(b, c);
        graph.AddDependency(c, a);

        var resolver = new DependencyResolver(graph);
        var order = resolver.ComputeOrder(new[] { a, b, c });

        Assert.Null(order);
    }

    [Fact]
    public void ComputeOrder_SelfLoop_ShouldReturnNull()
    {
        // A -> A の自己ループ
        var graph = new DependencyGraph();
        var a = _arena.CreateHandle(1);

        graph.AddDependency(a, a);

        var resolver = new DependencyResolver(graph);
        var order = resolver.ComputeOrder(new[] { a });

        Assert.Null(order);
    }

    [Fact]
    public void ComputeOrder_IndependentEntities_ShouldReturnAll()
    {
        var graph = new DependencyGraph();
        var a = _arena.CreateHandle(1);
        var b = _arena.CreateHandle(2);
        var c = _arena.CreateHandle(3);

        // 依存関係なし

        var resolver = new DependencyResolver(graph);
        var order = resolver.ComputeOrder(new[] { a, b, c });

        Assert.NotNull(order);
        Assert.Equal(3, order.Count);
        Assert.Contains(a, order);
        Assert.Contains(b, order);
        Assert.Contains(c, order);
    }

    [Fact]
    public void ComputeOrder_MultipleDependencies_ShouldRespectAll()
    {
        // A -> B, A -> C の場合、BとCはAの前
        var graph = new DependencyGraph();
        var a = _arena.CreateHandle(1);
        var b = _arena.CreateHandle(2);
        var c = _arena.CreateHandle(3);

        graph.AddDependency(a, b);
        graph.AddDependency(a, c);

        var resolver = new DependencyResolver(graph);
        var order = resolver.ComputeOrder(new[] { a, b, c });

        Assert.NotNull(order);
        Assert.Equal(3, order.Count);

        var aIndex = order.IndexOf(a);
        var bIndex = order.IndexOf(b);
        var cIndex = order.IndexOf(c);

        Assert.True(bIndex < aIndex, "B should come before A");
        Assert.True(cIndex < aIndex, "C should come before A");
    }

    [Fact]
    public void ComputeOrder_DiamondDependency_ShouldHandleCorrectly()
    {
        // Diamond: A -> B, A -> C, B -> D, C -> D
        var graph = new DependencyGraph();
        var a = _arena.CreateHandle(1);
        var b = _arena.CreateHandle(2);
        var c = _arena.CreateHandle(3);
        var d = _arena.CreateHandle(4);

        graph.AddDependency(a, b);
        graph.AddDependency(a, c);
        graph.AddDependency(b, d);
        graph.AddDependency(c, d);

        var resolver = new DependencyResolver(graph);
        var order = resolver.ComputeOrder(new[] { a, b, c, d });

        Assert.NotNull(order);
        Assert.Equal(4, order.Count);

        var aIndex = order.IndexOf(a);
        var bIndex = order.IndexOf(b);
        var cIndex = order.IndexOf(c);
        var dIndex = order.IndexOf(d);

        Assert.True(dIndex < bIndex, "D should come before B");
        Assert.True(dIndex < cIndex, "D should come before C");
        Assert.True(bIndex < aIndex, "B should come before A");
        Assert.True(cIndex < aIndex, "C should come before A");
    }

    #region Helper Classes

    private class MockArena : IEntityArena
    {
        public VoidHandle CreateHandle(int index) => new VoidHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    #endregion
}
