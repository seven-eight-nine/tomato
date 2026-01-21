using System;
using System.Collections.Generic;
using Tomato.CollisionSystem;
using Tomato.EntityHandleSystem;
using Xunit;

namespace Tomato.SpatialIndexSystem.Tests;

/// <summary>
/// SpatialHashGrid テスト
/// </summary>
public class SpatialHashGridTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultCellSize_ShouldBe10()
    {
        var grid = new SpatialHashGrid();
        Assert.Equal(10f, grid.CellSize);
    }

    [Fact]
    public void Constructor_CustomCellSize_ShouldUseProvided()
    {
        var grid = new SpatialHashGrid(5f);
        Assert.Equal(5f, grid.CellSize);
    }

    [Fact]
    public void Constructor_ZeroCellSize_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new SpatialHashGrid(0f));
    }

    [Fact]
    public void Constructor_NegativeCellSize_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new SpatialHashGrid(-1f));
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_NewEntity_ShouldAddToGrid()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(0, 0, 0));

        Assert.Equal(1, grid.Count);
    }

    [Fact]
    public void Update_SamePosition_ShouldNotDuplicate()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(0, 0, 0));
        grid.Update(handle, new Vector3(0, 0, 0));

        Assert.Equal(1, grid.Count);
    }

    [Fact]
    public void Update_MovedWithinSameCell_ShouldUpdatePosition()
    {
        var grid = new SpatialHashGrid(10f);
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(1, 1, 1));
        grid.Update(handle, new Vector3(2, 2, 2)); // Same cell

        Assert.Equal(1, grid.Count);
        Assert.Equal(1, grid.CellCount);
    }

    [Fact]
    public void Update_MovedToDifferentCell_ShouldUpdateCell()
    {
        var grid = new SpatialHashGrid(10f);
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(0, 0, 0));
        grid.Update(handle, new Vector3(15, 0, 0)); // Different cell

        Assert.Equal(1, grid.Count);
        Assert.Equal(1, grid.CellCount); // Old cell should be removed
    }

    [Fact]
    public void Update_MultipleEntities_ShouldTrackAll()
    {
        var grid = new SpatialHashGrid();

        grid.Update(CreateHandle(1), new Vector3(0, 0, 0));
        grid.Update(CreateHandle(2), new Vector3(10, 0, 0));
        grid.Update(CreateHandle(3), new Vector3(20, 0, 0));

        Assert.Equal(3, grid.Count);
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_ExistingEntity_ShouldRemove()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(0, 0, 0));
        var removed = grid.Remove(handle);

        Assert.True(removed);
        Assert.Equal(0, grid.Count);
    }

    [Fact]
    public void Remove_NonExistingEntity_ShouldReturnFalse()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        var removed = grid.Remove(handle);

        Assert.False(removed);
    }

    [Fact]
    public void Remove_LastInCell_ShouldRemoveCell()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(0, 0, 0));
        grid.Remove(handle);

        Assert.Equal(0, grid.CellCount);
    }

    #endregion

    #region QuerySphere Tests

    [Fact]
    public void QuerySphere_EmptyGrid_ShouldReturnEmpty()
    {
        var grid = new SpatialHashGrid();
        var results = new List<AnyHandle>();

        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        Assert.Empty(results);
    }

    [Fact]
    public void QuerySphere_EntityInRange_ShouldReturn()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(5, 0, 0));
        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        Assert.Single(results);
    }

    [Fact]
    public void QuerySphere_EntityOutOfRange_ShouldNotReturn()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(15, 0, 0));
        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        Assert.Empty(results);
    }

    [Fact]
    public void QuerySphere_EntityExactlyAtBoundary_ShouldReturn()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(10, 0, 0));
        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        Assert.Single(results);
    }

    [Fact]
    public void QuerySphere_MultipleEntities_ShouldReturnAllInRange()
    {
        var grid = new SpatialHashGrid();

        grid.Update(CreateHandle(1), new Vector3(0, 0, 0));
        grid.Update(CreateHandle(2), new Vector3(5, 0, 0));
        grid.Update(CreateHandle(3), new Vector3(100, 0, 0)); // Out of range

        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void QuerySphere_WithEntityRadius_ShouldConsiderRadius()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        // Entity at 15 with radius 6 should be within range 10 from origin (15 - 6 = 9 < 10)
        grid.Update(handle, new Vector3(15, 0, 0), 6f);
        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        Assert.Single(results);
    }

    #endregion

    #region QueryAABB Tests

    [Fact]
    public void QueryAABB_EmptyGrid_ShouldReturnEmpty()
    {
        var grid = new SpatialHashGrid();
        var results = new List<AnyHandle>();
        var bounds = new AABB(new Vector3(-5, -5, -5), new Vector3(5, 5, 5));

        grid.QueryAABB(bounds, results);

        Assert.Empty(results);
    }

    [Fact]
    public void QueryAABB_EntityInside_ShouldReturn()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(0, 0, 0));
        var results = new List<AnyHandle>();
        var bounds = new AABB(new Vector3(-5, -5, -5), new Vector3(5, 5, 5));

        grid.QueryAABB(bounds, results);

        Assert.Single(results);
    }

    [Fact]
    public void QueryAABB_EntityOutside_ShouldNotReturn()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(20, 20, 20));
        var results = new List<AnyHandle>();
        var bounds = new AABB(new Vector3(-5, -5, -5), new Vector3(5, 5, 5));

        grid.QueryAABB(bounds, results);

        Assert.Empty(results);
    }

    [Fact]
    public void QueryAABB_EntityOnBoundary_ShouldReturn()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(5, 5, 5));
        var results = new List<AnyHandle>();
        var bounds = new AABB(new Vector3(-5, -5, -5), new Vector3(5, 5, 5));

        grid.QueryAABB(bounds, results);

        Assert.Single(results);
    }

    [Fact]
    public void QueryAABB_WithEntityRadius_ShouldConsiderRadius()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        // Entity at 8 with radius 4 has bounding box 4-12, which overlaps with bounds 0-5
        grid.Update(handle, new Vector3(8, 0, 0), 4f);
        var results = new List<AnyHandle>();
        var bounds = new AABB(new Vector3(-5, -5, -5), new Vector3(5, 5, 5));

        grid.QueryAABB(bounds, results);

        Assert.Single(results);
    }

    #endregion

    #region QueryNearest Tests

    [Fact]
    public void QueryNearest_EmptyGrid_ShouldReturnFalse()
    {
        var grid = new SpatialHashGrid();

        var found = grid.QueryNearest(new Vector3(0, 0, 0), 100f, out var nearest, out var distance);

        Assert.False(found);
    }

    [Fact]
    public void QueryNearest_SingleEntity_ShouldReturnIt()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        grid.Update(handle, new Vector3(5, 0, 0));
        var found = grid.QueryNearest(new Vector3(0, 0, 0), 100f, out var nearest, out var distance);

        Assert.True(found);
        Assert.Equal(5f, distance, 0.001f);
    }

    [Fact]
    public void QueryNearest_MultipleEntities_ShouldReturnClosest()
    {
        var grid = new SpatialHashGrid();

        grid.Update(CreateHandle(1), new Vector3(10, 0, 0));
        grid.Update(CreateHandle(2), new Vector3(5, 0, 0)); // Closest
        grid.Update(CreateHandle(3), new Vector3(15, 0, 0));

        var found = grid.QueryNearest(new Vector3(0, 0, 0), 100f, out var nearest, out var distance);

        Assert.True(found);
        Assert.Equal(5f, distance, 0.001f);
    }

    [Fact]
    public void QueryNearest_AllOutOfRange_ShouldReturnFalse()
    {
        var grid = new SpatialHashGrid();

        grid.Update(CreateHandle(1), new Vector3(100, 0, 0));
        grid.Update(CreateHandle(2), new Vector3(200, 0, 0));

        var found = grid.QueryNearest(new Vector3(0, 0, 0), 10f, out var nearest, out var distance);

        Assert.False(found);
    }

    [Fact]
    public void QueryNearest_WithEntityRadius_ShouldSubtractRadius()
    {
        var grid = new SpatialHashGrid();
        var handle = CreateHandle(1);

        // Entity at 10 with radius 3, effective distance should be 7
        grid.Update(handle, new Vector3(10, 0, 0), 3f);
        var found = grid.QueryNearest(new Vector3(0, 0, 0), 100f, out var nearest, out var distance);

        Assert.True(found);
        Assert.Equal(7f, distance, 0.001f);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_ShouldRemoveAllEntities()
    {
        var grid = new SpatialHashGrid();

        grid.Update(CreateHandle(1), new Vector3(0, 0, 0));
        grid.Update(CreateHandle(2), new Vector3(10, 0, 0));
        grid.Update(CreateHandle(3), new Vector3(20, 0, 0));

        grid.Clear();

        Assert.Equal(0, grid.Count);
        Assert.Equal(0, grid.CellCount);
    }

    #endregion

    #region Cross-Cell Tests

    [Fact]
    public void QuerySphere_SpanningMultipleCells_ShouldQueryAllCells()
    {
        var grid = new SpatialHashGrid(10f);

        // Entities in different cells
        grid.Update(CreateHandle(1), new Vector3(-15, 0, 0)); // Cell -2
        grid.Update(CreateHandle(2), new Vector3(-5, 0, 0));  // Cell -1
        grid.Update(CreateHandle(3), new Vector3(5, 0, 0));   // Cell 0
        grid.Update(CreateHandle(4), new Vector3(15, 0, 0));  // Cell 1
        grid.Update(CreateHandle(5), new Vector3(25, 0, 0));  // Cell 2

        // Query spanning multiple cells
        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 20f, results);

        // Should get entities at -15, -5, 5, 15
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void Update_MovingAcrossManyCells_ShouldTrackCorrectly()
    {
        var grid = new SpatialHashGrid(10f);
        var handle = CreateHandle(1);

        // Move entity across many cells
        grid.Update(handle, new Vector3(0, 0, 0));
        grid.Update(handle, new Vector3(50, 0, 0));
        grid.Update(handle, new Vector3(-50, 0, 0));
        grid.Update(handle, new Vector3(100, 100, 100));

        Assert.Equal(1, grid.Count);
        Assert.Equal(1, grid.CellCount);

        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(100, 100, 100), 1f, results);
        Assert.Single(results);
    }

    #endregion

    #region 3D Tests

    [Fact]
    public void QuerySphere_3DSpace_ShouldWorkCorrectly()
    {
        var grid = new SpatialHashGrid();

        // Entity in 3D space
        grid.Update(CreateHandle(1), new Vector3(5, 5, 5));

        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        // Distance = sqrt(5^2 + 5^2 + 5^2) = sqrt(75) ≈ 8.66 < 10
        Assert.Single(results);
    }

    [Fact]
    public void QuerySphere_3DSpace_OutOfRange_ShouldNotReturn()
    {
        var grid = new SpatialHashGrid();

        // Entity in 3D space
        grid.Update(CreateHandle(1), new Vector3(7, 7, 7));

        var results = new List<AnyHandle>();
        grid.QuerySphere(new Vector3(0, 0, 0), 10f, results);

        // Distance = sqrt(7^2 + 7^2 + 7^2) = sqrt(147) ≈ 12.12 > 10
        Assert.Empty(results);
    }

    #endregion

    #region Helper Methods

    private static AnyHandle CreateHandle(int id)
    {
        return new AnyHandle(new TestArena(id), id, 1);
    }

    private class TestArena : IEntityArena
    {
        private readonly int _id;

        public TestArena(int id) => _id = id;

        public bool IsValid(int index, int generation) => index == _id && generation == 1;
    }

    #endregion
}
