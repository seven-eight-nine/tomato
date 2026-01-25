using System;
using System.Diagnostics;
using Tomato.Math;
using Xunit;
using Xunit.Abstractions;

namespace Tomato.CollisionSystem.Tests;

public class DebugBenchmark
{
    private readonly ITestOutputHelper _output;

    public DebugBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_GridSAP()
    {
        _output.WriteLine("Testing GridSAP...");
        RunSingleStrategy("GridSAP", new GridSAPBroadPhase(8f));
        _output.WriteLine("GridSAP OK");
    }

    [Fact]
    public void Test_SpatialHash()
    {
        _output.WriteLine("Testing SpatialHash...");
        RunSingleStrategy("SpatialHash", new SpatialHashBroadPhase(8f, 1001));
        _output.WriteLine("SpatialHash OK");
    }

    [Fact]
    public void Test_Octree()
    {
        var worldBounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        _output.WriteLine("Testing Octree...");
        RunSingleStrategy("Octree", new OctreeBroadPhase(worldBounds, 8, 1001));
        _output.WriteLine("Octree OK");
    }

    [Fact]
    public void Test_BVH()
    {
        _output.WriteLine("Testing BVH...");
        RunSingleStrategy("BVH", new BVHBroadPhase(1001, true));
        _output.WriteLine("BVH OK");
    }

    [Fact]
    public void Test_DBVT()
    {
        _output.WriteLine("Testing DBVT...");
        RunSingleStrategy("DBVT", new DBVTBroadPhase(1001, 0.1f));
        _output.WriteLine("DBVT OK");
    }

    [Fact]
    public void Test_MBP()
    {
        var worldBounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        _output.WriteLine("Testing MBP...");
        RunSingleStrategy("MBP", new MBPBroadPhase(worldBounds, 8, 8, 1001));
        _output.WriteLine("MBP OK");
    }

    private void RunSingleStrategy(string name, IBroadPhase broadPhase)
    {
        const int shapeCount = 100;
        const int queryCount = 100;

        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);
        var handles = new ShapeHandle[shapeCount];

        _output.WriteLine($"  Adding {shapeCount} shapes...");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < shapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            handles[i] = world.AddSphere(new Vector3(x, y, z), 1f);
        }
        _output.WriteLine($"  Add: {sw.ElapsedMilliseconds}ms");

        _output.WriteLine($"  Running {queryCount} raycasts...");
        sw.Restart();
        for (int i = 0; i < queryCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            float dx = (float)(random.NextDouble() * 2 - 1);
            float dy = (float)(random.NextDouble() * 2 - 1);
            float dz = (float)(random.NextDouble() * 2 - 1);
            var dir = new Vector3(dx, dy, dz).Normalized;
            var query = new RayQuery(new Vector3(x, y, z), dir, 100f);
            world.Raycast(query, out _);
        }
        _output.WriteLine($"  Raycast: {sw.ElapsedMilliseconds}ms");

        _output.WriteLine($"  Running {shapeCount} updates...");
        sw.Restart();
        for (int i = 0; i < shapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            world.UpdateSphere(handles[i], new Vector3(x, y, z), 1f);
        }
        _output.WriteLine($"  Update: {sw.ElapsedMilliseconds}ms");
    }
}
