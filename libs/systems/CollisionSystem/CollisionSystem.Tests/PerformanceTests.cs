using System;
using System.Diagnostics;
using Tomato.Math;
using Xunit;
using Xunit.Abstractions;

namespace Tomato.CollisionSystem.Tests;

public class PerformanceTests
{
    private readonly ITestOutputHelper _output;
    private const int ShapeCount = 100_000;
    private const int QueryIterations = 10_000;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LargeScale_AddShapes_Performance()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
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

        sw.Stop();

        _output.WriteLine($"Added {ShapeCount:N0} spheres in {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / ShapeCount:F2} us per shape");

        Assert.Equal(ShapeCount, world.ShapeCount);
    }

    [Fact]
    public void LargeScale_Raycast_Performance()
    {
        var world = CreateLargeWorld(ShapeCount);
        var random = new Random(123);

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomRay(random);
            world.Raycast(query, out _);
        }

        var sw = Stopwatch.StartNew();
        int hitCount = 0;

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random);
            if (world.Raycast(query, out _))
                hitCount++;
        }

        sw.Stop();

        _output.WriteLine($"Raycast {QueryIterations:N0} queries against {ShapeCount:N0} shapes");
        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Hit rate: {hitCount * 100.0 / QueryIterations:F1}%");

        Assert.True(sw.ElapsedMilliseconds < 10000, "Raycast performance too slow");
    }

    [Fact]
    public void LargeScale_RaycastAll_Performance()
    {
        var world = CreateLargeWorld(ShapeCount);
        var random = new Random(456);

        // ウォームアップ
        Span<HitResult> warmupBuffer = stackalloc HitResult[64];
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomRay(random);
            world.RaycastAll(query, warmupBuffer);
        }

        var sw = Stopwatch.StartNew();
        int totalHits = 0;
        Span<HitResult> buffer = stackalloc HitResult[64];

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random);
            totalHits += world.RaycastAll(query, buffer);
        }

        sw.Stop();

        _output.WriteLine($"RaycastAll {QueryIterations:N0} queries against {ShapeCount:N0} shapes");
        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Total hits: {totalHits:N0}, Average hits per query: {(float)totalHits / QueryIterations:F2}");

        Assert.True(sw.ElapsedMilliseconds < 15000, "RaycastAll performance too slow");
    }

    [Fact]
    public void LargeScale_SphereOverlap_Performance()
    {
        var world = CreateLargeWorld(ShapeCount);
        var random = new Random(789);

        // ウォームアップ
        Span<HitResult> warmupBuffer = stackalloc HitResult[64];
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomSphereOverlap(random);
            world.QuerySphereOverlap(query, warmupBuffer);
        }

        var sw = Stopwatch.StartNew();
        int totalHits = 0;
        Span<HitResult> buffer = stackalloc HitResult[64];

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomSphereOverlap(random);
            totalHits += world.QuerySphereOverlap(query, buffer);
        }

        sw.Stop();

        _output.WriteLine($"SphereOverlap {QueryIterations:N0} queries against {ShapeCount:N0} shapes");
        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Total hits: {totalHits:N0}, Average hits per query: {(float)totalHits / QueryIterations:F2}");

        Assert.True(sw.ElapsedMilliseconds < 15000, "SphereOverlap performance too slow");
    }

    [Fact]
    public void LargeScale_CapsuleSweep_Performance()
    {
        var world = CreateLargeWorld(ShapeCount);
        var random = new Random(101112);

        // ウォームアップ
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomCapsuleSweep(random);
            world.CapsuleSweep(query, out _);
        }

        var sw = Stopwatch.StartNew();
        int hitCount = 0;

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomCapsuleSweep(random);
            if (world.CapsuleSweep(query, out _))
                hitCount++;
        }

        sw.Stop();

        _output.WriteLine($"CapsuleSweep {QueryIterations:N0} queries against {ShapeCount:N0} shapes");
        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Hit rate: {hitCount * 100.0 / QueryIterations:F1}%");

        Assert.True(sw.ElapsedMilliseconds < 15000, "CapsuleSweep performance too slow");
    }

    [Fact]
    public void LargeScale_SlashQuery_Performance()
    {
        var world = CreateLargeWorld(ShapeCount);
        var random = new Random(131415);

        // ウォームアップ
        Span<HitResult> warmupBuffer = stackalloc HitResult[64];
        for (int i = 0; i < 100; i++)
        {
            var query = CreateRandomSlash(random);
            world.QuerySlash(query, warmupBuffer);
        }

        var sw = Stopwatch.StartNew();
        int totalHits = 0;
        Span<HitResult> buffer = stackalloc HitResult[64];

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomSlash(random);
            totalHits += world.QuerySlash(query, buffer);
        }

        sw.Stop();

        _output.WriteLine($"SlashQuery {QueryIterations:N0} queries against {ShapeCount:N0} shapes");
        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Total hits: {totalHits:N0}, Average hits per query: {(float)totalHits / QueryIterations:F2}");

        Assert.True(sw.ElapsedMilliseconds < 15000, "SlashQuery performance too slow");
    }

    [Fact]
    public void LargeScale_MixedShapes_Performance()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var random = new Random(161718);

        var sw = Stopwatch.StartNew();

        // 1/3ずつ異なる形状を追加
        int sphereCount = ShapeCount / 3;
        int capsuleCount = ShapeCount / 3;
        int cylinderCount = ShapeCount - sphereCount - capsuleCount;

        for (int i = 0; i < sphereCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            float radius = (float)(random.NextDouble() * 2 + 0.5);
            world.AddSphere(new Vector3(x, y, z), radius);
        }

        for (int i = 0; i < capsuleCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            float height = (float)(random.NextDouble() * 3 + 1);
            float radius = (float)(random.NextDouble() * 1 + 0.3);
            world.AddCapsule(new Vector3(x, y, z), new Vector3(x, y + height, z), radius);
        }

        for (int i = 0; i < cylinderCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            float height = (float)(random.NextDouble() * 3 + 1);
            float radius = (float)(random.NextDouble() * 1 + 0.3);
            world.AddCylinder(new Vector3(x, y, z), height, radius);
        }

        sw.Stop();
        _output.WriteLine($"Added {ShapeCount:N0} mixed shapes in {sw.ElapsedMilliseconds} ms");

        // クエリテスト
        sw.Restart();
        int hitCount = 0;
        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random);
            if (world.Raycast(query, out _))
                hitCount++;
        }
        sw.Stop();

        _output.WriteLine($"Raycast {QueryIterations:N0} queries: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Hit rate: {hitCount * 100.0 / QueryIterations:F1}%");

        Assert.Equal(ShapeCount, world.ShapeCount);
    }

    [Fact]
    public void LargeScale_UpdatePositions_Performance()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var random = new Random(192021);
        var handles = new ShapeHandle[ShapeCount];

        // 形状を追加
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            handles[i] = world.AddSphere(new Vector3(x, y, z), 1f);
        }

        // 位置更新テスト
        var sw = Stopwatch.StartNew();
        int updateCount = ShapeCount; // 全形状を1回更新

        for (int i = 0; i < updateCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            world.UpdateSphere(handles[i], new Vector3(x, y, z), 1f);
        }

        sw.Stop();

        _output.WriteLine($"Updated {updateCount:N0} shape positions in {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / updateCount:F2} us per update");

        Assert.True(sw.ElapsedMilliseconds < 5000, "Update performance too slow");
    }

    [Fact]
    public void LargeScale_RemoveShapes_Performance()
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var random = new Random(222324);
        var handles = new ShapeHandle[ShapeCount];

        // 形状を追加
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            handles[i] = world.AddSphere(new Vector3(x, y, z), 1f);
        }

        // 削除テスト（半分を削除）
        var sw = Stopwatch.StartNew();
        int removeCount = ShapeCount / 2;

        for (int i = 0; i < removeCount; i++)
        {
            world.Remove(handles[i * 2]); // 偶数インデックスを削除
        }

        sw.Stop();

        _output.WriteLine($"Removed {removeCount:N0} shapes in {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / removeCount:F2} us per removal");

        Assert.Equal(ShapeCount - removeCount, world.ShapeCount);
    }

    [Fact]
    public void LargeScale_DenseArea_Raycast_Performance()
    {
        // 密集エリアでのレイキャストテスト
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var random = new Random(252627);

        // 小さなエリアに密集配置（10x10x10の空間に10万個）
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 10);
            float y = (float)(random.NextDouble() * 10);
            float z = (float)(random.NextDouble() * 10);
            world.AddSphere(new Vector3(x, y, z), 0.1f);
        }

        _output.WriteLine($"Created dense world with {ShapeCount:N0} spheres in 10x10x10 area");

        // 密集エリアを貫通するレイ
        var sw = Stopwatch.StartNew();
        int hitCount = 0;
        int iterations = 1000; // 密集エリアは遅いので少なめ

        for (int i = 0; i < iterations; i++)
        {
            float y = (float)(random.NextDouble() * 10);
            float z = (float)(random.NextDouble() * 10);
            var query = new RayQuery(new Vector3(-5, y, z), new Vector3(1, 0, 0), 20f);
            if (world.Raycast(query, out _))
                hitCount++;
        }

        sw.Stop();

        _output.WriteLine($"Dense area raycast {iterations:N0} queries: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / iterations:F2} us per query");
        _output.WriteLine($"Hit rate: {hitCount * 100.0 / iterations:F1}%");
    }

    [Fact]
    public void LargeScale_SparseArea_Raycast_Performance()
    {
        // 広大なエリアでのレイキャストテスト（大きなグリッドサイズで最適化）
        var world = new SpatialWorld(new GridSAPBroadPhase(156f)); // 10000/64 ≈ 156
        var random = new Random(282930);

        // 大きなエリアに分散配置（10000x10000x10000の空間に10万個）
        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 10000 - 5000);
            float y = (float)(random.NextDouble() * 10000 - 5000);
            float z = (float)(random.NextDouble() * 10000 - 5000);
            world.AddSphere(new Vector3(x, y, z), 5f);
        }

        _output.WriteLine($"Created sparse world with {ShapeCount:N0} spheres in 10000x10000x10000 area");

        var sw = Stopwatch.StartNew();
        int hitCount = 0;

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random, 5000f, 1000f);
            if (world.Raycast(query, out _))
                hitCount++;
        }

        sw.Stop();

        _output.WriteLine($"Sparse area raycast {QueryIterations:N0} queries: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Hit rate: {hitCount * 100.0 / QueryIterations:F1}%");
    }

    [Fact]
    public void LargeScale_SparseArea_SmallGrid_Performance()
    {
        // 比較用: 小さいグリッドサイズでの疎エリアテスト
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var random = new Random(282930);

        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 10000 - 5000);
            float y = (float)(random.NextDouble() * 10000 - 5000);
            float z = (float)(random.NextDouble() * 10000 - 5000);
            world.AddSphere(new Vector3(x, y, z), 5f);
        }

        _output.WriteLine($"[Small grid] Testing with gridSize=8m");

        var sw = Stopwatch.StartNew();
        int hitCount = 0;

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random, 5000f, 1000f);
            if (world.Raycast(query, out _))
                hitCount++;
        }

        sw.Stop();

        _output.WriteLine($"[Small grid] Raycast {QueryIterations:N0} queries: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Hit rate: {hitCount * 100.0 / QueryIterations:F1}%");
    }

    [Fact]
    public void LargeScale_SparseArea_LargeGrid_Performance()
    {
        // 大きなグリッドサイズでの疎エリアテスト（10000m / 64 ≈ 156m）
        var world = new SpatialWorld(new GridSAPBroadPhase(156f));
        var random = new Random(282930);

        for (int i = 0; i < ShapeCount; i++)
        {
            float x = (float)(random.NextDouble() * 10000 - 5000);
            float y = (float)(random.NextDouble() * 10000 - 5000);
            float z = (float)(random.NextDouble() * 10000 - 5000);
            world.AddSphere(new Vector3(x, y, z), 5f);
        }

        _output.WriteLine($"[Large grid] Testing with gridSize=156m");

        var sw = Stopwatch.StartNew();
        int hitCount = 0;

        for (int i = 0; i < QueryIterations; i++)
        {
            var query = CreateRandomRay(random, 5000f, 1000f);
            if (world.Raycast(query, out _))
                hitCount++;
        }

        sw.Stop();

        _output.WriteLine($"[Large grid] Raycast {QueryIterations:N0} queries: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds * 1000 / QueryIterations:F2} us per query");
        _output.WriteLine($"Hit rate: {hitCount * 100.0 / QueryIterations:F1}%");
    }

    // ヘルパーメソッド

    private SpatialWorld CreateLargeWorld(int count)
    {
        var world = new SpatialWorld(new GridSAPBroadPhase(8f));
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            float x = (float)(random.NextDouble() * 1000 - 500);
            float y = (float)(random.NextDouble() * 1000 - 500);
            float z = (float)(random.NextDouble() * 1000 - 500);
            float radius = (float)(random.NextDouble() * 2 + 0.5);

            world.AddSphere(new Vector3(x, y, z), radius);
        }

        return world;
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

    private CapsuleSweepQuery CreateRandomCapsuleSweep(Random random)
    {
        float x = (float)(random.NextDouble() * 1000 - 500);
        float y = (float)(random.NextDouble() * 1000 - 500);
        float z = (float)(random.NextDouble() * 1000 - 500);

        float dx = (float)(random.NextDouble() * 100 - 50);
        float dy = (float)(random.NextDouble() * 100 - 50);
        float dz = (float)(random.NextDouble() * 100 - 50);

        return new CapsuleSweepQuery(
            new Vector3(x, y, z),
            new Vector3(x + dx, y + dy, z + dz),
            (float)(random.NextDouble() * 2 + 0.5));
    }

    private SlashQuery CreateRandomSlash(Random random)
    {
        float x = (float)(random.NextDouble() * 1000 - 500);
        float y = (float)(random.NextDouble() * 1000 - 500);
        float z = (float)(random.NextDouble() * 1000 - 500);

        float size = (float)(random.NextDouble() * 5 + 2);

        return new SlashQuery(
            new Vector3(x - size, y, z - size),
            new Vector3(x - size, y, z + size),
            new Vector3(x + size, y, z - size),
            new Vector3(x + size, y, z + size));
    }
}
