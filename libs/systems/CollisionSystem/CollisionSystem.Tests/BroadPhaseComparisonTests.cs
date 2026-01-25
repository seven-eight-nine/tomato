using System;
using System.Diagnostics;
using Tomato.Math;
using Xunit;
using Xunit.Abstractions;

namespace Tomato.CollisionSystem.Tests;

public class BroadPhaseComparisonTests
{
    private readonly ITestOutputHelper _output;
    private const int ShapeCount = 5_000;
    private const int QueryIterations = 1_000;

    public BroadPhaseComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CompareAllStrategies_Raycast()
    {
        var worldBounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));

        var strategies = new (string Name, IBroadPhase BroadPhase)[]
        {
            ("GridSAP", new GridSAPBroadPhase(8f)),
            ("SpatialHash", new SpatialHashBroadPhase(8f, ShapeCount + 1)),
            ("Octree", new OctreeBroadPhase(worldBounds, 8, ShapeCount + 1)),
            ("BVH", new BVHBroadPhase(ShapeCount + 1, true)),
            ("DBVT", new DBVTBroadPhase(ShapeCount + 1, 0.1f)),
            ("MBP", new MBPBroadPhase(worldBounds, 8, 8, ShapeCount + 1)),
        };

        _output.WriteLine($"=== Raycast Performance Comparison ({ShapeCount:N0} shapes, {QueryIterations:N0} queries) ===\n");
        _output.WriteLine($"{"Strategy",-15} {"Add(ms)",-10} {"Query(ms)",-12} {"us/query",-12} {"Hits",-10}");
        _output.WriteLine(new string('-', 60));

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunRaycastBenchmark(broadPhase);
            _output.WriteLine($"{name,-15} {result.AddTime,-10} {result.QueryTime,-12} {result.UsPerQuery,-12:F2} {result.HitCount,-10}");
        }
    }

    [Fact]
    public void CompareAllStrategies_SphereOverlap()
    {
        var worldBounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));

        var strategies = new (string Name, IBroadPhase BroadPhase)[]
        {
            ("GridSAP", new GridSAPBroadPhase(8f)),
            ("SpatialHash", new SpatialHashBroadPhase(8f, ShapeCount + 1)),
            ("Octree", new OctreeBroadPhase(worldBounds, 8, ShapeCount + 1)),
            ("BVH", new BVHBroadPhase(ShapeCount + 1, true)),
            ("DBVT", new DBVTBroadPhase(ShapeCount + 1, 0.1f)),
            ("MBP", new MBPBroadPhase(worldBounds, 8, 8, ShapeCount + 1)),
        };

        _output.WriteLine($"=== SphereOverlap Performance Comparison ({ShapeCount:N0} shapes, {QueryIterations:N0} queries) ===\n");
        _output.WriteLine($"{"Strategy",-15} {"Add(ms)",-10} {"Query(ms)",-12} {"us/query",-12} {"TotalHits",-10}");
        _output.WriteLine(new string('-', 60));

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunSphereOverlapBenchmark(broadPhase);
            _output.WriteLine($"{name,-15} {result.AddTime,-10} {result.QueryTime,-12} {result.UsPerQuery,-12:F2} {result.HitCount,-10}");
        }
    }

    [Fact]
    public void CompareAllStrategies_Update()
    {
        var worldBounds = new AABB(new Vector3(-500, -500, -500), new Vector3(500, 500, 500));

        var strategies = new (string Name, IBroadPhase BroadPhase)[]
        {
            ("GridSAP", new GridSAPBroadPhase(8f)),
            ("SpatialHash", new SpatialHashBroadPhase(8f, ShapeCount + 1)),
            ("Octree", new OctreeBroadPhase(worldBounds, 8, ShapeCount + 1)),
            ("BVH", new BVHBroadPhase(ShapeCount + 1, true)),
            ("DBVT", new DBVTBroadPhase(ShapeCount + 1, 0.1f)),
            ("MBP", new MBPBroadPhase(worldBounds, 8, 8, ShapeCount + 1)),
        };

        _output.WriteLine($"=== Update Performance Comparison ({ShapeCount:N0} shapes) ===\n");
        _output.WriteLine($"{"Strategy",-15} {"Add(ms)",-10} {"Update(ms)",-12} {"us/update",-12}");
        _output.WriteLine(new string('-', 50));

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunUpdateBenchmark(broadPhase);
            _output.WriteLine($"{name,-15} {result.AddTime,-10} {result.UpdateTime,-12} {result.UsPerUpdate,-12:F2}");
        }
    }

    [Fact]
    public void CompareAllStrategies_DenseArea()
    {
        // 密集エリアテスト（10x10x10の空間）
        var worldBounds = new AABB(new Vector3(-5, -5, -5), new Vector3(15, 15, 15));
        int denseShapeCount = 2_000;
        int denseQueryCount = 500;

        var strategies = new (string Name, IBroadPhase BroadPhase)[]
        {
            ("GridSAP", new GridSAPBroadPhase(2f)),
            ("SpatialHash", new SpatialHashBroadPhase(2f, denseShapeCount + 1)),
            ("Octree", new OctreeBroadPhase(worldBounds, 10, denseShapeCount + 1)),
            ("BVH", new BVHBroadPhase(denseShapeCount + 1, true)),
            ("DBVT", new DBVTBroadPhase(denseShapeCount + 1, 0.05f)),
            ("MBP", new MBPBroadPhase(worldBounds, 4, 4, denseShapeCount + 1)),
        };

        _output.WriteLine($"=== Dense Area Performance ({denseShapeCount:N0} shapes in 10x10x10, {denseQueryCount:N0} queries) ===\n");
        _output.WriteLine($"{"Strategy",-15} {"Add(ms)",-10} {"Query(ms)",-12} {"us/query",-12} {"Hits",-10}");
        _output.WriteLine(new string('-', 60));

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunDenseAreaBenchmark(broadPhase, denseShapeCount, denseQueryCount);
            _output.WriteLine($"{name,-15} {result.AddTime,-10} {result.QueryTime,-12} {result.UsPerQuery,-12:F2} {result.HitCount,-10}");
        }
    }

    [Fact]
    public void CompareAllStrategies_SparseArea()
    {
        // 疎エリアテスト（10000x10000x10000の空間）
        var worldBounds = new AABB(new Vector3(-5000, -5000, -5000), new Vector3(5000, 5000, 5000));

        var strategies = new (string Name, IBroadPhase BroadPhase)[]
        {
            ("GridSAP", new GridSAPBroadPhase(100f)),
            ("SpatialHash", new SpatialHashBroadPhase(100f, ShapeCount + 1)),
            ("Octree", new OctreeBroadPhase(worldBounds, 8, ShapeCount + 1)),
            ("BVH", new BVHBroadPhase(ShapeCount + 1, true)),
            ("DBVT", new DBVTBroadPhase(ShapeCount + 1, 1f)),
            ("MBP", new MBPBroadPhase(worldBounds, 8, 8, ShapeCount + 1)),
        };

        _output.WriteLine($"=== Sparse Area Performance ({ShapeCount:N0} shapes in 10000^3, {QueryIterations:N0} queries) ===\n");
        _output.WriteLine($"{"Strategy",-15} {"Add(ms)",-10} {"Query(ms)",-12} {"us/query",-12} {"Hits",-10}");
        _output.WriteLine(new string('-', 60));

        foreach (var (name, broadPhase) in strategies)
        {
            var result = RunSparseAreaBenchmark(broadPhase);
            _output.WriteLine($"{name,-15} {result.AddTime,-10} {result.QueryTime,-12} {result.UsPerQuery,-12:F2} {result.HitCount,-10}");
        }
    }

    private BenchmarkResult RunRaycastBenchmark(IBroadPhase broadPhase)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            float radius = (float)(random.NextDouble() * 2 + 0.5);
            world.AddSphere(new Vector3(x, y, z), radius);
        }
        long addTime = sw.ElapsedMilliseconds;

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomRay(random);
            world.Raycast(query, out _);
        }

        sw.Restart();
        int hitCount = 0;
        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random);
            if (world.Raycast(query, out _))
                hitCount++;
        }
        long queryTime = sw.ElapsedMilliseconds;

        return new BenchmarkResult
        {
            AddTime = addTime,
            QueryTime = queryTime,
            UsPerQuery = queryTime * 1000.0 / QueryIterations,
            HitCount = hitCount
        };
    }

    private BenchmarkResult RunSphereOverlapBenchmark(IBroadPhase broadPhase)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            float radius = (float)(random.NextDouble() * 2 + 0.5);
            world.AddSphere(new Vector3(x, y, z), radius);
        }
        long addTime = sw.ElapsedMilliseconds;

        Span<HitResult> buffer = stackalloc HitResult[64];

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomSphereOverlap(random);
            world.QuerySphereOverlap(query, buffer);
        }

        sw.Restart();
        int totalHits = 0;
        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomSphereOverlap(random);
            totalHits += world.QuerySphereOverlap(query, buffer);
        }
        long queryTime = sw.ElapsedMilliseconds;

        return new BenchmarkResult
        {
            AddTime = addTime,
            QueryTime = queryTime,
            UsPerQuery = queryTime * 1000.0 / QueryIterations,
            HitCount = totalHits
        };
    }

    private UpdateBenchmarkResult RunUpdateBenchmark(IBroadPhase broadPhase)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);
        var handles = new ShapeHandle[ShapeCount];

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            handles[i] = world.AddSphere(new Vector3(x, y, z), 1f);
        }
        long addTime = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            world.UpdateSphere(handles[i], new Vector3(x, y, z), 1f);
        }
        long updateTime = sw.ElapsedMilliseconds;

        return new UpdateBenchmarkResult
        {
            AddTime = addTime,
            UpdateTime = updateTime,
            UsPerUpdate = updateTime * 1000.0 / ShapeCount
        };
    }

    private BenchmarkResult RunDenseAreaBenchmark(IBroadPhase broadPhase, int shapeCount, int queryCount)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < shapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 10);
            float y = (float)(random.NextDouble() * 10);
            float z = (float)(random.NextDouble() * 10);
            world.AddSphere(new Vector3(x, y, z), 0.1f);
        }
        long addTime = sw.ElapsedMilliseconds;

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            float y = (float)(random.NextDouble() * 10);
            float z = (float)(random.NextDouble() * 10);
            var query = new RayQuery(new Vector3(-5, y, z), new Vector3(1, 0, 0), 20f);
            world.Raycast(query, out _);
        }

        sw.Restart();
        int hitCount = 0;
        for (int i = 0; i < queryCount; i++)
        {
            float y = (float)(random.NextDouble() * 10);
            float z = (float)(random.NextDouble() * 10);
            var query = new RayQuery(new Vector3(-5, y, z), new Vector3(1, 0, 0), 20f);
            if (world.Raycast(query, out _))
                hitCount++;
        }
        long queryTime = sw.ElapsedMilliseconds;

        return new BenchmarkResult
        {
            AddTime = addTime,
            QueryTime = queryTime,
            UsPerQuery = queryTime * 1000.0 / queryCount,
            HitCount = hitCount
        };
    }

    private BenchmarkResult RunSparseAreaBenchmark(IBroadPhase broadPhase)
    {
        var world = new SpatialWorld(broadPhase);
        var random = new Random(42);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 10000 - 5000);
            float y = (float)(random.NextDouble() * 10000 - 5000);
            float z = (float)(random.NextDouble() * 10000 - 5000);
            world.AddSphere(new Vector3(x, y, z), 5f);
        }
        long addTime = sw.ElapsedMilliseconds;

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomRay(random, 5000f, 1000f);
            world.Raycast(query, out _);
        }

        sw.Restart();
        int hitCount = 0;
        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random, 5000f, 1000f);
            if (world.Raycast(query, out _))
                hitCount++;
        }
        long queryTime = sw.ElapsedMilliseconds;

        return new BenchmarkResult
        {
            AddTime = addTime,
            QueryTime = queryTime,
            UsPerQuery = queryTime * 1000.0 / QueryIterations,
            HitCount = hitCount
        };
    }

    private RayQuery CreateRandomRay(Random random, float worldSize = 500f, float maxDist = 100f)
    {
        float x = (float)(random.NextDouble() * worldSize * 2 - worldSize);
        float y = (float)(random.NextDouble() * worldSize * 2 - worldSize);
        float z = (float)(random.NextDouble() * worldSize * 2 - worldSize);

        float dx = (float)(random.NextDouble() * 2 - 1);
        float dy = (float)(random.NextDouble() * 2 - 1);
        float dz = (float)(random.NextDouble() * 2 - 1);
        var dir = new Vector3(dx, dy, dz).Normalized;

        return new RayQuery(new Vector3(x, y, z), dir, maxDist);
    }

    private SphereOverlapQuery CreateRandomSphereOverlap(Random random)
    {
        float x = (float)(random.NextDouble() * 1000 - 500);
        float y = (float)(random.NextDouble() * 1000 - 500);
        float z = (float)(random.NextDouble() * 1000 - 500);
        float radius = (float)(random.NextDouble() * 10 + 5);

        return new SphereOverlapQuery(new Vector3(x, y, z), radius);
    }

    private struct BenchmarkResult
    {
        public long AddTime;
        public long QueryTime;
        public double UsPerQuery;
        public int HitCount;
    }

    private struct UpdateBenchmarkResult
    {
        public long AddTime;
        public long UpdateTime;
        public double UsPerUpdate;
    }
}
