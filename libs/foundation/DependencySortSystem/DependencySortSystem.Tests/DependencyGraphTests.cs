using System.Linq;
using Xunit;

namespace Tomato.DependencySortSystem.Tests;

/// <summary>
/// DependencyGraph テスト
/// </summary>
public class DependencyGraphTests
{
    [Fact]
    public void DependencyGraph_ShouldBeCreatable()
    {
        var graph = new DependencyGraph<int>();

        Assert.NotNull(graph);
        Assert.Equal(0, graph.NodeCount);
    }

    [Fact]
    public void AddDependency_ShouldAddDependency()
    {
        var graph = new DependencyGraph<string>();

        // riderがhorseに依存
        graph.AddDependency("rider", "horse");

        var dependencies = graph.GetDependencies("rider");
        Assert.Single(dependencies);
        Assert.Equal("horse", dependencies[0]);
    }

    [Fact]
    public void GetDependencies_ShouldReturnEmptyForUnknownNode()
    {
        var graph = new DependencyGraph<int>();

        var dependencies = graph.GetDependencies(999);

        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetDependents_ShouldReturnDependents()
    {
        var graph = new DependencyGraph<string>();

        graph.AddDependency("rider", "horse");

        // horseの依存元はrider
        var dependents = graph.GetDependents("horse");
        Assert.Single(dependents);
        Assert.Equal("rider", dependents[0]);
    }

    [Fact]
    public void GetDependents_ShouldReturnEmptyForUnknownNode()
    {
        var graph = new DependencyGraph<int>();

        var dependents = graph.GetDependents(999);

        Assert.Empty(dependents);
    }

    [Fact]
    public void RemoveDependency_ShouldRemoveDependency()
    {
        var graph = new DependencyGraph<string>();

        graph.AddDependency("rider", "horse");
        graph.RemoveDependency("rider", "horse");

        Assert.Empty(graph.GetDependencies("rider"));
        Assert.Empty(graph.GetDependents("horse"));
    }

    [Fact]
    public void RemoveNode_ShouldRemoveAllRelatedDependencies()
    {
        var graph = new DependencyGraph<string>();

        // rider -> horse, saddle -> horse
        graph.AddDependency("rider", "horse");
        graph.AddDependency("saddle", "horse");

        // horseを削除
        graph.RemoveNode("horse");

        // horseへの依存が全て削除される
        Assert.Empty(graph.GetDependencies("rider"));
        Assert.Empty(graph.GetDependencies("saddle"));
        Assert.Empty(graph.GetDependents("horse"));
    }

    [Fact]
    public void RemoveNode_ShouldRemoveDependenciesFromNode()
    {
        var graph = new DependencyGraph<string>();

        // rider -> horse, rider -> weapon
        graph.AddDependency("rider", "horse");
        graph.AddDependency("rider", "weapon");

        // riderを削除
        graph.RemoveNode("rider");

        // riderからの依存が全て削除される
        Assert.Empty(graph.GetDependents("horse"));
        Assert.Empty(graph.GetDependents("weapon"));
    }

    [Fact]
    public void AddDependency_ShouldNotAddDuplicate()
    {
        var graph = new DependencyGraph<string>();

        graph.AddDependency("rider", "horse");
        graph.AddDependency("rider", "horse");  // 重複

        Assert.Single(graph.GetDependencies("rider"));
        Assert.Single(graph.GetDependents("horse"));
    }

    [Fact]
    public void Clear_ShouldClearAllDependencies()
    {
        var graph = new DependencyGraph<string>();

        graph.AddDependency("a", "b");
        graph.AddDependency("b", "c");

        graph.Clear();

        Assert.Empty(graph.GetDependencies("a"));
        Assert.Empty(graph.GetDependencies("b"));
        Assert.Empty(graph.GetDependents("b"));
        Assert.Empty(graph.GetDependents("c"));
        Assert.Equal(0, graph.NodeCount);
    }

    [Fact]
    public void MultipleDependencies_ShouldAllBeStored()
    {
        var graph = new DependencyGraph<int>();

        graph.AddDependency(1, 2);
        graph.AddDependency(1, 3);
        graph.AddDependency(1, 4);

        var dependencies = graph.GetDependencies(1);
        Assert.Equal(3, dependencies.Count);
        Assert.Contains(2, dependencies);
        Assert.Contains(3, dependencies);
        Assert.Contains(4, dependencies);
    }

    [Fact]
    public void HasDependencies_ShouldReturnTrueWhenHasDependencies()
    {
        var graph = new DependencyGraph<string>();

        graph.AddDependency("a", "b");

        Assert.True(graph.HasDependencies("a"));
        Assert.False(graph.HasDependencies("b"));
    }

    [Fact]
    public void HasDependents_ShouldReturnTrueWhenHasDependents()
    {
        var graph = new DependencyGraph<string>();

        graph.AddDependency("a", "b");

        Assert.False(graph.HasDependents("a"));
        Assert.True(graph.HasDependents("b"));
    }

    [Fact]
    public void GetAllNodes_ShouldReturnAllNodes()
    {
        var graph = new DependencyGraph<string>();

        graph.AddDependency("a", "b");
        graph.AddDependency("b", "c");

        var nodes = graph.GetAllNodes().ToList();
        Assert.Equal(3, nodes.Count);
        Assert.Contains("a", nodes);
        Assert.Contains("b", nodes);
        Assert.Contains("c", nodes);
    }

    [Fact]
    public void CustomEqualityComparer_ShouldBeUsed()
    {
        var graph = new DependencyGraph<string>(System.StringComparer.OrdinalIgnoreCase);

        graph.AddDependency("A", "B");
        graph.AddDependency("a", "b");  // 大文字小文字無視なので重複

        Assert.Single(graph.GetDependencies("A"));
        Assert.Single(graph.GetDependencies("a"));
    }
}
