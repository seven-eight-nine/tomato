using System;
using System.Runtime.CompilerServices;

namespace Tomato.Math
{
    /// <summary>
    /// float 型に特化した数学関数群。
    /// .NET Standard 2.0 で MathF が使えないため、性能を維持しつつ提供する。
    /// </summary>
    public static class MathF
    {
        /// <summary>
        /// 円周率。
        /// </summary>
        public const float PI = 3.14159265358979323846f;

        /// <summary>
        /// 自然対数の底。
        /// </summary>
        public const float E = 2.71828182845904523536f;

        /// <summary>
        /// 平方根を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sqrt(float value)
            => (float)System.Math.Sqrt(value);

        /// <summary>
        /// 絶対値を返す。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float value)
            => value < 0f ? -value : value;

        /// <summary>
        /// 2つの値のうち小さい方を返す。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Min(float a, float b)
            => a < b ? a : b;

        /// <summary>
        /// 2つの値のうち大きい方を返す。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Max(float a, float b)
            => a > b ? a : b;

        /// <summary>
        /// 小数点以下を切り捨てる。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float value)
            => (float)System.Math.Floor(value);

        /// <summary>
        /// 小数点以下を切り上げる。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float value)
            => (float)System.Math.Ceiling(value);

        /// <summary>
        /// 四捨五入する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float value)
            => (float)System.Math.Round(value);

        /// <summary>
        /// 正弦を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float value)
            => (float)System.Math.Sin(value);

        /// <summary>
        /// 余弦を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float value)
            => (float)System.Math.Cos(value);

        /// <summary>
        /// 正接を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tan(float value)
            => (float)System.Math.Tan(value);

        /// <summary>
        /// 逆正弦を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Asin(float value)
            => (float)System.Math.Asin(value);

        /// <summary>
        /// 逆余弦を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Acos(float value)
            => (float)System.Math.Acos(value);

        /// <summary>
        /// 逆正接を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan(float value)
            => (float)System.Math.Atan(value);

        /// <summary>
        /// 2引数の逆正接を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan2(float y, float x)
            => (float)System.Math.Atan2(y, x);

        /// <summary>
        /// べき乗を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float x, float y)
            => (float)System.Math.Pow(x, y);

        /// <summary>
        /// 指数関数を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float value)
            => (float)System.Math.Exp(value);

        /// <summary>
        /// 自然対数を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log(float value)
            => (float)System.Math.Log(value);

        /// <summary>
        /// 常用対数を計算する。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Log10(float value)
            => (float)System.Math.Log10(value);

        /// <summary>
        /// 値を指定範囲にクランプする。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// 符号を返す (-1, 0, 1)。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(float value)
        {
            if (value > 0f) return 1;
            if (value < 0f) return -1;
            return 0;
        }

        /// <summary>
        /// 小数部分を返す。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Truncate(float value)
            => (float)System.Math.Truncate(value);
    }
}
