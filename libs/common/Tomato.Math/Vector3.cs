using System;
using System.Runtime.CompilerServices;

namespace Tomato.Math;

/// <summary>
/// 3次元ベクトル。
/// 高性能な空間演算に使用する基本的な数学型。
/// </summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    public float LengthSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => X * X + Y * Y + Z * Z;
    }

    public float Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MathF.Sqrt(LengthSquared);
    }

    public Vector3 Normalized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var len = Length;
            if (len < float.Epsilon)
                return Zero;
            return this * (1f / len);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator +(Vector3 a, Vector3 b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 a, Vector3 b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Vector3 v, float scalar)
        => new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(float scalar, Vector3 v)
        => v * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator /(Vector3 v, float scalar)
        => new(v.X / scalar, v.Y / scalar, v.Z / scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 v)
        => new(-v.X, -v.Y, -v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector3 a, Vector3 b)
        => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Cross(Vector3 a, Vector3 b)
        => new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector3 a, Vector3 b)
        => (a - b).Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector3 a, Vector3 b)
        => (a - b).LengthSquared;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Min(Vector3 a, Vector3 b)
        => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Min(a.Z, b.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Max(Vector3 a, Vector3 b)
        => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Max(a.Z, b.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        => a + (b - a) * t;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Abs(Vector3 v)
        => new(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Clamp(Vector3 v, Vector3 min, Vector3 max)
        => new(
            MathF.Max(min.X, MathF.Min(max.X, v.X)),
            MathF.Max(min.Y, MathF.Min(max.Y, v.Y)),
            MathF.Max(min.Z, MathF.Min(max.Z, v.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Vector3 other)
        => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj)
        => obj is Vector3 other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + X.GetHashCode();
            hash = hash * 31 + Y.GetHashCode();
            hash = hash * 31 + Z.GetHashCode();
            return hash;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector3 left, Vector3 right)
        => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector3 left, Vector3 right)
        => !left.Equals(right);

    public override string ToString()
        => $"({X}, {Y}, {Z})";
}
