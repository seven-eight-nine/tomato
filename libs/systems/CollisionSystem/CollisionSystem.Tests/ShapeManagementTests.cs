using System;
using Tomato.Math;
using Xunit;

namespace Tomato.CollisionSystem.Tests;

public class ShapeManagementTests
{
    [Fact]
    public void AddSphere_ReturnsValidHandle()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(0, 0, 0), 1f);

        Assert.True(handle.IsValid);
        Assert.True(world.IsValid(handle));
    }

    [Fact]
    public void Remove_InvalidatesHandle()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(0, 0, 0), 1f);

        bool removed = world.Remove(handle);

        Assert.True(removed);
        Assert.False(world.IsValid(handle));
    }

    [Fact]
    public void Remove_ShapeNoLongerHit()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(5, 0, 0), 1f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        Assert.True(world.Raycast(query, out _));

        world.Remove(handle);

        Assert.False(world.Raycast(query, out _));
    }

    [Fact]
    public void Update_MovesShape()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(5, 0, 0), 1f);

        // 元の位置でヒット
        var query1 = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 10f);
        Assert.True(world.Raycast(query1, out _));

        // 移動（レイの範囲外へ）
        world.UpdateSphere(handle, new Vector3(50, 50, 50), 1f);

        // 元の位置でミス（球は別の場所に移動したので）
        Assert.False(world.Raycast(query1, out _));

        // 新しい位置でヒット
        var query2 = new RayQuery(new Vector3(45, 50, 50), new Vector3(1, 0, 0), 100f);
        Assert.True(world.Raycast(query2, out _));
    }

    [Fact]
    public void GetUserData_ReturnsStoredValue()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var handle = world.AddSphere(new Vector3(0, 0, 0), 1f, userData: 42);

        Assert.Equal(42, world.GetUserData(handle));
        Assert.Equal(42, world.GetUserData(handle.Index));
    }

    [Fact]
    public void ShapeCount_TracksCorrectly()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));

        Assert.Equal(0, world.ShapeCount);

        var h1 = world.AddSphere(new Vector3(0, 0, 0), 1f);
        Assert.Equal(1, world.ShapeCount);

        var h2 = world.AddCapsule(new Vector3(1, 0, 0), new Vector3(1, 2, 0), 0.5f);
        Assert.Equal(2, world.ShapeCount);

        world.Remove(h1);
        Assert.Equal(1, world.ShapeCount);

        world.Remove(h2);
        Assert.Equal(0, world.ShapeCount);
    }

    [Fact]
    public void MultipleShapeTypes_WorkTogether()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        // 全てy=0に配置し、半径/高さを調整してレイがヒットするようにする
        world.AddSphere(new Vector3(5, 0, 0), 2f);  // 半径2なのでy=0のレイでもヒット
        world.AddCapsule(new Vector3(10, -1, 0), new Vector3(10, 3, 0), 1f);  // y=-1からy=3のカプセル
        world.AddCylinder(new Vector3(15, -1, 0), height: 3f, radius: 1f);  // y=-1からy=2の円柱

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.RaycastAll(query, results);

        Assert.Equal(3, count);
    }
}
