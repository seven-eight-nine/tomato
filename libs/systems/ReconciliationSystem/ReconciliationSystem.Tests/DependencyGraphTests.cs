using System;
using System.Linq;
using Xunit;
using Tomato.ReconciliationSystem;
using Tomato.CommandGenerator;
using Tomato.EntityHandleSystem;

namespace Tomato.ReconciliationSystem.Tests;

/// <summary>
/// DependencyGraph テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] DependencyGraphを作成できる
/// - [x] 依存関係を追加できる
/// - [x] 依存先を取得できる
/// - [x] 依存元を取得できる
/// - [x] 依存関係を削除できる
/// - [x] Entityを削除できる（関連する依存関係も削除）
/// - [x] 重複した依存関係は追加されない
/// - [x] Clearでグラフ全体をクリアできる
/// </summary>
public class DependencyGraphTests
{
    private readonly MockArena _arena = new();

    [Fact]
    public void DependencyGraph_ShouldBeCreatable()
    {
        var graph = new DependencyGraph();

        Assert.NotNull(graph);
    }

    [Fact]
    public void AddDependency_ShouldAddDependency()
    {
        var graph = new DependencyGraph();
        var rider = _arena.CreateHandle(1);  // 騎乗者
        var horse = _arena.CreateHandle(2);  // 馬

        // 騎乗者が馬に依存
        graph.AddDependency(rider, horse);

        var dependencies = graph.GetDependencies(rider);
        Assert.Single(dependencies);
        Assert.Equal(horse, dependencies[0]);
    }

    [Fact]
    public void GetDependencies_ShouldReturnEmptyForUnknownEntity()
    {
        var graph = new DependencyGraph();
        var unknown = _arena.CreateHandle(999);

        var dependencies = graph.GetDependencies(unknown);

        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetDependents_ShouldReturnDependents()
    {
        var graph = new DependencyGraph();
        var rider = _arena.CreateHandle(1);
        var horse = _arena.CreateHandle(2);

        graph.AddDependency(rider, horse);

        // 馬の依存元は騎乗者
        var dependents = graph.GetDependents(horse);
        Assert.Single(dependents);
        Assert.Equal(rider, dependents[0]);
    }

    [Fact]
    public void GetDependents_ShouldReturnEmptyForUnknownEntity()
    {
        var graph = new DependencyGraph();
        var unknown = _arena.CreateHandle(999);

        var dependents = graph.GetDependents(unknown);

        Assert.Empty(dependents);
    }

    [Fact]
    public void RemoveDependency_ShouldRemoveDependency()
    {
        var graph = new DependencyGraph();
        var rider = _arena.CreateHandle(1);
        var horse = _arena.CreateHandle(2);

        graph.AddDependency(rider, horse);
        graph.RemoveDependency(rider, horse);

        Assert.Empty(graph.GetDependencies(rider));
        Assert.Empty(graph.GetDependents(horse));
    }

    [Fact]
    public void RemoveEntity_ShouldRemoveAllRelatedDependencies()
    {
        var graph = new DependencyGraph();
        var rider = _arena.CreateHandle(1);
        var horse = _arena.CreateHandle(2);
        var saddle = _arena.CreateHandle(3);  // 鞍（馬に装備）

        // rider -> horse, saddle -> horse
        graph.AddDependency(rider, horse);
        graph.AddDependency(saddle, horse);

        // 馬を削除
        graph.RemoveEntity(horse);

        // 馬への依存が全て削除される
        Assert.Empty(graph.GetDependencies(rider));
        Assert.Empty(graph.GetDependencies(saddle));
        Assert.Empty(graph.GetDependents(horse));
    }

    [Fact]
    public void RemoveEntity_ShouldRemoveDependenciesFromEntity()
    {
        var graph = new DependencyGraph();
        var rider = _arena.CreateHandle(1);
        var horse = _arena.CreateHandle(2);
        var weapon = _arena.CreateHandle(3);

        // rider -> horse, rider -> weapon
        graph.AddDependency(rider, horse);
        graph.AddDependency(rider, weapon);

        // 騎乗者を削除
        graph.RemoveEntity(rider);

        // 騎乗者からの依存が全て削除される
        Assert.Empty(graph.GetDependents(horse));
        Assert.Empty(graph.GetDependents(weapon));
    }

    [Fact]
    public void AddDependency_ShouldNotAddDuplicate()
    {
        var graph = new DependencyGraph();
        var rider = _arena.CreateHandle(1);
        var horse = _arena.CreateHandle(2);

        graph.AddDependency(rider, horse);
        graph.AddDependency(rider, horse);  // 重複

        Assert.Single(graph.GetDependencies(rider));
        Assert.Single(graph.GetDependents(horse));
    }

    [Fact]
    public void Clear_ShouldClearAllDependencies()
    {
        var graph = new DependencyGraph();
        var a = _arena.CreateHandle(1);
        var b = _arena.CreateHandle(2);
        var c = _arena.CreateHandle(3);

        graph.AddDependency(a, b);
        graph.AddDependency(b, c);

        graph.Clear();

        Assert.Empty(graph.GetDependencies(a));
        Assert.Empty(graph.GetDependencies(b));
        Assert.Empty(graph.GetDependents(b));
        Assert.Empty(graph.GetDependents(c));
    }

    [Fact]
    public void MultipleDependencies_ShouldAllBeStored()
    {
        var graph = new DependencyGraph();
        var entity = _arena.CreateHandle(1);
        var dep1 = _arena.CreateHandle(2);
        var dep2 = _arena.CreateHandle(3);
        var dep3 = _arena.CreateHandle(4);

        graph.AddDependency(entity, dep1);
        graph.AddDependency(entity, dep2);
        graph.AddDependency(entity, dep3);

        var dependencies = graph.GetDependencies(entity);
        Assert.Equal(3, dependencies.Count);
        Assert.Contains(dep1, dependencies);
        Assert.Contains(dep2, dependencies);
        Assert.Contains(dep3, dependencies);
    }

    #region Helper Classes

    private class MockArena : IEntityArena
    {
        public VoidHandle CreateHandle(int index) => new VoidHandle(this, index, 0);
        public bool IsValid(int index, int generation) => true;
    }

    #endregion
}
