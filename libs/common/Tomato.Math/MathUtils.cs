using System;
using System.Runtime.CompilerServices;

namespace Tomato.Math;

/// <summary>
/// 数学ユーティリティ。
/// </summary>
public static class MathUtils
{
    /// <summary>
    /// 値を指定範囲にクランプする。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max)
        => MathF.Max(min, MathF.Min(max, value));

    /// <summary>
    /// 値を指定範囲にクランプする。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max)
        => System.Math.Max(min, System.Math.Min(max, value));

    /// <summary>
    /// 線形補間。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t)
        => a + (b - a) * t;

    /// <summary>
    /// 浮動小数点の近似等価判定。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ApproximatelyEqual(float a, float b, float epsilon = 1e-6f)
        => MathF.Abs(a - b) < epsilon;

    /// <summary>
    /// 2の累乗に切り上げる。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextPowerOfTwo(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    /// <summary>
    /// 2の累乗かどうか判定する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPowerOfTwo(int value)
        => value > 0 && (value & (value - 1)) == 0;

    /// <summary>
    /// 点から線分への最近接点のパラメータtを計算する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ClosestPointOnSegmentParameter(
        in Vector3 point,
        in Vector3 segmentStart,
        in Vector3 segmentEnd)
    {
        var segment = segmentEnd - segmentStart;
        var lengthSq = segment.LengthSquared;

        if (lengthSq < float.Epsilon)
            return 0f;

        var t = Vector3.Dot(point - segmentStart, segment) / lengthSq;
        return Clamp(t, 0f, 1f);
    }

    /// <summary>
    /// 点から線分への最近接点を計算する。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ClosestPointOnSegment(
        in Vector3 point,
        in Vector3 segmentStart,
        in Vector3 segmentEnd)
    {
        var t = ClosestPointOnSegmentParameter(point, segmentStart, segmentEnd);
        return Vector3.Lerp(segmentStart, segmentEnd, t);
    }

    /// <summary>
    /// 2つの線分間の最近接点のパラメータを計算する。
    /// </summary>
    public static void ClosestPointsBetweenSegments(
        in Vector3 p1, in Vector3 q1,
        in Vector3 p2, in Vector3 q2,
        out float s, out float t)
    {
        var d1 = q1 - p1;
        var d2 = q2 - p2;
        var r = p1 - p2;

        var a = Vector3.Dot(d1, d1);
        var e = Vector3.Dot(d2, d2);
        var f = Vector3.Dot(d2, r);

        const float epsilon = 1e-6f;

        if (a <= epsilon && e <= epsilon)
        {
            s = t = 0f;
            return;
        }

        if (a <= epsilon)
        {
            s = 0f;
            t = Clamp(f / e, 0f, 1f);
        }
        else
        {
            var c = Vector3.Dot(d1, r);
            if (e <= epsilon)
            {
                t = 0f;
                s = Clamp(-c / a, 0f, 1f);
            }
            else
            {
                var b = Vector3.Dot(d1, d2);
                var denom = a * e - b * b;

                if (denom != 0f)
                {
                    s = Clamp((b * f - c * e) / denom, 0f, 1f);
                }
                else
                {
                    s = 0f;
                }

                t = (b * s + f) / e;

                if (t < 0f)
                {
                    t = 0f;
                    s = Clamp(-c / a, 0f, 1f);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Clamp((b - c) / a, 0f, 1f);
                }
            }
        }
    }

    /// <summary>
    /// 2つの線分間の最近接点を計算する。
    /// </summary>
    public static void ClosestPointsBetweenSegments(
        in Vector3 p1, in Vector3 q1,
        in Vector3 p2, in Vector3 q2,
        out Vector3 closestOnSegment1,
        out Vector3 closestOnSegment2)
    {
        ClosestPointsBetweenSegments(p1, q1, p2, q2, out float s, out float t);
        closestOnSegment1 = Vector3.Lerp(p1, q1, s);
        closestOnSegment2 = Vector3.Lerp(p2, q2, t);
    }
}
