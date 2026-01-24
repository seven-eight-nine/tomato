using System.Linq;
using Xunit;

namespace Tomato.DependencySortSystem.Tests;

/// <summary>
/// TopologicalSorter テスト
/// </summary>
public class TopologicalSorterTests
{
    [Fact]
    public void Sort_EmptyGraph_ShouldReturnEmpty()
    {
        var graph = new DependencyGraph<int>();
        var sorter = new TopologicalSorter<int>();

        var result = sorter.Sort(System.Array.Empty<int>(), graph);

        Assert.True(result.Success);
        Assert.NotNull(result.SortedOrder);
        Assert.Empty(result.SortedOrder);
    }

    [Fact]
    public void Sort_SingleNode_ShouldReturnSingleNode()
    {
        var graph = new DependencyGraph<string>();
        var sorter = new TopologicalSorter<string>();

        var result = sorter.Sort(new[] { "a" }, graph);

        Assert.True(result.Success);
        Assert.Single(result.SortedOrder!);
        Assert.Equal("a", result.SortedOrder![0]);
    }

    [Fact]
    public void Sort_LinearDependency_ShouldReturnCorrectOrder()
    {
        // a -> b -> c (aはbに依存、bはcに依存)
        var graph = new DependencyGraph<string>();
        graph.AddDependency("a", "b");
        graph.AddDependency("b", "c");

        var sorter = new TopologicalSorter<string>();
        var result = sorter.Sort(new[] { "a", "b", "c" }, graph);

        Assert.True(result.Success);
        Assert.Equal(3, result.SortedOrder!.Count);

        // 依存先が先に来る: c, b, a の順
        var order = result.SortedOrder!.ToList();
        Assert.True(order.IndexOf("c") < order.IndexOf("b"));
        Assert.True(order.IndexOf("b") < order.IndexOf("a"));
    }

    [Fact]
    public void Sort_DiamondDependency_ShouldReturnValidOrder()
    {
        // a -> b, a -> c, b -> d, c -> d
        //     a
        //    / \
        //   b   c
        //    \ /
        //     d
        var graph = new DependencyGraph<string>();
        graph.AddDependency("a", "b");
        graph.AddDependency("a", "c");
        graph.AddDependency("b", "d");
        graph.AddDependency("c", "d");

        var sorter = new TopologicalSorter<string>();
        var result = sorter.Sort(new[] { "a", "b", "c", "d" }, graph);

        Assert.True(result.Success);
        Assert.Equal(4, result.SortedOrder!.Count);

        var order = result.SortedOrder!.ToList();
        // dは最初、aは最後
        Assert.True(order.IndexOf("d") < order.IndexOf("b"));
        Assert.True(order.IndexOf("d") < order.IndexOf("c"));
        Assert.True(order.IndexOf("b") < order.IndexOf("a"));
        Assert.True(order.IndexOf("c") < order.IndexOf("a"));
    }

    [Fact]
    public void Sort_SimpleCycle_ShouldDetectCycle()
    {
        // a -> b -> a (循環)
        var graph = new DependencyGraph<string>();
        graph.AddDependency("a", "b");
        graph.AddDependency("b", "a");

        var sorter = new TopologicalSorter<string>();
        var result = sorter.Sort(new[] { "a", "b" }, graph);

        Assert.False(result.Success);
        Assert.Null(result.SortedOrder);
        Assert.NotNull(result.CyclePath);
        Assert.True(result.CyclePath!.Count >= 2);
    }

    [Fact]
    public void Sort_SelfCycle_ShouldDetectCycle()
    {
        // a -> a (自己循環)
        var graph = new DependencyGraph<string>();
        graph.AddDependency("a", "a");

        var sorter = new TopologicalSorter<string>();
        var result = sorter.Sort(new[] { "a" }, graph);

        Assert.False(result.Success);
        Assert.NotNull(result.CyclePath);
    }

    [Fact]
    public void Sort_LongCycle_ShouldReturnCyclePath()
    {
        // a -> b -> c -> d -> a (長い循環)
        var graph = new DependencyGraph<string>();
        graph.AddDependency("a", "b");
        graph.AddDependency("b", "c");
        graph.AddDependency("c", "d");
        graph.AddDependency("d", "a");

        var sorter = new TopologicalSorter<string>();
        var result = sorter.Sort(new[] { "a", "b", "c", "d" }, graph);

        Assert.False(result.Success);
        Assert.NotNull(result.CyclePath);
        // サイクルパスには循環を構成するノードが含まれる
        Assert.True(result.CyclePath!.Count >= 2);
    }

    [Fact]
    public void Sort_IndependentNodes_ShouldReturnAll()
    {
        var graph = new DependencyGraph<int>();
        var sorter = new TopologicalSorter<int>();

        var result = sorter.Sort(new[] { 1, 2, 3, 4, 5 }, graph);

        Assert.True(result.Success);
        Assert.Equal(5, result.SortedOrder!.Count);
        Assert.Contains(1, result.SortedOrder);
        Assert.Contains(2, result.SortedOrder);
        Assert.Contains(3, result.SortedOrder);
        Assert.Contains(4, result.SortedOrder);
        Assert.Contains(5, result.SortedOrder);
    }

    [Fact]
    public void Sort_PartialNodes_ShouldOnlySortSpecifiedNodes()
    {
        // グラフには a -> b -> c があるが、a, b のみソート
        var graph = new DependencyGraph<string>();
        graph.AddDependency("a", "b");
        graph.AddDependency("b", "c");

        var sorter = new TopologicalSorter<string>();
        var result = sorter.Sort(new[] { "a", "b" }, graph);

        Assert.True(result.Success);
        // cはソート対象に含まれていないが、依存関係は辿られる
        var order = result.SortedOrder!.ToList();
        Assert.True(order.IndexOf("b") < order.IndexOf("a"));
    }

    [Fact]
    public void Sort_WithCustomComparer_ShouldUseComparer()
    {
        var graph = new DependencyGraph<string>(System.StringComparer.OrdinalIgnoreCase);
        graph.AddDependency("A", "B");

        var sorter = new TopologicalSorter<string>(System.StringComparer.OrdinalIgnoreCase);
        var result = sorter.Sort(new[] { "a", "b" }, graph);

        Assert.True(result.Success);
        Assert.Equal(2, result.SortedOrder!.Count);
    }

    [Fact]
    public void Sort_MultipleTimes_ShouldWorkCorrectly()
    {
        var graph = new DependencyGraph<int>();
        graph.AddDependency(1, 2);
        graph.AddDependency(2, 3);

        var sorter = new TopologicalSorter<int>();

        // 1回目
        var result1 = sorter.Sort(new[] { 1, 2, 3 }, graph);
        Assert.True(result1.Success);

        // 2回目（ソーターは再利用可能）
        var result2 = sorter.Sort(new[] { 1, 2, 3 }, graph);
        Assert.True(result2.Success);

        Assert.Equal(result1.SortedOrder!.Count, result2.SortedOrder!.Count);
    }

    [Fact]
    public void Sort_ComplexGraph_ShouldReturnValidOrder()
    {
        // 複雑な依存関係
        //       1
        //      /|\
        //     2 3 4
        //     |/ \|
        //     5   6
        //      \ /
        //       7
        var graph = new DependencyGraph<int>();
        graph.AddDependency(1, 2);
        graph.AddDependency(1, 3);
        graph.AddDependency(1, 4);
        graph.AddDependency(2, 5);
        graph.AddDependency(3, 5);
        graph.AddDependency(3, 6);
        graph.AddDependency(4, 6);
        graph.AddDependency(5, 7);
        graph.AddDependency(6, 7);

        var sorter = new TopologicalSorter<int>();
        var result = sorter.Sort(new[] { 1, 2, 3, 4, 5, 6, 7 }, graph);

        Assert.True(result.Success);
        Assert.Equal(7, result.SortedOrder!.Count);

        var order = result.SortedOrder!.ToList();
        // 7が最初、1が最後
        Assert.Equal(0, order.IndexOf(7));
        Assert.Equal(6, order.IndexOf(1));
    }

    [Fact]
    public void Sort_CycleInSubgraph_ShouldDetectCycle()
    {
        // 1 -> 2 -> 3 -> 4 -> 2 (2-3-4でサイクル)
        var graph = new DependencyGraph<int>();
        graph.AddDependency(1, 2);
        graph.AddDependency(2, 3);
        graph.AddDependency(3, 4);
        graph.AddDependency(4, 2);

        var sorter = new TopologicalSorter<int>();
        var result = sorter.Sort(new[] { 1, 2, 3, 4 }, graph);

        Assert.False(result.Success);
        Assert.NotNull(result.CyclePath);
        // サイクルパスには2, 3, 4が含まれる
        Assert.Contains(2, result.CyclePath!);
    }
}
