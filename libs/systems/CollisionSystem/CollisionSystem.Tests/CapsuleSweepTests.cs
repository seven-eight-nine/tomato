using System;
using Tomato.Math;
using Xunit;

namespace Tomato.CollisionSystem.Tests;

public class CapsuleSweepTests
{
    private const float Epsilon = 0.01f;

    [Fact]
    public void CapsuleSweep_HitsSphere_ReturnsTOI()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(5, 0, 0), 1f);

        var query = new CapsuleSweepQuery(
            start: new Vector3(0, 0, 0),
            end: new Vector3(10, 0, 0),
            radius: 0.5f);

        bool hit = world.CapsuleSweep(query, out var result);

        Assert.True(hit);
        Assert.True(result.Distance >= 0 && result.Distance <= 1f);
    }

    [Fact]
    public void CapsuleSweep_StartingInContact_ReturnsTOI0()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(0, 0, 0), 2f);

        var query = new CapsuleSweepQuery(
            start: new Vector3(0, 0, 0),
            end: new Vector3(5, 0, 0),
            radius: 0.5f);

        bool hit = world.CapsuleSweep(query, out var result);

        Assert.True(hit);
        Assert.Equal(0f, result.Distance);
    }

    [Fact]
    public void CapsuleSweep_MissesWhenNoObstacle()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(10, 10, 10), 1f);

        var query = new CapsuleSweepQuery(
            start: new Vector3(0, 0, 0),
            end: new Vector3(5, 0, 0),
            radius: 0.5f);

        bool hit = world.CapsuleSweep(query, out var result);

        Assert.False(hit);
    }

    [Fact]
    public void CapsuleSweep_FastMovement_StillHits()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(50, 0, 0), 1f);

        var query = new CapsuleSweepQuery(
            start: new Vector3(0, 0, 0),
            end: new Vector3(100, 0, 0),
            radius: 0.5f);

        bool hit = world.CapsuleSweep(query, out var result);

        Assert.True(hit);
        Assert.True(result.Distance > 0);
    }

    [Fact]
    public void CapsuleSweep_ReturnsEarliestHit()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(10, 0, 0), 1f);
        var near = world.AddSphere(new Vector3(5, 0, 0), 1f);

        var query = new CapsuleSweepQuery(
            start: new Vector3(0, 0, 0),
            end: new Vector3(15, 0, 0),
            radius: 0.5f);

        bool hit = world.CapsuleSweep(query, out var result);

        Assert.True(hit);
        Assert.Equal(near.Index, result.ShapeIndex);
    }

    [Fact]
    public void CapsuleSweep_WithCapsuleTarget()
    {
        var world = new SpatialWorld();
        world.AddCapsule(new Vector3(5, 0, 0), new Vector3(5, 5, 0), 0.5f);

        var query = new CapsuleSweepQuery(
            start: new Vector3(0, 2.5f, 0),
            end: new Vector3(10, 2.5f, 0),
            radius: 0.5f);

        bool hit = world.CapsuleSweep(query, out var result);

        Assert.True(hit);
    }
}
