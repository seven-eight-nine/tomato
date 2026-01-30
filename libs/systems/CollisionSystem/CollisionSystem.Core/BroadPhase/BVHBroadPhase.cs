using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// Bounding Volume Hierarchy (BVH) による Broad Phase 実装。
/// SAH または Median 分割でトップダウン構築。遅延再構築をサポート。
/// 静的オブジェクトやレイキャストに適する。
/// </summary>
public sealed class BVHBroadPhase : IBroadPhase
{
    private const int BruteForceThreshold = 32;
    private const int InvalidIndex = -1;

    // BVHノード（最大 2N-1 ノード）
    private readonly BVHNode[] _nodes;
    private int _nodeCount;

    // Shape情報
    private readonly ShapeInfo[] _shapes;
    private readonly int[] _shapeIndices; // 再構築時のソート用
    private int _shapeCount;

    // 設定
    private readonly bool _useSAH;

    // 再構築フラグ
    private bool _needsRebuild;

    // クエリ用スタック
    private readonly int[] _queryStack;

    // ルートノード
    private int _rootIndex;

    /// <summary>
    /// BVHBroadPhase を作成する。
    /// </summary>
    /// <param name="maxShapes">最大Shape数</param>
    /// <param name="useSAH">SAH分割を使用するか（falseならMedian分割）</param>
    public BVHBroadPhase(int maxShapes = 1024, bool useSAH = true)
    {
        _useSAH = useSAH;

        // ノード数は最大 2N-1
        _nodes = new BVHNode[maxShapes * 2];
        _nodeCount = 0;

        _shapes = new ShapeInfo[maxShapes + 1];
        _shapeIndices = new int[maxShapes + 1];
        _shapeCount = 0;

        _queryStack = new int[64];
        _rootIndex = InvalidIndex;
        _needsRebuild = false;
    }

    /// <summary>
    /// SAH分割を使用しているか。
    /// </summary>
    public bool UseSAH => _useSAH;

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int ShapeCount => _shapeCount;

    /// <summary>
    /// 再構築が必要か。
    /// </summary>
    public bool NeedsRebuild => _needsRebuild;

    /// <summary>
    /// Shape を登録する。
    /// </summary>
    public void Add(int shapeIndex, in AABB aabb)
    {
        if (shapeIndex >= _shapes.Length)
            return;

        ref var shape = ref _shapes[shapeIndex];
        shape.IsActive = true;
        shape.AABB = aabb;

        _shapeCount++;
        _needsRebuild = true;
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(int shapeIndex)
    {
        if (shapeIndex >= _shapes.Length || !_shapes[shapeIndex].IsActive)
            return false;

        _shapes[shapeIndex].IsActive = false;
        _shapeCount--;
        _needsRebuild = true;

        return true;
    }

    /// <summary>
    /// Shape の AABB を更新する。
    /// </summary>
    public void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB)
    {
        if (shapeIndex >= _shapes.Length || !_shapes[shapeIndex].IsActive)
            return;

        _shapes[shapeIndex].AABB = newAABB;
        _needsRebuild = true;
    }

    /// <summary>
    /// 指定した AABB と重なる候補を列挙する。
    /// </summary>
    public int Query(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        // 再構築が必要なら実行
        if (_needsRebuild)
        {
            Rebuild();
        }

        if (_shapeCount <= BruteForceThreshold || _rootIndex == InvalidIndex)
        {
            return QueryBruteForce(queryAABB, candidates, allAABBs);
        }

        int count = 0;
        int stackTop = 0;
        _queryStack[stackTop++] = _rootIndex;

        while (stackTop > 0)
        {
            int nodeIndex = _queryStack[--stackTop];
            ref readonly var node = ref _nodes[nodeIndex];

            if (!node.Bounds.Intersects(queryAABB))
                continue;

            if (node.IsLeaf)
            {
                int shapeIndex = node.ShapeIndex;
                if (shapeIndex != InvalidIndex && _shapes[shapeIndex].IsActive)
                {
                    if (allAABBs[shapeIndex].Intersects(queryAABB) && count < candidates.Length)
                    {
                        candidates[count++] = shapeIndex;
                    }
                }
            }
            else
            {
                if (node.LeftChild != InvalidIndex && stackTop < _queryStack.Length)
                    _queryStack[stackTop++] = node.LeftChild;
                if (node.RightChild != InvalidIndex && stackTop < _queryStack.Length)
                    _queryStack[stackTop++] = node.RightChild;
            }
        }

        return count;
    }

    /// <summary>
    /// BVHを再構築する。
    /// </summary>
    public void Rebuild()
    {
        _nodeCount = 0;
        _rootIndex = InvalidIndex;

        // アクティブなShapeを収集
        int count = 0;
        for (int i = 0; i < _shapes.Length && count < _shapeIndices.Length; i++)
        {
            if (_shapes[i].IsActive)
            {
                _shapeIndices[count++] = i;
            }
        }

        if (count == 0)
        {
            _needsRebuild = false;
            return;
        }

        _rootIndex = BuildRecursive(_shapeIndices.AsSpan(0, count), 0);
        _needsRebuild = false;
    }

    /// <summary>
    /// 全データをクリアする。
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _shapes.Length; i++)
        {
            _shapes[i] = default;
        }

        _nodeCount = 0;
        _shapeCount = 0;
        _rootIndex = InvalidIndex;
        _needsRebuild = false;
    }

    private int QueryBruteForce(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        int count = 0;

        for (int i = 0; i < _shapes.Length && count < candidates.Length; i++)
        {
            if (!_shapes[i].IsActive)
                continue;

            if (allAABBs[i].Intersects(queryAABB))
            {
                candidates[count++] = i;
            }
        }

        return count;
    }

    private int BuildRecursive(Span<int> indices, int depth)
    {
        if (indices.Length == 0)
            return InvalidIndex;

        int nodeIndex = AllocateNode();
        if (nodeIndex == InvalidIndex)
            return InvalidIndex;

        ref var node = ref _nodes[nodeIndex];

        // 全体のAABBを計算
        var bounds = _shapes[indices[0]].AABB;
        for (int i = 1; i < indices.Length; i++)
        {
            bounds = AABB.Merge(bounds, _shapes[indices[i]].AABB);
        }
        node.Bounds = bounds;

        // リーフノード
        if (indices.Length == 1)
        {
            node.IsLeaf = true;
            node.ShapeIndex = indices[0];
            node.LeftChild = InvalidIndex;
            node.RightChild = InvalidIndex;
            return nodeIndex;
        }

        // 分割軸と位置を決定
        int axis;
        float splitPos;

        if (_useSAH)
        {
            FindSAHSplit(indices, bounds, out axis, out splitPos);
        }
        else
        {
            FindMedianSplit(indices, bounds, out axis, out splitPos);
        }

        // パーティション
        int mid = PartitionByAxis(indices, axis, splitPos);
        if (mid == 0 || mid == indices.Length)
        {
            mid = indices.Length / 2;
        }

        // 子ノードを再帰的に構築
        node.IsLeaf = false;
        node.ShapeIndex = InvalidIndex;
        node.LeftChild = BuildRecursive(indices.Slice(0, mid), depth + 1);
        node.RightChild = BuildRecursive(indices.Slice(mid), depth + 1);

        return nodeIndex;
    }

    private void FindSAHSplit(Span<int> indices, in AABB bounds, out int bestAxis, out float bestSplit)
    {
        bestAxis = 0;
        bestSplit = bounds.Center.X;
        float bestCost = float.MaxValue;

        var size = bounds.Size;
        float parentArea = 2f * (size.X * size.Y + size.Y * size.Z + size.Z * size.X);
        if (parentArea < 1e-6f)
            parentArea = 1f;

        // 各軸で評価
        for (int axis = 0; axis < 3; axis++)
        {
            // 中央値を候補として評価
            float mid = GetAxisValue(bounds.Center, axis);
            float cost = EvaluateSAHCost(indices, axis, mid, parentArea);

            if (cost < bestCost)
            {
                bestCost = cost;
                bestAxis = axis;
                bestSplit = mid;
            }
        }
    }

    private void FindMedianSplit(Span<int> indices, in AABB bounds, out int axis, out float splitPos)
    {
        // 最長軸を選択
        var size = bounds.Size;
        if (size.X >= size.Y && size.X >= size.Z)
            axis = 0;
        else if (size.Y >= size.Z)
            axis = 1;
        else
            axis = 2;

        splitPos = GetAxisValue(bounds.Center, axis);
    }

    private float EvaluateSAHCost(Span<int> indices, int axis, float splitPos, float parentArea)
    {
        int leftCount = 0, rightCount = 0;
        AABB leftBounds = default, rightBounds = default;
        bool leftInit = false, rightInit = false;

        foreach (int idx in indices)
        {
            float center = GetAxisValue(_shapes[idx].AABB.Center, axis);
            if (center < splitPos)
            {
                if (!leftInit)
                {
                    leftBounds = _shapes[idx].AABB;
                    leftInit = true;
                }
                else
                {
                    leftBounds = AABB.Merge(leftBounds, _shapes[idx].AABB);
                }
                leftCount++;
            }
            else
            {
                if (!rightInit)
                {
                    rightBounds = _shapes[idx].AABB;
                    rightInit = true;
                }
                else
                {
                    rightBounds = AABB.Merge(rightBounds, _shapes[idx].AABB);
                }
                rightCount++;
            }
        }

        if (leftCount == 0 || rightCount == 0)
            return float.MaxValue;

        var leftSize = leftBounds.Size;
        var rightSize = rightBounds.Size;
        float leftArea = 2f * (leftSize.X * leftSize.Y + leftSize.Y * leftSize.Z + leftSize.Z * leftSize.X);
        float rightArea = 2f * (rightSize.X * rightSize.Y + rightSize.Y * rightSize.Z + rightSize.Z * rightSize.X);

        return (leftArea * leftCount + rightArea * rightCount) / parentArea;
    }

    private int PartitionByAxis(Span<int> indices, int axis, float splitPos)
    {
        int left = 0;
        int right = indices.Length - 1;

        while (left <= right)
        {
            float center = GetAxisValue(_shapes[indices[left]].AABB.Center, axis);
            if (center < splitPos)
            {
                left++;
            }
            else
            {
                // スワップ
                (indices[left], indices[right]) = (indices[right], indices[left]);
                right--;
            }
        }

        return left;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetAxisValue(Vector3 v, int axis)
    {
        return axis switch
        {
            0 => v.X,
            1 => v.Y,
            _ => v.Z
        };
    }

    private int AllocateNode()
    {
        if (_nodeCount >= _nodes.Length)
            return InvalidIndex;

        return _nodeCount++;
    }

    private struct BVHNode
    {
        public AABB Bounds;
        public int LeftChild;
        public int RightChild;
        public int ShapeIndex;
        public bool IsLeaf;
    }

    private struct ShapeInfo
    {
        public bool IsActive;
        public AABB AABB;
    }
}
