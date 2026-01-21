using System;
using Xunit;
using Tomato.CollisionSystem;

namespace Tomato.CollisionSystem.Tests;

/// <summary>
/// 衝突形状テスト - TDD t-wada style
///
/// TODOリスト:
/// - [x] SphereShapeを作成できる
/// - [x] SphereShapeのAABBを取得できる
/// - [x] SphereShape同士の交差判定ができる
/// - [x] SphereShape同士が離れている場合は交差しない
/// - [x] CapsuleShapeを作成できる
/// - [x] CapsuleShapeのAABBを取得できる
/// - [x] BoxShapeを作成できる
/// - [x] BoxShapeのAABBを取得できる
/// - [x] CollisionContactが衝突情報を保持する
/// </summary>
public class CollisionShapeTests
{
    #region SphereShape Tests

    [Fact]
    public void SphereShape_ShouldBeCreatable()
    {
        var sphere = new SphereShape(1.0f);

        Assert.Equal(ShapeType.Sphere, sphere.Type);
        Assert.Equal(1.0f, sphere.Radius);
        Assert.Equal(Vector3.Zero, sphere.Offset);
    }

    [Fact]
    public void SphereShape_WithOffset_ShouldStoreOffset()
    {
        var offset = new Vector3(1f, 2f, 3f);
        var sphere = new SphereShape(1.0f, offset);

        Assert.Equal(offset, sphere.Offset);
    }

    [Fact]
    public void SphereShape_GetBounds_ShouldReturnCorrectAABB()
    {
        var sphere = new SphereShape(2.0f);
        var position = new Vector3(5f, 5f, 5f);

        var bounds = sphere.GetBounds(position);

        Assert.Equal(new Vector3(3f, 3f, 3f), bounds.Min);
        Assert.Equal(new Vector3(7f, 7f, 7f), bounds.Max);
    }

    [Fact]
    public void SphereShape_GetBounds_WithOffset_ShouldIncludeOffset()
    {
        var sphere = new SphereShape(1.0f, new Vector3(2f, 0f, 0f));
        var position = new Vector3(0f, 0f, 0f);

        var bounds = sphere.GetBounds(position);

        Assert.Equal(new Vector3(1f, -1f, -1f), bounds.Min);
        Assert.Equal(new Vector3(3f, 1f, 1f), bounds.Max);
    }

    [Fact]
    public void SphereShape_Intersects_WhenOverlapping_ShouldReturnTrue()
    {
        var sphere1 = new SphereShape(1.0f);
        var sphere2 = new SphereShape(1.0f);

        var pos1 = new Vector3(0f, 0f, 0f);
        var pos2 = new Vector3(1.5f, 0f, 0f); // 距離1.5、半径の合計2.0なので交差

        var result = sphere1.Intersects(pos1, sphere2, pos2, out var contact);

        Assert.True(result);
        Assert.True(contact.Penetration > 0);
    }

    [Fact]
    public void SphereShape_Intersects_WhenNotOverlapping_ShouldReturnFalse()
    {
        var sphere1 = new SphereShape(1.0f);
        var sphere2 = new SphereShape(1.0f);

        var pos1 = new Vector3(0f, 0f, 0f);
        var pos2 = new Vector3(3f, 0f, 0f); // 距離3.0、半径の合計2.0なので交差しない

        var result = sphere1.Intersects(pos1, sphere2, pos2, out var contact);

        Assert.False(result);
    }

    [Fact]
    public void SphereShape_Intersects_WhenTouching_ShouldReturnTrue()
    {
        var sphere1 = new SphereShape(1.0f);
        var sphere2 = new SphereShape(1.0f);

        var pos1 = new Vector3(0f, 0f, 0f);
        var pos2 = new Vector3(2f, 0f, 0f); // ちょうど接触

        var result = sphere1.Intersects(pos1, sphere2, pos2, out var contact);

        Assert.True(result);
        Assert.Equal(0f, contact.Penetration, 5);
    }

    [Fact]
    public void SphereShape_Intersects_ShouldReturnCorrectContactNormal()
    {
        var sphere1 = new SphereShape(1.0f);
        var sphere2 = new SphereShape(1.0f);

        var pos1 = new Vector3(0f, 0f, 0f);
        var pos2 = new Vector3(1f, 0f, 0f);

        sphere1.Intersects(pos1, sphere2, pos2, out var contact);

        // 法線はsphere1からsphere2への方向
        Assert.Equal(1f, contact.Normal.X, 5);
        Assert.Equal(0f, contact.Normal.Y, 5);
        Assert.Equal(0f, contact.Normal.Z, 5);
    }

    #endregion

    #region CapsuleShape Tests

    [Fact]
    public void CapsuleShape_ShouldBeCreatable()
    {
        var capsule = new CapsuleShape(0.5f, 2.0f);

        Assert.Equal(ShapeType.Capsule, capsule.Type);
        Assert.Equal(0.5f, capsule.Radius);
        Assert.Equal(2.0f, capsule.Height);
        Assert.Equal(CapsuleDirection.Y, capsule.Direction);
    }

    [Fact]
    public void CapsuleShape_WithDirection_ShouldStoreDirection()
    {
        var capsule = new CapsuleShape(0.5f, 2.0f, CapsuleDirection.X);

        Assert.Equal(CapsuleDirection.X, capsule.Direction);
    }

    [Fact]
    public void CapsuleShape_GetBounds_Y_ShouldReturnCorrectAABB()
    {
        var capsule = new CapsuleShape(1.0f, 2.0f, CapsuleDirection.Y);
        var position = new Vector3(0f, 0f, 0f);

        var bounds = capsule.GetBounds(position);

        // Y方向カプセル: height=2なので上下に1ずつ + radius=1
        Assert.Equal(new Vector3(-1f, -2f, -1f), bounds.Min);
        Assert.Equal(new Vector3(1f, 2f, 1f), bounds.Max);
    }

    [Fact]
    public void CapsuleShape_GetBounds_X_ShouldReturnCorrectAABB()
    {
        var capsule = new CapsuleShape(1.0f, 2.0f, CapsuleDirection.X);
        var position = new Vector3(0f, 0f, 0f);

        var bounds = capsule.GetBounds(position);

        // X方向カプセル
        Assert.Equal(new Vector3(-2f, -1f, -1f), bounds.Min);
        Assert.Equal(new Vector3(2f, 1f, 1f), bounds.Max);
    }

    #endregion

    #region BoxShape Tests

    [Fact]
    public void BoxShape_ShouldBeCreatable()
    {
        var halfExtents = new Vector3(1f, 2f, 3f);
        var box = new BoxShape(halfExtents);

        Assert.Equal(ShapeType.Box, box.Type);
        Assert.Equal(halfExtents, box.HalfExtents);
        Assert.Equal(Vector3.Zero, box.Offset);
    }

    [Fact]
    public void BoxShape_GetBounds_ShouldReturnCorrectAABB()
    {
        var box = new BoxShape(new Vector3(1f, 2f, 3f));
        var position = new Vector3(5f, 5f, 5f);

        var bounds = box.GetBounds(position);

        Assert.Equal(new Vector3(4f, 3f, 2f), bounds.Min);
        Assert.Equal(new Vector3(6f, 7f, 8f), bounds.Max);
    }

    [Fact]
    public void BoxShape_GetBounds_WithOffset_ShouldIncludeOffset()
    {
        var box = new BoxShape(new Vector3(1f, 1f, 1f), new Vector3(2f, 0f, 0f));
        var position = new Vector3(0f, 0f, 0f);

        var bounds = box.GetBounds(position);

        Assert.Equal(new Vector3(1f, -1f, -1f), bounds.Min);
        Assert.Equal(new Vector3(3f, 1f, 1f), bounds.Max);
    }

    #endregion

    #region CollisionContact Tests

    [Fact]
    public void CollisionContact_ShouldStoreCollisionInfo()
    {
        var point = new Vector3(1f, 2f, 3f);
        var normal = new Vector3(0f, 1f, 0f);
        var penetration = 0.5f;

        var contact = new CollisionContact(point, normal, penetration);

        Assert.Equal(point, contact.Point);
        Assert.Equal(normal, contact.Normal);
        Assert.Equal(penetration, contact.Penetration);
    }

    [Fact]
    public void CollisionContact_None_ShouldHaveZeroValues()
    {
        var none = CollisionContact.None;

        Assert.Equal(Vector3.Zero, none.Point);
        Assert.Equal(Vector3.Zero, none.Normal);
        Assert.Equal(0f, none.Penetration);
    }

    #endregion
}
