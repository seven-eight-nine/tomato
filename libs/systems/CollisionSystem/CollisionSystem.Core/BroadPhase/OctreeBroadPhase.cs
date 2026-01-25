using System;
using System.Runtime.CompilerServices;
using Tomato.Math;

namespace Tomato.CollisionSystem;

/// <summary>
/// 8分木（Octree）による Broad Phase 実装。
/// 階層的な空間分割で効率的な枝刈りを実現。
/// 疎な空間や静的シーンに適する。
/// </summary>
public sealed class OctreeBroadPhase : IBroadPhase
{
    private const int BruteForceThreshold = 32;
    private const int MaxObjectsPerNode = 8;
    private const int InvalidIndex = -1;

    // ノードプール
    private readonly Node[] _nodes;
    private int _nodeCount;
    private int _nodeFreeList;

    // オブジェクトプール
    private readonly ObjectEntry[] _objects;
    private int _objectCount;

    // 設定
    private readonly int _maxDepth;
    private readonly AABB _worldBounds;

    // クエリ用スタック（事前確保）
    private readonly int[] _queryStack;

    // ルートノードインデックス
    private int _rootIndex;

    /// <summary>
    /// OctreeBroadPhase を作成する。
    /// </summary>
    /// <param name="worldBounds">ワールド境界</param>
    /// <param name="maxDepth">最大深度</param>
    /// <param name="maxShapes">最大Shape数</param>
    public OctreeBroadPhase(in AABB worldBounds, int maxDepth = 8, int maxShapes = 1024)
    {
        _worldBounds = worldBounds;
        _maxDepth = maxDepth;

        // ノード数の上限を計算（8^(maxDepth+1) - 1）/ 7 だが、実際は遅延作成するので少なめに
        int maxNodes = System.Math.Min(maxShapes * 4, 65536);
        _nodes = new Node[maxNodes];
        _nodeCount = 0;
        _nodeFreeList = InvalidIndex;

        _objects = new ObjectEntry[maxShapes + 1];
        _objectCount = 0;

        _queryStack = new int[maxDepth * 8 + 64];

        // ルートノード作成
        _rootIndex = AllocateNode();
        _nodes[_rootIndex].Bounds = worldBounds;
        _nodes[_rootIndex].Depth = 0;
    }

    /// <summary>
    /// ワールド境界。
    /// </summary>
    public AABB WorldBounds => _worldBounds;

    /// <summary>
    /// 登録されている Shape 数。
    /// </summary>
    public int ShapeCount => _objectCount;

    /// <summary>
    /// Shape を登録する。
    /// </summary>
    public void Add(int shapeIndex, in AABB aabb)
    {
        if (shapeIndex >= _objects.Length)
            return;

        ref var obj = ref _objects[shapeIndex];
        obj.IsActive = true;
        obj.AABB = aabb;
        obj.NodeIndex = InvalidIndex;
        obj.NextInNode = InvalidIndex;

        InsertObject(shapeIndex, _rootIndex);
        _objectCount++;
    }

    /// <summary>
    /// Shape を削除する。
    /// </summary>
    public bool Remove(int shapeIndex)
    {
        if (shapeIndex >= _objects.Length || !_objects[shapeIndex].IsActive)
            return false;

        ref var obj = ref _objects[shapeIndex];
        int nodeIndex = obj.NodeIndex;

        if (nodeIndex != InvalidIndex)
        {
            RemoveObjectFromNode(shapeIndex, nodeIndex);
        }

        obj.IsActive = false;
        obj.NodeIndex = InvalidIndex;
        _objectCount--;

        return true;
    }

    /// <summary>
    /// Shape の AABB を更新する。
    /// </summary>
    public void Update(int shapeIndex, in AABB oldAABB, in AABB newAABB)
    {
        if (shapeIndex >= _objects.Length || !_objects[shapeIndex].IsActive)
            return;

        ref var obj = ref _objects[shapeIndex];
        int nodeIndex = obj.NodeIndex;

        // ノードの境界内に収まっているかチェック
        if (nodeIndex != InvalidIndex)
        {
            ref readonly var node = ref _nodes[nodeIndex];
            if (node.Bounds.Contains(newAABB.Min) && node.Bounds.Contains(newAABB.Max))
            {
                // 同じノード内なら更新のみ
                obj.AABB = newAABB;
                return;
            }
        }

        // 再挿入
        Remove(shapeIndex);
        Add(shapeIndex, newAABB);
    }

    /// <summary>
    /// 指定した AABB と重なる候補を列挙する。
    /// </summary>
    public int Query(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        if (_objectCount <= BruteForceThreshold)
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

            // 境界チェック
            if (!node.Bounds.Intersects(queryAABB))
                continue;

            // このノードのオブジェクトをチェック
            int objIndex = node.FirstObjectIndex;
            while (objIndex != InvalidIndex && count < candidates.Length)
            {
                ref readonly var obj = ref _objects[objIndex];
                if (obj.IsActive && allAABBs[objIndex].Intersects(queryAABB))
                {
                    candidates[count++] = objIndex;
                }
                objIndex = obj.NextInNode;
            }

            // 子ノードをスタックに追加
            if (node.HasChildren)
            {
                for (int i = 0; i < 8 && stackTop < _queryStack.Length; i++)
                {
                    int childIndex = node.FirstChildIndex + i;
                    if (childIndex < _nodeCount)
                    {
                        _queryStack[stackTop++] = childIndex;
                    }
                }
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

        for (int i = 0; i < _objects.Length; i++)
        {
            _objects[i] = default;
        }

        _nodeCount = 0;
        _nodeFreeList = InvalidIndex;
        _objectCount = 0;

        // ルートノード再作成
        _rootIndex = AllocateNode();
        _nodes[_rootIndex].Bounds = _worldBounds;
        _nodes[_rootIndex].Depth = 0;
    }

    private int QueryBruteForce(in AABB queryAABB, Span<int> candidates, ReadOnlySpan<AABB> allAABBs)
    {
        int count = 0;

        for (int i = 0; i < _objects.Length && count < candidates.Length; i++)
        {
            if (!_objects[i].IsActive)
                continue;

            if (allAABBs[i].Intersects(queryAABB))
            {
                candidates[count++] = i;
            }
        }

        return count;
    }

    private void InsertObject(int objectIndex, int nodeIndex)
    {
        ref var node = ref _nodes[nodeIndex];
        ref var obj = ref _objects[objectIndex];
        ref readonly var aabb = ref obj.AABB;

        // 最大深度に達したか、これ以上分割不要な場合は現在のノードに追加
        if (node.Depth >= _maxDepth || node.ObjectCount < MaxObjectsPerNode)
        {
            AddObjectToNode(objectIndex, nodeIndex);
            return;
        }

        // 子ノードを作成（まだ存在しない場合）
        if (!node.HasChildren)
        {
            SubdivideNode(nodeIndex);
        }

        // オブジェクトが完全に収まる子ノードを探す
        int bestChild = FindBestChild(aabb, nodeIndex);
        if (bestChild != InvalidIndex)
        {
            InsertObject(objectIndex, bestChild);
        }
        else
        {
            // どの子ノードにも収まらない場合は現在のノードに追加
            AddObjectToNode(objectIndex, nodeIndex);
        }
    }

    private int FindBestChild(in AABB aabb, int nodeIndex)
    {
        ref readonly var node = ref _nodes[nodeIndex];
        var center = node.Bounds.Center;

        for (int i = 0; i < 8; i++)
        {
            int childIndex = node.FirstChildIndex + i;
            if (childIndex >= _nodeCount)
                continue;

            ref readonly var child = ref _nodes[childIndex];
            if (child.Bounds.Contains(aabb.Min) && child.Bounds.Contains(aabb.Max))
            {
                return childIndex;
            }
        }

        return InvalidIndex;
    }

    private void SubdivideNode(int nodeIndex)
    {
        ref var node = ref _nodes[nodeIndex];

        if (_nodeCount + 8 > _nodes.Length)
            return;

        int firstChild = _nodeCount;
        node.FirstChildIndex = firstChild;
        node.HasChildren = true;

        var center = node.Bounds.Center;
        var min = node.Bounds.Min;
        var max = node.Bounds.Max;
        int childDepth = node.Depth + 1;

        // 8つの子ノードを作成
        for (int i = 0; i < 8; i++)
        {
            int childIndex = AllocateNode();
            ref var child = ref _nodes[childIndex];
            child.Depth = childDepth;

            // ビット演算で各軸の分割を決定
            float minX = (i & 1) == 0 ? min.X : center.X;
            float maxX = (i & 1) == 0 ? center.X : max.X;
            float minY = (i & 2) == 0 ? min.Y : center.Y;
            float maxY = (i & 2) == 0 ? center.Y : max.Y;
            float minZ = (i & 4) == 0 ? min.Z : center.Z;
            float maxZ = (i & 4) == 0 ? center.Z : max.Z;

            child.Bounds = new AABB(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddObjectToNode(int objectIndex, int nodeIndex)
    {
        ref var node = ref _nodes[nodeIndex];
        ref var obj = ref _objects[objectIndex];

        obj.NextInNode = node.FirstObjectIndex;
        obj.NodeIndex = nodeIndex;
        node.FirstObjectIndex = objectIndex;
        node.ObjectCount++;
    }

    private void RemoveObjectFromNode(int objectIndex, int nodeIndex)
    {
        ref var node = ref _nodes[nodeIndex];

        if (node.FirstObjectIndex == objectIndex)
        {
            node.FirstObjectIndex = _objects[objectIndex].NextInNode;
            node.ObjectCount--;
            return;
        }

        int prevIndex = node.FirstObjectIndex;
        while (prevIndex != InvalidIndex)
        {
            int nextIndex = _objects[prevIndex].NextInNode;
            if (nextIndex == objectIndex)
            {
                _objects[prevIndex].NextInNode = _objects[objectIndex].NextInNode;
                node.ObjectCount--;
                return;
            }
            prevIndex = nextIndex;
        }
    }

    private int AllocateNode()
    {
        if (_nodeFreeList != InvalidIndex)
        {
            int index = _nodeFreeList;
            _nodeFreeList = _nodes[index].FirstChildIndex;
            _nodes[index] = Node.Create();
            return index;
        }

        if (_nodeCount >= _nodes.Length)
            return InvalidIndex;

        int newIndex = _nodeCount++;
        _nodes[newIndex] = Node.Create();
        return newIndex;
    }

    private struct Node
    {
        public AABB Bounds;
        public int Depth;
        public int FirstChildIndex;
        public bool HasChildren;
        public int FirstObjectIndex;
        public int ObjectCount;

        public static Node Create() => new Node
        {
            FirstChildIndex = InvalidIndex,
            FirstObjectIndex = InvalidIndex
        };
    }

    private struct ObjectEntry
    {
        public bool IsActive;
        public AABB AABB;
        public int NodeIndex;
        public int NextInNode;

        public static ObjectEntry Create() => new ObjectEntry
        {
            NodeIndex = InvalidIndex,
            NextInNode = InvalidIndex
        };
    }
}
