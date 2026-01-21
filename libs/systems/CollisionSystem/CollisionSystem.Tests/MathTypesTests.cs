using System;
using Xunit;
using Tomato.CollisionSystem;

namespace Tomato.CollisionSystem.Tests;

/// <summary>
/// 数学型テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] Vector3を作成できる
/// - [x] Vector3の加算ができる
/// - [x] Vector3の減算ができる
/// - [x] Vector3のスカラー乗算ができる
/// - [x] Vector3の長さを計算できる
/// - [x] Vector3の正規化ができる
/// - [x] Vector3の内積を計算できる
/// - [x] AABBを作成できる
/// - [x] AABBの交差判定ができる
/// - [x] AABBの包含判定ができる
/// </summary>
public class MathTypesTests
{
    #region Vector3 Tests

    [Fact]
    public void Vector3_ShouldBeCreatable()
    {
        var v = new Vector3(1f, 2f, 3f);

        Assert.Equal(1f, v.X);
        Assert.Equal(2f, v.Y);
        Assert.Equal(3f, v.Z);
    }

    [Fact]
    public void Vector3_Zero_ShouldReturnZeroVector()
    {
        var zero = Vector3.Zero;

        Assert.Equal(0f, zero.X);
        Assert.Equal(0f, zero.Y);
        Assert.Equal(0f, zero.Z);
    }

    [Fact]
    public void Vector3_Add_ShouldAddComponents()
    {
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);

        var result = a + b;

        Assert.Equal(5f, result.X);
        Assert.Equal(7f, result.Y);
        Assert.Equal(9f, result.Z);
    }

    [Fact]
    public void Vector3_Subtract_ShouldSubtractComponents()
    {
        var a = new Vector3(5f, 7f, 9f);
        var b = new Vector3(1f, 2f, 3f);

        var result = a - b;

        Assert.Equal(4f, result.X);
        Assert.Equal(5f, result.Y);
        Assert.Equal(6f, result.Z);
    }

    [Fact]
    public void Vector3_ScalarMultiply_ShouldMultiplyAllComponents()
    {
        var v = new Vector3(1f, 2f, 3f);

        var result = v * 2f;

        Assert.Equal(2f, result.X);
        Assert.Equal(4f, result.Y);
        Assert.Equal(6f, result.Z);
    }

    [Fact]
    public void Vector3_Length_ShouldCalculateMagnitude()
    {
        var v = new Vector3(3f, 4f, 0f);

        Assert.Equal(5f, v.Length);
    }

    [Fact]
    public void Vector3_LengthSquared_ShouldReturnSquaredMagnitude()
    {
        var v = new Vector3(3f, 4f, 0f);

        Assert.Equal(25f, v.LengthSquared);
    }

    [Fact]
    public void Vector3_Normalized_ShouldReturnUnitVector()
    {
        var v = new Vector3(3f, 4f, 0f);

        var normalized = v.Normalized;

        Assert.Equal(0.6f, normalized.X, 5);
        Assert.Equal(0.8f, normalized.Y, 5);
        Assert.Equal(0f, normalized.Z, 5);
        Assert.Equal(1f, normalized.Length, 5);
    }

    [Fact]
    public void Vector3_Dot_ShouldCalculateDotProduct()
    {
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);

        var dot = Vector3.Dot(a, b);

        Assert.Equal(32f, dot); // 1*4 + 2*5 + 3*6 = 4 + 10 + 18 = 32
    }

    [Fact]
    public void Vector3_Cross_ShouldCalculateCrossProduct()
    {
        var a = new Vector3(1f, 0f, 0f);
        var b = new Vector3(0f, 1f, 0f);

        var cross = Vector3.Cross(a, b);

        Assert.Equal(0f, cross.X);
        Assert.Equal(0f, cross.Y);
        Assert.Equal(1f, cross.Z);
    }

    [Fact]
    public void Vector3_Distance_ShouldCalculateDistanceBetweenPoints()
    {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(3f, 4f, 0f);

        var distance = Vector3.Distance(a, b);

        Assert.Equal(5f, distance);
    }

    #endregion

    #region AABB Tests

    [Fact]
    public void AABB_ShouldBeCreatable()
    {
        var min = new Vector3(0f, 0f, 0f);
        var max = new Vector3(1f, 1f, 1f);

        var aabb = new AABB(min, max);

        Assert.Equal(min, aabb.Min);
        Assert.Equal(max, aabb.Max);
    }

    [Fact]
    public void AABB_Center_ShouldReturnCenterPoint()
    {
        var aabb = new AABB(new Vector3(0f, 0f, 0f), new Vector3(2f, 4f, 6f));

        var center = aabb.Center;

        Assert.Equal(1f, center.X);
        Assert.Equal(2f, center.Y);
        Assert.Equal(3f, center.Z);
    }

    [Fact]
    public void AABB_Size_ShouldReturnDimensions()
    {
        var aabb = new AABB(new Vector3(1f, 2f, 3f), new Vector3(4f, 6f, 9f));

        var size = aabb.Size;

        Assert.Equal(3f, size.X);
        Assert.Equal(4f, size.Y);
        Assert.Equal(6f, size.Z);
    }

    [Fact]
    public void AABB_Intersects_WhenOverlapping_ShouldReturnTrue()
    {
        var a = new AABB(new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 2f));
        var b = new AABB(new Vector3(1f, 1f, 1f), new Vector3(3f, 3f, 3f));

        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
    }

    [Fact]
    public void AABB_Intersects_WhenNotOverlapping_ShouldReturnFalse()
    {
        var a = new AABB(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f));
        var b = new AABB(new Vector3(2f, 2f, 2f), new Vector3(3f, 3f, 3f));

        Assert.False(a.Intersects(b));
        Assert.False(b.Intersects(a));
    }

    [Fact]
    public void AABB_Intersects_WhenTouching_ShouldReturnTrue()
    {
        var a = new AABB(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f));
        var b = new AABB(new Vector3(1f, 0f, 0f), new Vector3(2f, 1f, 1f));

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void AABB_Contains_WhenPointInside_ShouldReturnTrue()
    {
        var aabb = new AABB(new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 2f));
        var point = new Vector3(1f, 1f, 1f);

        Assert.True(aabb.Contains(point));
    }

    [Fact]
    public void AABB_Contains_WhenPointOutside_ShouldReturnFalse()
    {
        var aabb = new AABB(new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 2f));
        var point = new Vector3(3f, 3f, 3f);

        Assert.False(aabb.Contains(point));
    }

    [Fact]
    public void AABB_Merge_ShouldReturnEnclosingAABB()
    {
        var a = new AABB(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f));
        var b = new AABB(new Vector3(2f, 2f, 2f), new Vector3(3f, 3f, 3f));

        var merged = AABB.Merge(a, b);

        Assert.Equal(new Vector3(0f, 0f, 0f), merged.Min);
        Assert.Equal(new Vector3(3f, 3f, 3f), merged.Max);
    }

    #endregion
}
