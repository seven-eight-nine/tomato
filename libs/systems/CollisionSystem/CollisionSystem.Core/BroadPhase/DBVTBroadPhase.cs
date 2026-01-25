using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// Dynamic Bounding Volume Tree (DBVT) による Broad Phase 実装。
/// Fat AABB を使用した増分更新で、動的オブジェクトや高頻度更新に適する。
/// </summary>
public sealed class DBVTBroadPhase : IBroadPhase
{
    private const int BruteForceThreshold = 32;
    private const int InvalidIndex = -1;

    // ノードプール
    private readonly DBVTNode[] _nodes;
    private int _nodeCount;
    private int _nodeFreeList;

    // Shape → Node マッピング
    private readonly int[] _shapeToNode;
    private int _shapeCount;

    // 設定
    private readonly float _fatMargin;

    // クエリ用スタック
    private readonly int[] _queryStack;

    // ルートノード
    private int _rootIndex;

    /// <summary>
    /// DBVTBroadPhase を作成する。
    /// </summary>
    /// <param name="maxShapes">最大Shape数</param>
    /// <param name="fatMargin">Fat AABBの余白</param>
    public DBVTBroadPhase(int maxShapes = 1024, float fatMargin = 0.1f)
    {
        _fatMargin = fatMargin;

        // ノード数は最大 2N-1
        _nodes = new DBVTNode[maxShapes * 2];
        _nodeCount = 0;
        _nodeFreeList = InvalidIndex;

        _shapeToNode = new int[maxShapes + 1];
        for (int i = 0; i < _shapeToNode.Length; i++)
        {
            _shapeToNode[i] = InvalidIndex;
        }
        _shapeCount = 0;

        _queryStack = new int[64];
        _rootIndex = InvalidIndex;
    }

    /// <summary>
    /// Fat AABBの余白。
    /// </summary>
    public float FatMargin => _fatMargin;

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int ShapeCount => _shapeCount;

    /// <summary>
    /// Shape を登録する。
    /// </summary>
    public void Add(int shapeIndex, in AABB aabb)
    {
        if (shapeIndex >= _shapeToNode.Length)
            return;

        // Fat AABBを作成
        var fatAABB = aabb.Expand(_fatMargin);

        // リーフノードを作成
        int leafIndex = AllocateNode();
        if (leafIndex == InvalidIndex)
            return;

        ref var leaf = ref _nodes[leafIndex];
        leaf.Bounds = fatAABB;
        leaf.ShapeIndex = shapeIndex;
        leaf.IsLeaf = true;
        leaf.Parent = InvalidIndex;
        leaf.LeftChild = InvalidIndex;
        leaf.RightChild = InvalidIndex;

        _shapeToNode[shapeIndex] = leafIndex;
        _shapeCount++;

        // ツリーに挿入
        InsertLeaf(leafIndex);
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(int shapeIndex)
    {
        if (shapeIndex >= _shapeToNode.Length)
            return false;

        int leafIndex = _shapeToNode[shapeIndex];
        if (leafIndex == InvalidIndex)
            return false;

        RemoveLeaf(leafIndex);
        FreeNode(leafIndex);

        _shapeToNode[shapeIndex] = InvalidIndex;
        _shapeCount--;

        return true;
    }

    /// <summary>
    /// Shape の AABB を更新する。
    /// </summary>
    public void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB)
    {
        if (shapeIndex >= _shapeToNode.Length)
            return;

        int leafIndex = _shapeToNode[shapeIndex];
        if (leafIndex == InvalidIndex)
            return;

        ref var leaf = ref _nodes[leafIndex];

        // Fat AABB内に収まっていれば更新不要
        if (leaf.Bounds.Contains(newAABB.Min) && leaf.Bounds.Contains(newAABB.Max))
        {
            return;
        }

        // 再挿入
        RemoveLeaf(leafIndex);

        // 新しいFat AABBを設定
        var fatAABB = newAABB.Expand(_fatMargin);
        leaf.Bounds = fatAABB;

        InsertLeaf(leafIndex);
    }

    /// <summary>
    /// 指定した AABB と重なる候補を列挙する。
    /// </summary>
    public int Query(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
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
                if (shapeIndex != InvalidIndex && allAABBs[shapeIndex].Intersects(queryAABB))
                {
                    if (count < candidates.Length)
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
    /// 全データをクリアする。
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _nodeCount; i++)
        {
            _nodes[i] = default;
        }

        for (int i = 0; i < _shapeToNode.Length; i++)
        {
            _shapeToNode[i] = InvalidIndex;
        }

        _nodeCount = 0;
        _nodeFreeList = InvalidIndex;
        _shapeCount = 0;
        _rootIndex = InvalidIndex;
    }

    private int QueryBruteForce(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        int count = 0;

        for (int i = 0; i < _shapeToNode.Length && count < candidates.Length; i++)
        {
            if (_shapeToNode[i] == InvalidIndex)
                continue;

            if (allAABBs[i].Intersects(queryAABB))
            {
                candidates[count++] = i;
            }
        }

        return count;
    }

    private void InsertLeaf(int leafIndex)
    {
        if (_rootIndex == InvalidIndex)
        {
            _rootIndex = leafIndex;
            _nodes[leafIndex].Parent = InvalidIndex;
            return;
        }

        ref readonly var leafBounds = ref _nodes[leafIndex].Bounds;

        // 最適な挿入位置を探す
        int sibling = FindBestSibling(leafIndex);

        // 新しい親ノードを作成
        int oldParent = _nodes[sibling].Parent;
        int newParent = AllocateNode();
        if (newParent == InvalidIndex)
            return;

        ref var newParentNode = ref _nodes[newParent];
        newParentNode.Parent = oldParent;
        newParentNode.Bounds = AABB.Merge(_nodes[sibling].Bounds, leafBounds);
        newParentNode.IsLeaf = false;
        newParentNode.ShapeIndex = InvalidIndex;

        if (oldParent != InvalidIndex)
        {
            ref var oldParentNode = ref _nodes[oldParent];
            if (oldParentNode.LeftChild == sibling)
                oldParentNode.LeftChild = newParent;
            else
                oldParentNode.RightChild = newParent;
        }
        else
        {
            _rootIndex = newParent;
        }

        newParentNode.LeftChild = sibling;
        newParentNode.RightChild = leafIndex;
        _nodes[sibling].Parent = newParent;
        _nodes[leafIndex].Parent = newParent;

        // 祖先のAABBを更新
        RefitAncestors(newParent);
    }

    private int FindBestSibling(int leafIndex)
    {
        ref readonly var leafBounds = ref _nodes[leafIndex].Bounds;
        int index = _rootIndex;

        while (!_nodes[index].IsLeaf)
        {
            ref readonly var node = ref _nodes[index];
            int left = node.LeftChild;
            int right = node.RightChild;

            float area = GetSurfaceArea(node.Bounds);

            var combinedBounds = AABB.Merge(node.Bounds, leafBounds);
            float combinedArea = GetSurfaceArea(combinedBounds);

            float cost = 2f * combinedArea;
            float inheritanceCost = 2f * (combinedArea - area);

            // 左子のコスト
            float costLeft;
            if (_nodes[left].IsLeaf)
            {
                var leftCombined = AABB.Merge(_nodes[left].Bounds, leafBounds);
                costLeft = GetSurfaceArea(leftCombined) + inheritanceCost;
            }
            else
            {
                var leftCombined = AABB.Merge(_nodes[left].Bounds, leafBounds);
                float oldArea = GetSurfaceArea(_nodes[left].Bounds);
                float newArea = GetSurfaceArea(leftCombined);
                costLeft = (newArea - oldArea) + inheritanceCost;
            }

            // 右子のコスト
            float costRight;
            if (_nodes[right].IsLeaf)
            {
                var rightCombined = AABB.Merge(_nodes[right].Bounds, leafBounds);
                costRight = GetSurfaceArea(rightCombined) + inheritanceCost;
            }
            else
            {
                var rightCombined = AABB.Merge(_nodes[right].Bounds, leafBounds);
                float oldArea = GetSurfaceArea(_nodes[right].Bounds);
                float newArea = GetSurfaceArea(rightCombined);
                costRight = (newArea - oldArea) + inheritanceCost;
            }

            // 最小コストを選択
            if (cost < costLeft && cost < costRight)
                break;

            index = costLeft < costRight ? left : right;
        }

        return index;
    }

    private void RemoveLeaf(int leafIndex)
    {
        if (leafIndex == _rootIndex)
        {
            _rootIndex = InvalidIndex;
            return;
        }

        int parent = _nodes[leafIndex].Parent;
        int grandParent = _nodes[parent].Parent;

        // 兄弟を見つける
        int sibling = _nodes[parent].LeftChild == leafIndex
            ? _nodes[parent].RightChild
            : _nodes[parent].LeftChild;

        if (grandParent != InvalidIndex)
        {
            // 親を削除し、兄弟を祖父に接続
            if (_nodes[grandParent].LeftChild == parent)
                _nodes[grandParent].LeftChild = sibling;
            else
                _nodes[grandParent].RightChild = sibling;

            _nodes[sibling].Parent = grandParent;
            FreeNode(parent);

            RefitAncestors(grandParent);
        }
        else
        {
            // 親がルートだった場合、兄弟がルートになる
            _rootIndex = sibling;
            _nodes[sibling].Parent = InvalidIndex;
            FreeNode(parent);
        }
    }

    private void RefitAncestors(int index)
    {
        while (index != InvalidIndex)
        {
            ref var node = ref _nodes[index];

            if (!node.IsLeaf)
            {
                var leftBounds = _nodes[node.LeftChild].Bounds;
                var rightBounds = _nodes[node.RightChild].Bounds;
                node.Bounds = AABB.Merge(leftBounds, rightBounds);
            }

            index = node.Parent;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetSurfaceArea(in AABB aabb)
    {
        var size = aabb.Size;
        return 2f * (size.X * size.Y + size.Y * size.Z + size.Z * size.X);
    }

    private int AllocateNode()
    {
        if (_nodeFreeList != InvalidIndex)
        {
            int index = _nodeFreeList;
            _nodeFreeList = _nodes[index].LeftChild;
            _nodes[index] = default;
            return index;
        }

        if (_nodeCount >= _nodes.Length)
            return InvalidIndex;

        return _nodeCount++;
    }

    private void FreeNode(int nodeIndex)
    {
        _nodes[nodeIndex].LeftChild = _nodeFreeList;
        _nodeFreeList = nodeIndex;
    }

    private struct DBVTNode
    {
        public AABB Bounds;
        public int Parent;
        public int LeftChild;
        public int RightChild;
        public int ShapeIndex;
        public bool IsLeaf;
    }
}
