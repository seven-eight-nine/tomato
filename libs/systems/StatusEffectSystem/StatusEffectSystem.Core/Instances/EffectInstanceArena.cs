using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// EffectInstanceのArena（オブジェクトプール + 世代管理）
    /// </summary>
    public sealed class EffectInstanceArena
    {
        private readonly object _lock = new();
        private EffectInstance?[] _instances;
        private int[] _generations;
        private int[] _freeIndices;
        private int _freeCount;
        private int _count;

        public int Count
        {
            get { lock (_lock) { return _count; } }
        }

        public int Capacity => _instances.Length;

        public EffectInstanceArena(int initialCapacity = 256)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _instances = new EffectInstance?[initialCapacity];
            _generations = new int[initialCapacity];
            _freeIndices = new int[initialCapacity];

            // 全インデックスを空きリストに追加（逆順で追加して0から使う）
            for (int i = initialCapacity - 1; i >= 0; i--)
            {
                _freeIndices[_freeCount++] = i;
                _generations[i] = 1; // 世代1からスタート
            }
        }

        /// <summary>新しいインスタンスを割り当て</summary>
        public EffectInstanceId Allocate(
            EffectId definitionId,
            ulong ownerId,
            ulong sourceId,
            GameTick appliedAt,
            GameTick expiresAt,
            int initialStacks,
            FlagSet initialFlags,
            IReadOnlyDictionary<string, FixedPoint>? snapshot = null)
        {
            lock (_lock)
            {
                if (_freeCount == 0)
                    Grow();

                int index = _freeIndices[--_freeCount];
                int generation = _generations[index];

                var instanceId = new EffectInstanceId(index, generation);

                _instances[index] = new EffectInstance(
                    instanceId,
                    definitionId,
                    ownerId,
                    sourceId,
                    appliedAt,
                    expiresAt,
                    initialStacks,
                    initialFlags,
                    snapshot);

                _count++;
                return instanceId;
            }
        }

        /// <summary>インスタンスを解放</summary>
        public bool Free(EffectInstanceId id)
        {
            lock (_lock)
            {
                if (!IsValidInternal(id))
                    return false;

                int index = id.Index;
                _instances[index] = null;
                _generations[index]++; // 世代をインクリメント
                _freeIndices[_freeCount++] = index;
                _count--;
                return true;
            }
        }

        /// <summary>インスタンスを取得</summary>
        public EffectInstance? Get(EffectInstanceId id)
        {
            lock (_lock)
            {
                return IsValidInternal(id) ? _instances[id.Index] : null;
            }
        }

        /// <summary>有効性チェック</summary>
        public bool IsValid(EffectInstanceId id)
        {
            lock (_lock)
            {
                return IsValidInternal(id);
            }
        }

        private bool IsValidInternal(EffectInstanceId id)
        {
            int index = id.Index;
            return index >= 0
                && index < _instances.Length
                && _generations[index] == id.Generation
                && _instances[index] != null;
        }

        private void Grow()
        {
            int oldCapacity = _instances.Length;
            int newCapacity = oldCapacity * 2;

            Array.Resize(ref _instances, newCapacity);
            Array.Resize(ref _generations, newCapacity);
            Array.Resize(ref _freeIndices, newCapacity);

            // 新しいスロットを空きリストに追加
            for (int i = newCapacity - 1; i >= oldCapacity; i--)
            {
                _freeIndices[_freeCount++] = i;
                _generations[i] = 1;
            }
        }

        /// <summary>全有効インスタンスを列挙</summary>
        public IEnumerable<EffectInstance> GetAll()
        {
            lock (_lock)
            {
                var result = new List<EffectInstance>(_count);
                for (int i = 0; i < _instances.Length; i++)
                {
                    var instance = _instances[i];
                    if (instance != null)
                        result.Add(instance);
                }
                return result;
            }
        }

        /// <summary>特定のオーナーの全インスタンスを列挙</summary>
        public IEnumerable<EffectInstance> GetByOwner(ulong ownerId)
        {
            lock (_lock)
            {
                var result = new List<EffectInstance>();
                for (int i = 0; i < _instances.Length; i++)
                {
                    var instance = _instances[i];
                    if (instance != null && instance.OwnerId == ownerId)
                        result.Add(instance);
                }
                return result;
            }
        }
    }
}
