using System.Runtime.InteropServices;

namespace Tomato.CollisionSystem;

/// <summary>
/// SAP（Sweep and Prune）エントリ。
/// 主軸・副軸の min/max と ShapeIndex を保持。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SAPEntry
{
    /// <summary>
    /// 主軸の最小値（ソート対象）。
    /// </summary>
    public float MinPrimary;

    /// <summary>
    /// 主軸の最大値。
    /// </summary>
    public float MaxPrimary;

    /// <summary>
    /// 副軸の最小値（XZモード時使用）。
    /// </summary>
    public float MinSecondary;

    /// <summary>
    /// 副軸の最大値（XZモード時使用）。
    /// </summary>
    public float MaxSecondary;

    /// <summary>
    /// Shape のインデックス。
    /// </summary>
    public int ShapeIndex;

    public SAPEntry(float minPrimary, float maxPrimary, int shapeIndex)
    {
        MinPrimary = minPrimary;
        MaxPrimary = maxPrimary;
        MinSecondary = 0f;
        MaxSecondary = 0f;
        ShapeIndex = shapeIndex;
    }

    public SAPEntry(float minPrimary, float maxPrimary, float minSecondary, float maxSecondary, int shapeIndex)
    {
        MinPrimary = minPrimary;
        MaxPrimary = maxPrimary;
        MinSecondary = minSecondary;
        MaxSecondary = maxSecondary;
        ShapeIndex = shapeIndex;
    }
}
