namespace Tomato.CollisionSystem;

/// <summary>
/// Shape の種別。
/// </summary>
public enum ShapeType : byte
{
    /// <summary>
    /// 球体（中心＋半径）。
    /// </summary>
    Sphere = 0,

    /// <summary>
    /// カプセル（始点・終点・半径）。
    /// </summary>
    Capsule = 1,

    /// <summary>
    /// 円柱（底面中心・高さ・半径、回転なし）。
    /// </summary>
    Cylinder = 2,

    /// <summary>
    /// ボックス（中心・半サイズ、回転なし）。
    /// </summary>
    Box = 3,
}
