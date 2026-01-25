using System;
using System.Diagnostics;
using Tomato.Math;
using Xunit;
using Xunit.Abstractions;

namespace Tomato.CollisionSystem.Tests;

/// <summary>
/// 様々なパターンでBroadPhase戦略を比較するベンチマーク
/// </summary>
public class ComprehensiveBenchmark
{
    private readonly ITestOutputHelper _output;

    public ComprehensiveBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Pattern1_UniformDistribution_SmallObjects()
    {
        // 均一分布、小さいオブジェクト（BVHが得意）
        _output.WriteLine("=== Pattern 1: Uniform Distribution, Small Objects ===");
        _output.WriteLine("1000 shapes (radius=1) in 1000x1000x1000, short rays (maxDist=50)\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        RunBenchmarkSuite(bounds, 1000, 500, shapeRadius: 1f, rayMaxDist: 50f, distribution: Distribution.Uniform);
    }

    [Fact]
    public void Pattern2_UniformDistribution_LargeObjects()
    {
        // 均一分布、大きいオブジェクト（複数セルにまたがる）
        _output.WriteLine("=== Pattern 2: Uniform Distribution, Large Objects ===");
        _output.WriteLine("1000 shapes (radius=20) in 1000x1000x1000, short rays (maxDist=50)\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        RunBenchmarkSuite(bounds, 1000, 500, shapeRadius: 20f, rayMaxDist: 50f, distribution: Distribution.Uniform);
    }

    [Fact]
    public void Pattern3_ClusteredDistribution()
    {
        // クラスター分布（複数の塊に分かれている）
        _output.WriteLine("=== Pattern 3: Clustered Distribution ===");
        _output.WriteLine("1000 shapes in 5 clusters, short rays (maxDist=50)\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        RunBenchmarkSuite(bounds, 1000, 500, shapeRadius: 2f, rayMaxDist: 50f, distribution: Distribution.Clustered);
    }

    [Fact]
    public void Pattern4_DenseSmallArea()
    {
        // 密集エリア（狭い範囲に大量のオブジェクト）
        _output.WriteLine("=== Pattern 4: Dense Small Area ===");
        _output.WriteLine("2000 shapes in 50x50x50 area, short rays (maxDist=30)\n");

        var bounds = new AABB(new Vector3(-25, -25, -25), new Vector3(25, 25, 25));
        RunBenchmarkSuite(bounds, 2000, 500, shapeRadius: 0.5f, rayMaxDist: 30f, distribution: Distribution.Uniform, cellSize: 2f);
    }

    [Fact]
    public void Pattern5_SparseWideArea()
    {
        // 疎な広域（広い範囲に少ないオブジェクト）
        _output.WriteLine("=== Pattern 5: Sparse Wide Area ===");
        _output.WriteLine("500 shapes in 5000x5000x5000 area, long rays (maxDist=500)\n");

        var bounds = new AABB(new Vector3(-2500, -2500, -2500), new Vector3(2500, 2500, 2500));
        RunBenchmarkSuite(bounds, 500, 500, shapeRadius: 10f, rayMaxDist: 500f, distribution: Distribution.Uniform, cellSize: 100f);
    }

    [Fact]
    public void Pattern6_LongRays()
    {
        // 長いレイ（AABBが大きくなる）
        _output.WriteLine("=== Pattern 6: Long Rays ===");
        _output.WriteLine("1000 shapes, very long rays (maxDist=500)\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        RunBenchmarkSuite(bounds, 1000, 500, shapeRadius: 2f, rayMaxDist: 500f, distribution: Distribution.Uniform);
    }

    [Fact]
    public void Pattern7_HighUpdateFrequency()
    {
        // 高頻度更新（動的シーン）
        _output.WriteLine("=== Pattern 7: High Update Frequency ===");
        _output.WriteLine("1000 shapes, 5000 updates, then 500 queries\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        RunUpdateHeavyBenchmark(bounds, 1000, 5000, 500);
    }

    [Fact]
    public void Pattern8_MixedSizes()
    {
        // 様々なサイズのオブジェクトが混在
        _output.WriteLine("=== Pattern 8: Mixed Object Sizes ===");
        _output.WriteLine("1000 shapes with radius 0.5-50, short rays\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        RunMixedSizeBenchmark(bounds, 1000, 500);
    }

    [Fact]
    public void Pattern9_SphereOverlapQueries()
    {
        // 球オーバーラップクエリ（レイキャストとは異なる特性）
        _output.WriteLine("=== Pattern 9: Sphere Overlap Queries ===");
        _output.WriteLine("1000 shapes, sphere overlap queries (radius=20)\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));
        RunSphereOverlapBenchmark(bounds, 1000, 500, queryRadius: 20f);
    }

    [Fact]
    public void Pattern10_ScalabilityTest()
    {
        // スケーラビリティ（オブジェクト数を増やす）
        _output.WriteLine("=== Pattern 10: Scalability Test ===\n");

        var bounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));

        foreach (int count in new[] { 100, 500, 1000, 2000, 5000 })
        {
            _output.WriteLine($"--- {count} shapes ---");
            RunBenchmarkSuite(bounds, count, 200, shapeRadius: 2f, rayMaxDist: 50f, distribution: Distribution.Uniform);
            _output.WriteLine("");
        }
    }

    private void RunBenchmarkSuite(AABB bounds, int shapeCount, int queryCount,
        float shapeRadius, float rayMaxDist, Distribution distribution, float cellSize = 8f)
    {
        _output.WriteLine($"{"Strategy",-12} {"Add(ms)",-8} {"Ray(ms)",-8} {"us/q",-8} {"Hits",-6}");
        _output.WriteLine(new string('-', 45));

        var strategies = CreateStrategies(bounds, shapeCount, cellSize);

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunRaycastBenchmark(broadPhase, bounds, shapeCount, queryCount,
                shapeRadius, rayMaxDist, distribution);
            _output.WriteLine($"{name,-12} {result.AddMs,-8} {result.QueryMs,-8} {result.UsPerQuery,-8:F1} {result.Hits,-6}");
        }
    }

    private void RunUpdateHeavyBenchmark(AABB bounds, int shapeCount, int updateCount, int queryCount)
    {
        _output.WriteLine($"{"Strategy",-12} {"Add(ms)",-8} {"Upd(ms)",-8} {"Ray(ms)",-8}");
        _output.WriteLine(new string('-', 40));

        var strategies = CreateStrategies(bounds, shapeCount, 8f);

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunUpdateBenchmark(broadPhase, bounds, shapeCount, updateCount, queryCount);
            _output.WriteLine($"{name,-12} {result.AddMs,-8} {result.UpdateMs,-8} {result.QueryMs,-8}");
        }
    }

    private void RunMixedSizeBenchmark(AABB bounds, int shapeCount, int queryCount)
    {
        _output.WriteLine($"{"Strategy",-12} {"Add(ms)",-8} {"Ray(ms)",-8} {"us/q",-8} {"Hits",-6}");
        _output.WriteLine(new string('-', 45));

        var strategies = CreateStrategies(bounds, shapeCount, 8f);

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunMixedSizeRaycastBenchmark(broadPhase, bounds, shapeCount, queryCount);
            _output.WriteLine($"{name,-12} {result.AddMs,-8} {result.QueryMs,-8} {result.UsPerQuery,-8:F1} {result.Hits,-6}");
        }
    }

    private void RunSphereOverlapBenchmark(AABB bounds, int shapeCount, int queryCount, float queryRadius)
    {
        _output.WriteLine($"{"Strategy",-12} {"Add(ms)",-8} {"Qry(ms)",-8} {"us/q",-8} {"Hits",-6}");
        _output.WriteLine(new string('-', 45));

        var strategies = CreateStrategies(bounds, shapeCount, 8f);

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunSphereQueryBenchmark(broadPhase, bounds, shapeCount, queryCount, queryRadius);
            _output.WriteLine($"{name,-12} {result.AddMs,-8} {result.QueryMs,-8} {result.UsPerQuery,-8:F1} {result.Hits,-6}");
        }
    }

    private (string Name, IBroadPhase BroadPhase)[] CreateStrategies(AABB bounds, int maxShapes, float cellSize)
    {
        return new (string, IBroadPhase)[]
        {
            ("GridSAP", new GridSAPBroadPhase(cellSize)),
            ("SpatialHash", new SpatialHashBroadPhase(cellSize, maxShapes + 1)),
            ("Octree", new OctreeBroadPhase(bounds, 8, maxShapes + 1)),
            ("BVH", new BVHBroadPhase(maxShapes + 1, true)),
            ("DBVT", new DBVTBroadPhase(maxShapes + 1, 0.1f)),
            ("MBP", new MBPBroadPhase(bounds, 8, 8, maxShapes + 1)),
        };
    }

    private BenchmarkResult RunRaycastBenchmark(IBroadPhase broadPhase, AABB bounds,
        int shapeCount, int queryCount, float shapeRadius, float rayMaxDist, Distribution dist)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);
        var size = bounds.Size;
        var min = bounds.Min;

        var sw = Stopwatch.StartNew();

        if (dist == Distribution.Uniform)
        {
            for (int i = 0; i < shapeCount; i++)
            {
                var pos = new Vector3(
                    min.X + (float)random.NextDouble() * size.X,
                    min.Y + (float)random.NextDouble() * size.Y,
                    min.Z + (float)random.NextDouble() * size.Z);
                world.AddSphere(pos, shapeRadius);
            }
        }
        else // Clustered
        {
            var clusterCenters = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                clusterCenters[i] = new Vector3(
                    min.X + (float)random.NextDouble() * size.X,
                    min.Y + (float)random.NextDouble() * size.Y,
                    min.Z + (float)random.NextDouble() * size.Z);
            }

            for (int i = 0; i < shapeCount; i++)
            {
                var center = clusterCenters[i % 5];
                var offset = new Vector3(
                    (float)(random.NextDouble() - 0.5) * 100,
                    (float)(random.NextDouble() - 0.5) * 100,
                    (float)(random.NextDouble() - 0.5) * 100);
                world.AddSphere(center + offset, shapeRadius);
            }
        }

        long addMs = sw.ElapsedMilliseconds;

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            var query = CreateRandomRay(random, bounds, rayMaxDist);
            world.Raycast(query, out _);
        }

        sw.Restart();
        int hits = 0;
        for (int i = 0; i < queryCount; i++)
        {
            var query = CreateRandomRay(random, bounds, rayMaxDist);
            if (world.Raycast(query, out _))
                hits++;
        }
        long queryMs = sw.ElapsedMilliseconds;

        return new BenchmarkResult
        {
            AddMs = addMs,
            QueryMs = queryMs,
            UsPerQuery = queryMs * 1000.0 / queryCount,
            Hits = hits
        };
    }

    private UpdateResult RunUpdateBenchmark(IBroadPhase broadPhase, AABB bounds,
        int shapeCount, int updateCount, int queryCount)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);
        var handles = new ShapeHandle[shapeCount];
        var size = bounds.Size;
        var min = bounds.Min;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < shapeCount; i++)
        {
            var pos = new Vector3(
                min.X + (float)random.NextDouble() * size.X,
                min.Y + (float)random.NextDouble() * size.Y,
                min.Z + (float)random.NextDouble() * size.Z);
            handles[i] = world.AddSphere(pos, 2f);
        }
        long addMs = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < updateCount; i++)
        {
            int idx = random.Next(shapeCount);
            var pos = new Vector3(
                min.X + (float)random.NextDouble() * size.X,
                min.Y + (float)random.NextDouble() * size.Y,
                min.Z + (float)random.NextDouble() * size.Z);
            world.UpdateSphere(handles[idx], pos, 2f);
        }
        long updateMs = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < queryCount; i++)
        {
            var query = CreateRandomRay(random, bounds, 50f);
            world.Raycast(query, out _);
        }
        long queryMs = sw.ElapsedMilliseconds;

        return new UpdateResult { AddMs = addMs, UpdateMs = updateMs, QueryMs = queryMs };
    }

    private BenchmarkResult RunMixedSizeRaycastBenchmark(IBroadPhase broadPhase, AABB bounds,
        int shapeCount, int queryCount)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);
        var size = bounds.Size;
        var min = bounds.Min;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < shapeCount; i++)
        {
            var pos = new Vector3(
                min.X + (float)random.NextDouble() * size.X,
                min.Y + (float)random.NextDouble() * size.Y,
                min.Z + (float)random.NextDouble() * size.Z);
            // 0.5 ~ 50 のランダムな半径
            float radius = 0.5f + (float)random.NextDouble() * 49.5f;
            world.AddSphere(pos, radius);
        }
        long addMs = sw.ElapsedMilliseconds;

        sw.Restart();
        int hits = 0;
        for (int i = 0; i < queryCount; i++)
        {
            var query = CreateRandomRay(random, bounds, 50f);
            if (world.Raycast(query, out _))
                hits++;
        }
        long queryMs = sw.ElapsedMilliseconds;

        return new BenchmarkResult
        {
            AddMs = addMs,
            QueryMs = queryMs,
            UsPerQuery = queryMs * 1000.0 / queryCount,
            Hits = hits
        };
    }

    private BenchmarkResult RunSphereQueryBenchmark(IBroadPhase broadPhase, AABB bounds,
        int shapeCount, int queryCount, float queryRadius)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);
        var size = bounds.Size;
        var min = bounds.Min;

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < shapeCount; i++)
        {
            var pos = new Vector3(
                min.X + (float)random.NextDouble() * size.X,
                min.Y + (float)random.NextDouble() * size.Y,
                min.Z + (float)random.NextDouble() * size.Z);
            world.AddSphere(pos, 2f);
        }
        long addMs = sw.ElapsedMilliseconds;

        Span<HitResult> buffer = stackalloc HitResult[64];
        sw.Restart();
        int totalHits = 0;
        for (int i = 0; i < queryCount; i++)
        {
            var center = new Vector3(
                min.X + (float)random.NextDouble() * size.X,
                min.Y + (float)random.NextDouble() * size.Y,
                min.Z + (float)random.NextDouble() * size.Z);
            var query = new SphereOverlapQuery(center, queryRadius);
            totalHits += world.QuerySphereOverlap(query, buffer);
        }
        long queryMs = sw.ElapsedMilliseconds;

        return new BenchmarkResult
        {
            AddMs = addMs,
            QueryMs = queryMs,
            UsPerQuery = queryMs * 1000.0 / queryCount,
            Hits = totalHits
        };
    }

    private RayQuery CreateRandomRay(Random random, AABB bounds, float maxDist)
    {
        var size = bounds.Size;
        var min = bounds.Min;

        var origin = new Vector3(
            min.X + (float)random.NextDouble() * size.X,
            min.Y + (float)random.NextDouble() * size.Y,
            min.Z + (float)random.NextDouble() * size.Z);

        var dir = new Vector3(
            (float)(random.NextDouble() * 2 - 1),
            (float)(random.NextDouble() * 2 - 1),
            (float)(random.NextDouble() * 2 - 1)).Normalized;

        return new RayQuery(origin, dir, maxDist);
    }

    private enum Distribution { Uniform, Clustered }

    private struct BenchmarkResult
    {
        public long AddMs;
        public long QueryMs;
        public double UsPerQuery;
        public int Hits;
    }

    private struct UpdateResult
    {
        public long AddMs;
        public long UpdateMs;
        public long QueryMs;
    }
}
