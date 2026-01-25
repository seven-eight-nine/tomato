using System;
using Tomato.Math;
using Xunit;

namespace Tomato.CollisionSystem.Tests;

public class SphereOverlapTests
{
    [Fact]
    public void SphereOverlap_CompletelyInside_ReturnsHit()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 0, 0), 5f);

        var query = new SphereOverlapQuery(new Vector3(0, 0, 0), 1f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void SphereOverlap_Touching_ReturnsHit()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 0, 0), 1f);

        var query = new SphereOverlapQuery(new Vector3(2f, 0, 0), 1f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void SphereOverlap_NotTouching_ReturnsNoHit()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 0, 0), 1f);

        var query = new SphereOverlapQuery(new Vector3(5f, 0, 0), 1f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(0, count);
    }

    [Fact]
    public void SphereOverlap_MultipleSpheres()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddSphere(new Vector3(0, 0, 0), 1f);
        world.AddSphere(new Vector3(1, 0, 0), 1f);
        world.AddSphere(new Vector3(10, 0, 0), 1f);

        var query = new SphereOverlapQuery(new Vector3(0.5f, 0, 0), 2f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(2, count);
    }

    [Fact]
    public void SphereOverlap_WithCapsule()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddCapsule(new Vector3(0, 0, 0), new Vector3(0, 5, 0), 0.5f);

        var query = new SphereOverlapQuery(new Vector3(1f, 2.5f, 0), 1f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void SphereOverlap_WithCylinder()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        world.AddCylinder(new Vector3(0, 0, 0), height: 3f, radius: 1f);

        var query = new SphereOverlapQuery(new Vector3(1.5f, 1.5f, 0), 1f);
        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySphereOverlap(query, results);

        Assert.Equal(1, count);
    }
}
