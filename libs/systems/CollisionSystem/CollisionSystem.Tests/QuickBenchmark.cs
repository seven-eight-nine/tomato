using System;
using System.Diagnostics;
using Tomato.Math;
using Xunit;
using Xunit.Abstractions;

namespace Tomato.CollisionSystem.Tests;

public class QuickBenchmark
{
    private readonly ITestOutputHelper _output;

    public QuickBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void QuickCompareAll()
    {
        const int shapeCount = 1000;
        const int queryCount = 500;
        var worldBounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));

        _output.WriteLine($"=== Quick BroadPhase Benchmark ({shapeCount} shapes, {queryCount} queries) ===\n");
        _output.WriteLine($"{"Strategy",-15} {"Add(ms)",-8} {"Ray(ms)",-8} {"Sphere(ms)",-10} {"Update(ms)",-10}");
        _output.WriteLine(new string('-', 55));

        RunBenchmark("GridSAP", new GridSAPBroadPhase(8f), shapeCount, queryCount);
        RunBenchmark("SpatialHash", new SpatialHashBroadPhase(8f, shapeCount + 1), shapeCount, queryCount);
        RunBenchmark("Octree", new OctreeBroadPhase(worldBounds, 8, shapeCount + 1), shapeCount, queryCount);
        RunBenchmark("BVH", new BVHBroadPhase(shapeCount + 1, true), shapeCount, queryCount);
        RunBenchmark("DBVT", new DBVTBroadPhase(shapeCount + 1, 0.1f), shapeCount, queryCount);
        RunBenchmark("MBP", new MBPBroadPhase(worldBounds, 8, 8, shapeCount + 1), shapeCount, queryCount);
    }

    private void RunBenchmark(string name, IBroadPhase broadPhase, int shapeCount, int queryCount)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);
        var handles = new ShapeHandle[shapeCount];

        // Add shapes
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < shapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            handles[i] = world.AddSphere(new Vector3(x, y, z), 1f);
        }
        long addMs = sw.ElapsedMilliseconds;

        // Raycast
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
        long rayMs = sw.ElapsedMilliseconds;

        // SphereOverlap
        Span<HitResult> buffer = stackalloc HitResult[32];
        sw.Restart();
        for (int i = 0; i < queryCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            var query = new SphereOverlapQuery(new Vector3(x, y, z), 10f);
            world.QuerySphereOverlap(query, buffer);
        }
        long sphereMs = sw.ElapsedMilliseconds;

        // Update
        sw.Restart();
        for (int i = 0; i < shapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            world.UpdateSphere(handles[i], new Vector3(x, y, z), 1f);
        }
        long updateMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"{name,-15} {addMs,-8} {rayMs,-8} {sphereMs,-10} {updateMs,-10}");
    }
}
