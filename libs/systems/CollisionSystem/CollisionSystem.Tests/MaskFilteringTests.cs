using System;
using Tomato.Math;
using Xunit;

namespace Tomato.CollisionSystem.Tests;

public class MaskFilteringTests
{
    private const uint Layer1 = 0x01;
    private const uint Layer2 = 0x02;
    private const uint Layer3 = 0x04;
    private const uint AllLayers = 0xFFFFFFFF;

    #region LayerMask Accessor Tests

    [Fact]
    public void GetLayerMask_ReturnsDefaultMask()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(0, 0, 0), 1f);

        Assert.Equal(AllLayers, world.GetLayerMask(handle));
    }

    [Fact]
    public void AddSphere_WithCustomLayerMask_SetsCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(0, 0, 0), 1f, layerMask: Layer1);

        Assert.Equal(Layer1, world.GetLayerMask(handle));
    }

    [Fact]
    public void SetLayerMask_UpdatesMask()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(0, 0, 0), 1f, layerMask: Layer1);

        world.SetLayerMask(handle, Layer2);

        Assert.Equal(Layer2, world.GetLayerMask(handle));
    }

    [Fact]
    public void GetLayerMask_InvalidHandle_ReturnsZero()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = new ShapeHandle(-1, 0);

        Assert.Equal(0u, world.GetLayerMask(handle));
    }

    #endregion

    #region Raycast Mask Filtering Tests

    [Fact]
    public void Raycast_IncludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(5, 0, 0), 1f, layerMask: Layer1);
        world.AddSphere(new Vector3(10, 0, 0), 1f, layerMask: Layer2);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f,
            includeMask: Layer2);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
        Assert.True(System.Math.Abs(result.Distance - 9f) < 0.01f);
    }

    [Fact]
    public void Raycast_ExcludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var near = world.AddSphere(new Vector3(5, 0, 0), 1f, layerMask: Layer1);
        var far = world.AddSphere(new Vector3(10, 0, 0), 1f, layerMask: Layer2);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f,
            excludeMask: Layer1);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
        Assert.Equal(far.Index, result.ShapeIndex);
    }

    [Fact]
    public void Raycast_CombinedMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(5, 0, 0), 1f, layerMask: Layer1);
        world.AddSphere(new Vector3(10, 0, 0), 1f, layerMask: Layer2);
        var target = world.AddSphere(new Vector3(15, 0, 0), 1f, layerMask: Layer1 | Layer3);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f,
            includeMask: Layer3, excludeMask: Layer2);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
        Assert.Equal(target.Index, result.ShapeIndex);
    }

    [Fact]
    public void Raycast_NoMatchingMask_ReturnsNoHit()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(5, 0, 0), 1f, layerMask: Layer1);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f,
            includeMask: Layer2);
        bool hit = world.Raycast(query, out _);

        Assert.False(hit);
    }

    [Fact]
    public void RaycastAll_IncludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(5, 0, 0), 1f, layerMask: Layer1);
        world.AddSphere(new Vector3(10, 0, 0), 1f, layerMask: Layer2);
        world.AddSphere(new Vector3(15, 0, 0), 1f, layerMask: Layer1);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f,
            includeMask: Layer1);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.RaycastAll(query, results);

        Assert.Equal(2, count);
    }

    #endregion

    #region SphereOverlap Mask Filtering Tests

    [Fact]
    public void SphereOverlap_IncludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(1, 0, 0), 0.5f, layerMask: Layer1);
        world.AddSphere(new Vector3(-1, 0, 0), 0.5f, layerMask: Layer2);

        var query = new SphereOverlapQuery(new Vector3(0, 0, 0), 2f, includeMask: Layer1);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void SphereOverlap_ExcludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(1, 0, 0), 0.5f, layerMask: Layer1);
        world.AddSphere(new Vector3(-1, 0, 0), 0.5f, layerMask: Layer2);

        var query = new SphereOverlapQuery(new Vector3(0, 0, 0), 2f, excludeMask: Layer1);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(1, count);
    }

    #endregion

    #region CapsuleSweep Mask Filtering Tests

    [Fact]
    public void CapsuleSweep_IncludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(5, 0, 0), 1f, layerMask: Layer1);
        var target = world.AddSphere(new Vector3(10, 0, 0), 1f, layerMask: Layer2);

        var query = new CapsuleSweepQuery(new Vector3(0, 0, 0), new Vector3(20, 0, 0), 0.5f,
            includeMask: Layer2);
        bool hit = world.CapsuleSweep(query, out var result);

        Assert.True(hit);
        Assert.Equal(target.Index, result.ShapeIndex);
    }

    [Fact]
    public void CapsuleSweep_ExcludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(5, 0, 0), 1f, layerMask: Layer1);
        var target = world.AddSphere(new Vector3(10, 0, 0), 1f, layerMask: Layer2);

        var query = new CapsuleSweepQuery(new Vector3(0, 0, 0), new Vector3(20, 0, 0), 0.5f,
            excludeMask: Layer1);
        bool hit = world.CapsuleSweep(query, out var result);

        Assert.True(hit);
        Assert.Equal(target.Index, result.ShapeIndex);
    }

    #endregion

    #region SlashQuery Mask Filtering Tests

    [Fact]
    public void SlashQuery_IncludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 1, 0), 0.5f, layerMask: Layer1);
        world.AddSphere(new Vector3(0, -1, 0), 0.5f, layerMask: Layer2);

        var query = new SlashQuery(
            new Vector3(-1, 0, 0), new Vector3(-1, 2, 0),
            new Vector3(1, 0, 0), new Vector3(1, 2, 0),
            includeMask: Layer1);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void SlashQuery_ExcludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 1, 0), 0.5f, layerMask: Layer1);
        world.AddSphere(new Vector3(0, -1, 0), 0.5f, layerMask: Layer2);

        var query = new SlashQuery(
            new Vector3(-1, -2, 0), new Vector3(-1, 2, 0),
            new Vector3(1, -2, 0), new Vector3(1, 2, 0),
            excludeMask: Layer1);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(1, count);
    }

    #endregion

    #region QueryPoint Mask Filtering Tests

    [Fact]
    public void QueryPoint_IncludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 0, 0), 2f, layerMask: Layer1);
        world.AddSphere(new Vector3(0, 0, 0), 2f, layerMask: Layer2);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(0, 0, 0), results, includeMask: Layer1);

        Assert.Equal(1, count);
    }

    [Fact]
    public void QueryPoint_ExcludeMask_FiltersCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 0, 0), 2f, layerMask: Layer1);
        world.AddSphere(new Vector3(0, 0, 0), 2f, layerMask: Layer2);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(0, 0, 0), results, excludeMask: Layer1);

        Assert.Equal(1, count);
    }

    #endregion

    #region PassesMask Helper Tests

    [Fact]
    public void PassesMask_IncludeMaskMatches_ReturnsTrue()
    {
        var query = new RayQuery(Vector3.Zero, Vector3.UnitX, 1f, includeMask: Layer1 | Layer2);

        Assert.True(query.PassesMask(Layer1));
        Assert.True(query.PassesMask(Layer2));
        Assert.True(query.PassesMask(Layer1 | Layer2));
    }

    [Fact]
    public void PassesMask_IncludeMaskNoMatch_ReturnsFalse()
    {
        var query = new RayQuery(Vector3.Zero, Vector3.UnitX, 1f, includeMask: Layer1);

        Assert.False(query.PassesMask(Layer2));
    }

    [Fact]
    public void PassesMask_ExcludeMaskMatches_ReturnsFalse()
    {
        var query = new RayQuery(Vector3.Zero, Vector3.UnitX, 1f, excludeMask: Layer1);

        Assert.False(query.PassesMask(Layer1));
        Assert.False(query.PassesMask(Layer1 | Layer2));
    }

    [Fact]
    public void PassesMask_ExcludeMaskNoMatch_ReturnsTrue()
    {
        var query = new RayQuery(Vector3.Zero, Vector3.UnitX, 1f, excludeMask: Layer1);

        Assert.True(query.PassesMask(Layer2));
    }

    [Fact]
    public void PassesMask_CombinedMask_WorksCorrectly()
    {
        var query = new RayQuery(Vector3.Zero, Vector3.UnitX, 1f,
            includeMask: Layer1 | Layer2, excludeMask: Layer3);

        Assert.True(query.PassesMask(Layer1));
        Assert.True(query.PassesMask(Layer2));
        Assert.False(query.PassesMask(Layer3));
        Assert.False(query.PassesMask(Layer1 | Layer3));
    }

    #endregion

    #region All Shape Types with Mask

    [Fact]
    public void AddCapsule_WithLayerMask_SetsCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddCapsule(new Vector3(0, 0, 0), new Vector3(0, 1, 0), 0.5f, layerMask: Layer2);

        Assert.Equal(Layer2, world.GetLayerMask(handle));
    }

    [Fact]
    public void AddCylinder_WithLayerMask_SetsCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddCylinder(new Vector3(0, 0, 0), 2f, 1f, layerMask: Layer3);

        Assert.Equal(Layer3, world.GetLayerMask(handle));
    }

    [Fact]
    public void AddBox_WithLayerMask_SetsCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddBox(new Vector3(0, 0, 0), new Vector3(1, 1, 1), layerMask: Layer1 | Layer2);

        Assert.Equal(Layer1 | Layer2, world.GetLayerMask(handle));
    }

    #endregion
}
