using System;
using Tomato.Math;
using Xunit;

namespace Tomato.CollisionSystem.Tests;

public class PointQueryTests
{
    private const float Epsilon = 0.001f;

    [Fact]
    public void Point_InsideSphere_ReturnsHit()
    {
        var world = new SpatialWorld();
        var handle = world.AddSphere(new Vector3(0, 0, 0), 2f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(0.5f, 0.5f, 0.5f), results);

        Assert.Equal(1, count);
        Assert.Equal(handle.Index, results[0].ShapeIndex);
    }

    [Fact]
    public void Point_OnSphereSurface_ReturnsHit()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(0, 0, 0), 1f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(1f, 0, 0), results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Point_AtSphereCenter_ReturnsHit()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(5, 5, 5), 1f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(5, 5, 5), results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Point_OutsideSphere_ReturnsNoHit()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(0, 0, 0), 1f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(5, 5, 5), results);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Point_InsideCapsule_ReturnsHit()
    {
        var world = new SpatialWorld();
        world.AddCapsule(new Vector3(0, 0, 0), new Vector3(0, 5, 0), 1f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(0, 2.5f, 0), results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Point_AtCapsuleEndpoint_ReturnsHit()
    {
        var world = new SpatialWorld();
        world.AddCapsule(new Vector3(0, 0, 0), new Vector3(0, 5, 0), 1f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(0, 0, 0), results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Point_InsideCylinder_ReturnsHit()
    {
        var world = new SpatialWorld();
        world.AddCylinder(new Vector3(0, 0, 0), height: 3f, radius: 1f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(0, 1.5f, 0), results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Point_OutsideCylinderTop_ReturnsNoHit()
    {
        var world = new SpatialWorld();
        world.AddCylinder(new Vector3(0, 0, 0), height: 3f, radius: 1f);

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QueryPoint(new Vector3(0, 5f, 0), results);

        Assert.Equal(0, count);
    }
}
