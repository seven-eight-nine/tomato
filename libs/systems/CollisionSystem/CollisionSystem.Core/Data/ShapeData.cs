using System.Runtime.InteropServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// 球体データ。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SphereData
{
    public Vector3 Center;
    public float Radius;

    public SphereData(Vector3 center, float radius)
    {
        Center = center;
        Radius = radius;
    }
}

/// <summary>
/// カプセルデータ。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CapsuleData
{
    public Vector3 Point1;
    public Vector3 Point2;
    public float Radius;

    public CapsuleData(Vector3 point1, Vector3 point2, float radius)
    {
        Point1 = point1;
        Point2 = point2;
        Radius = radius;
    }
}

/// <summary>
/// 円柱データ（回転なし、Y軸整列）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CylinderData
{
    public Vector3 BaseCenter;
    public float Height;
    public float Radius;

    public CylinderData(Vector3 baseCenter, float height, float radius)
    {
        BaseCenter = baseCenter;
        Height = height;
        Radius = radius;
    }
}

/// <summary>
/// ボックスデータ（Y軸回転対応）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BoxData
{
    public Vector3 Center;
    public Vector3 HalfExtents;
    /// <summary>
    /// Y軸周りの回転角（ラジアン）。
    /// </summary>
    public float Yaw;

    public BoxData(Vector3 center, Vector3 halfExtents, float yaw = 0f)
    {
        Center = center;
        HalfExtents = halfExtents;
        Yaw = yaw;
    }

    /// <summary>
    /// Min-Max形式でボックスを作成する（回転なし）。
    /// </summary>
    public static BoxData FromMinMax(Vector3 min, Vector3 max)
    {
        var center = (min + max) * 0.5f;
        var halfExtents = (max - min) * 0.5f;
        return new BoxData(center, halfExtents, 0f);
    }
}
