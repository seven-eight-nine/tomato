using System;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// Broad Phase アルゴリズムのインターフェース。
/// AABB ベースの候補枝刈りを担当する。
/// </summary>
public interface IBroadPhase
{
    /// <summary>
    /// Shape を登録する。
    /// </summary>
    void Add(int shapeIndex, in AABB aabb);

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    bool Remove(int shapeIndex);

    /// <summary>
    /// Shape の AABB を更新する。
    /// </summary>
    void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB);

    /// <summary>
    /// 指定した AABB と重なる候補を列挙する。
    /// </summary>
    /// <param name="queryAABB">クエリAABB</param>
    /// <param name="candidates">候補を格納するバッファ</param>
    /// <param name="allAABBs">全ShapeのAABB配列（3軸最終判定用）</param>
    /// <returns>候補数</returns>
    int Query(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs);

    /// <summary>
    /// 全データをクリアする。
    /// </summary>
    void Clear();
}
