using System;

namespace Tomato.CollisionSystem;

/// <summary>
/// 3次元ベクトル。
/// 衝突判定に使用する基本的な数学型。
/// </summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Zero => new(0f, 0f, 0f);
    public static Vector3 One => new(1f, 1f, 1f);
    public static Vector3 UnitX => new(1f, 0f, 0f);
    public static Vector3 UnitY => new(0f, 1f, 0f);
    public static Vector3 UnitZ => new(0f, 0f, 1f);

    public float LengthSquared => X * X + Y * Y + Z * Z;
    public float Length => MathF.Sqrt(LengthSquared);

    public Vector3 Normalized
    {
        get
        {
            var len = Length;
            if (len < float.Epsilon)
                return Zero;
            return this * (1f / len);
        }
    }

    public static Vector3 operator +(Vector3 a, Vector3 b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator *(Vector3 v, float scalar)
        => new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    public static Vector3 operator *(float scalar, Vector3 v)
        => v * scalar;

    public static Vector3 operator /(Vector3 v, float scalar)
        => new(v.X / scalar, v.Y / scalar, v.Z / scalar);

    public static Vector3 operator -(Vector3 v)
        => new(-v.X, -v.Y, -v.Z);

    public static float Dot(Vector3 a, Vector3 b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3 Cross(Vector3 a, Vector3 b)
        => new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

    public static float Distance(Vector3 a, Vector3 b)
        => (a - b).Length;

    public static float DistanceSquared(Vector3 a, Vector3 b)
        => (a - b).LengthSquared;

    public static Vector3 Min(Vector3 a, Vector3 b)
        => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Min(a.Z, b.Z));

    public static Vector3 Max(Vector3 a, Vector3 b)
        => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Max(a.Z, b.Z));

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        => a + (b - a) * t;

    public bool Equals(Vector3 other)
        => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj)
        => obj is Vector3 other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z);

    public static bool operator ==(Vector3 left, Vector3 right)
        => left.Equals(right);

    public static bool operator !=(Vector3 left, Vector3 right)
        => !left.Equals(right);

    public override string ToString()
        => $"({X}, {Y}, {Z})";
}
