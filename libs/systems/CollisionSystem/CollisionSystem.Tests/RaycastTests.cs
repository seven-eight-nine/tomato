using System;
using Tomato.Math;
using Xunit;

namespace Tomato.CollisionSystem.Tests;

public class RaycastTests
{
    private const float Epsilon = 0.01f;

    [Fact]
    public void Raycast_HitsSphere_ReturnsCorrectDistance()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(5, 0, 0), 1f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
        Assert.True(MathF.Abs(result.Distance - 4f) < Epsilon);
    }

    [Fact]
    public void Raycast_MissesSphere_ReturnsNoHit()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(5, 5, 0), 1f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        bool hit = world.Raycast(query, out var result);

        Assert.False(hit);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Raycast_FromInsideSphere_ReturnsImmediateHit()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(0, 0, 0), 5f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
        Assert.Equal(0f, result.Distance);
    }

    [Fact]
    public void Raycast_ReturnsClosestHit()
    {
        var world = new SpatialWorld();
        var far = world.AddSphere(new Vector3(10, 0, 0), 1f);
        var near = world.AddSphere(new Vector3(5, 0, 0), 1f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
        Assert.Equal(near.Index, result.ShapeIndex);
    }

    [Fact]
    public void Raycast_TangentHit_Succeeds()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(5, 1, 0), 1f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
    }

    [Fact]
    public void Raycast_HitsCapsule()
    {
        var world = new SpatialWorld();
        world.AddCapsule(new Vector3(5, 0, 0), new Vector3(5, 5, 0), 1f);

        var query = new RayQuery(new Vector3(0, 2.5f, 0), new Vector3(1, 0, 0), 100f);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
        Assert.True(MathF.Abs(result.Distance - 4f) < Epsilon);
    }

    [Fact]
    public void Raycast_HitsCylinder()
    {
        var world = new SpatialWorld();
        world.AddCylinder(new Vector3(5, 0, 0), height: 3f, radius: 1f);

        var query = new RayQuery(new Vector3(0, 1.5f, 0), new Vector3(1, 0, 0), 100f);
        bool hit = world.Raycast(query, out var result);

        Assert.True(hit);
    }

    [Fact]
    public void RaycastAll_ReturnsSortedByDistance()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(15, 0, 0), 1f);
        world.AddSphere(new Vector3(5, 0, 0), 1f);
        world.AddSphere(new Vector3(10, 0, 0), 1f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 100f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.RaycastAll(query, results);

        Assert.Equal(3, count);
        Assert.True(results[0].Distance < results[1].Distance);
        Assert.True(results[1].Distance < results[2].Distance);
    }

    [Fact]
    public void Raycast_MaxDistanceRespected()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(10, 0, 0), 1f);

        var query = new RayQuery(new Vector3(0, 0, 0), new Vector3(1, 0, 0), 5f);
        bool hit = world.Raycast(query, out _);

        Assert.False(hit);
    }
}
