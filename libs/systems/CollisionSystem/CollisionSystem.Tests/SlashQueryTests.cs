using System;
using Tomato.Math;
using Xunit;

namespace Tomato.CollisionSystem.Tests;

public class SlashQueryTests
{
    [Fact]
    public void Slash_HitsSphere_DirectIntersection()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(0, 0, 0), 1f);

        // 球の中心を通る斬撃
        var query = new SlashQuery(
            startBase: new Vector3(-2, 0, -2),
            startTip: new Vector3(-2, 0, 2),
            endBase: new Vector3(2, 0, -2),
            endTip: new Vector3(2, 0, 2));

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Slash_MissesSphere_Parallel()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(0, 0, 0), 1f);

        // 球の上を通過
        var query = new SlashQuery(
            startBase: new Vector3(-2, 5, -2),
            startTip: new Vector3(-2, 5, 2),
            endBase: new Vector3(2, 5, -2),
            endTip: new Vector3(2, 5, 2));

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Slash_EdgeHit()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(0, 0, 0), 1f);

        // エッジがかする
        var query = new SlashQuery(
            startBase: new Vector3(-2, 0.9f, 0),
            startTip: new Vector3(-2, 0.9f, 2),
            endBase: new Vector3(2, 0.9f, 0),
            endTip: new Vector3(2, 0.9f, 2));

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Slash_HitsCapsule()
    {
        var world = new SpatialWorld();
        world.AddCapsule(new Vector3(0, 0, 0), new Vector3(0, 3, 0), 0.5f);

        var query = new SlashQuery(
            startBase: new Vector3(-2, 1.5f, 0),
            startTip: new Vector3(-2, 1.5f, 2),
            endBase: new Vector3(2, 1.5f, 0),
            endTip: new Vector3(2, 1.5f, 2));

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Slash_HitsCylinder()
    {
        var world = new SpatialWorld();
        world.AddCylinder(new Vector3(0, 0, 0), height: 2f, radius: 0.5f);

        var query = new SlashQuery(
            startBase: new Vector3(-2, 1f, 0),
            startTip: new Vector3(-2, 1f, 2),
            endBase: new Vector3(2, 1f, 0),
            endTip: new Vector3(2, 1f, 2));

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Slash_MultipleTargets()
    {
        var world = new SpatialWorld();
        world.AddSphere(new Vector3(-1, 0, 0), 0.5f);
        world.AddSphere(new Vector3(1, 0, 0), 0.5f);
        world.AddSphere(new Vector3(0, 10, 0), 0.5f); // 離れてる

        var query = new SlashQuery(
            startBase: new Vector3(-3, 0, -1),
            startTip: new Vector3(-3, 0, 1),
            endBase: new Vector3(3, 0, -1),
            endTip: new Vector3(3, 0, 1));

        Span<HitResult> results = stackalloc HitResult[8];
        int count = world.QuerySlash(query, results);

        Assert.Equal(2, count);
    }
}
