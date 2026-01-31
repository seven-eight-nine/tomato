using System;
using System.Collections.Generic;
using System.Linq;
using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 効果マネージャー実装
    /// リクエストはキューイングされ、ProcessTick時に処理される
    /// </summary>
    public sealed class EffectManager : IEffectManager
    {
        private readonly IEffectRegistry _effectRegistry;
        private readonly ITagRegistry _tagRegistry;
        private readonly EffectInstanceArena _arena;

        // ソート済みインスタンスリスト（Priority昇順 → EffectId昇順）
        private readonly Dictionary<ulong, List<EffectInstanceId>> _sortedInstancesByOwner = new();

        // リクエストキュー
        private readonly List<ApplyRequest> _pendingApplies = new();
        private readonly List<RemoveRequest> _pendingRemoves = new();
        private readonly List<StackChangeRequest> _pendingStackChanges = new();
        private readonly List<ExtendDurationRequest> _pendingExtendDurations = new();
        private readonly List<SetFlagRequest> _pendingSetFlags = new();

        private readonly object _lock = new();

        private GameTick _currentTick;

        public event Action<EffectAppliedEvent>? OnEffectApplied;
        public event Action<EffectRemovedEvent>? OnEffectRemoved;
        public event Action<StackChangedEvent>? OnStackChanged;

        public EffectManager(IEffectRegistry effectRegistry, ITagRegistry tagRegistry, int initialCapacity = 256)
        {
            _effectRegistry = effectRegistry ?? throw new ArgumentNullException(nameof(effectRegistry));
            _tagRegistry = tagRegistry ?? throw new ArgumentNullException(nameof(tagRegistry));
            _arena = new EffectInstanceArena(initialCapacity);
        }

        #region Apply

        public ApplyResult TryApply(ulong targetId, EffectId effectId, ulong sourceId, ApplyOptions? options = null)
        {
            options ??= ApplyOptions.Default;

            var definition = _effectRegistry.Get(effectId);
            if (definition == null)
                return ApplyResult.Failed(new FailureReasonId(FailureReasonId.DefinitionNotFound));

            lock (_lock)
            {
                // 先行キュー内の同EffectId・同ターゲットをチェック
                var pendingInstance = FindPendingApply(targetId, effectId, sourceId, definition);
                if (pendingInstance != null)
                {
                    // ペンディング中のリクエストにマージ（スタック合成など）
                    return HandlePendingMerge(pendingInstance.Value, definition, options);
                }

                // 既存インスタンスを探す
                var existingInstance = FindExistingInstance(targetId, effectId, sourceId, definition);

                if (existingInstance != null)
                {
                    // 既存インスタンスへのスタック合成をキューイング
                    return QueueMergeToExisting(existingInstance, definition, options);
                }

                // 新規インスタンス作成をキューイング
                return QueueNewApply(targetId, effectId, sourceId, definition, options);
            }
        }

        private ApplyRequest? FindPendingApply(ulong targetId, EffectId effectId, ulong sourceId, StatusEffectDefinition definition)
        {
            foreach (var req in _pendingApplies)
            {
                if (req.TargetId != targetId || req.EffectId != effectId)
                    continue;

                // ソース判定（定義のSourceIdentifierに従う）
                var pendingInstance = _arena.Get(req.InstanceId);
                if (pendingInstance != null && definition.StackConfig.SourceIdentifier.IsSameSource(pendingInstance, sourceId))
                    return req;
            }
            return null;
        }

        private ApplyResult HandlePendingMerge(ApplyRequest pendingRequest, StatusEffectDefinition definition, ApplyOptions options)
        {
            var instance = _arena.Get(pendingRequest.InstanceId);
            if (instance == null)
                return ApplyResult.Failed(new FailureReasonId(FailureReasonId.DefinitionNotFound));

            var mergeContext = new StackMergeContext(
                instance,
                options.InitialStacks,
                definition.StackConfig.MaxStacks,
                instance.SourceId,
                _currentTick);

            var mergeResult = definition.StackConfig.StackBehavior.Merge(mergeContext);

            switch (mergeResult.Action)
            {
                case StackMergeAction.UpdateExisting:
                    instance.CurrentStacks = mergeResult.NewStackCount;
                    return ApplyResult.Succeeded(pendingRequest.InstanceId, wasMerged: true);

                case StackMergeAction.Reject:
                    return ApplyResult.Succeeded(pendingRequest.InstanceId, wasMerged: true);

                default:
                    return ApplyResult.Succeeded(pendingRequest.InstanceId, wasMerged: true);
            }
        }

        private ApplyResult QueueMergeToExisting(EffectInstance existingInstance, StatusEffectDefinition definition, ApplyOptions options)
        {
            var mergeContext = new StackMergeContext(
                existingInstance,
                options.InitialStacks,
                definition.StackConfig.MaxStacks,
                existingInstance.SourceId,
                _currentTick);

            var mergeResult = definition.StackConfig.StackBehavior.Merge(mergeContext);

            switch (mergeResult.Action)
            {
                case StackMergeAction.UpdateExisting:
                    // スタック変更をキューイング
                    _pendingStackChanges.Add(new StackChangeRequest(
                        existingInstance.InstanceId,
                        mergeResult.NewStackCount,
                        isAbsolute: true));

                    // 期間更新もキューイング
                    var duration = options.Duration ?? definition.BaseDuration;
                    var durationContext = new DurationUpdateContext(existingInstance, duration, _currentTick);
                    var newExpiry = definition.StackConfig.DurationBehavior.CalculateNewExpiry(durationContext);
                    var extension = newExpiry - existingInstance.ExpiresAt;
                    if (extension.Value > 0)
                    {
                        _pendingExtendDurations.Add(new ExtendDurationRequest(
                            existingInstance.InstanceId,
                            extension));
                    }

                    return ApplyResult.Succeeded(existingInstance.InstanceId, wasMerged: true);

                case StackMergeAction.Reject:
                    return ApplyResult.Succeeded(existingInstance.InstanceId, wasMerged: true);

                case StackMergeAction.CreateNew:
                    // 新規作成に進む（通常このケースはない）
                    break;
            }

            return ApplyResult.Succeeded(existingInstance.InstanceId, wasMerged: true);
        }

        private ApplyResult QueueNewApply(ulong targetId, EffectId effectId, ulong sourceId, StatusEffectDefinition definition, ApplyOptions options)
        {
            // 新規インスタンスを先にアロケート（IDを確保）
            var duration = options.Duration ?? definition.BaseDuration;
            var expiresAt = definition.IsPermanent || duration.IsInfinite
                ? GameTick.MaxValue
                : _currentTick + duration;

            var initialFlags = options.InitialFlags ?? definition.InitialFlags;

            var instanceId = _arena.Allocate(
                effectId,
                targetId,
                sourceId,
                _currentTick,
                expiresAt,
                options.InitialStacks,
                initialFlags);

            // リクエストをキューイング
            _pendingApplies.Add(new ApplyRequest(instanceId, effectId, targetId, definition));

            return ApplyResult.Succeeded(instanceId);
        }

        private EffectInstance? FindExistingInstance(ulong targetId, EffectId effectId, ulong sourceId, StatusEffectDefinition definition)
        {
            if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                return null;

            foreach (var id in ownerList)
            {
                var instance = _arena.Get(id);
                if (instance == null || instance.DefinitionId != effectId)
                    continue;

                if (definition.StackConfig.SourceIdentifier.IsSameSource(instance, sourceId))
                    return instance;
            }

            return null;
        }

        #endregion

        #region Remove

        public void Remove(EffectInstanceId instanceId, RemovalReasonId reason)
        {
            lock (_lock)
            {
                _pendingRemoves.Add(new RemoveRequest(instanceId, reason));
            }
        }

        public void RemoveAll(ulong targetId, RemovalReasonId reason)
        {
            lock (_lock)
            {
                if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                    return;

                foreach (var id in ownerList)
                {
                    _pendingRemoves.Add(new RemoveRequest(id, reason));
                }
            }
        }

        public int RemoveByTag(ulong targetId, TagId tag, RemovalReasonId reason)
        {
            lock (_lock)
            {
                if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                    return 0;

                int count = 0;
                foreach (var id in ownerList)
                {
                    var instance = _arena.Get(id);
                    if (instance == null)
                        continue;

                    var definition = _effectRegistry.Get(instance.DefinitionId);
                    if (definition != null && definition.Tags.Contains(tag))
                    {
                        _pendingRemoves.Add(new RemoveRequest(id, reason));
                        count++;
                    }
                }

                return count;
            }
        }

        private void ExecuteRemove(RemoveRequest request)
        {
            var instance = _arena.Get(request.InstanceId);
            if (instance == null)
                return;

            var instanceId = instance.InstanceId;
            var definitionId = instance.DefinitionId;
            var ownerId = instance.OwnerId;
            var finalStacks = instance.CurrentStacks;

            // ソート済みリストから削除
            if (_sortedInstancesByOwner.TryGetValue(ownerId, out var ownerList))
            {
                ownerList.Remove(instanceId);
                if (ownerList.Count == 0)
                    _sortedInstancesByOwner.Remove(ownerId);
            }

            // Arenaから解放
            _arena.Free(instanceId);

            // イベント発火
            OnEffectRemoved?.Invoke(new EffectRemovedEvent(
                instanceId,
                definitionId,
                ownerId,
                request.Reason,
                _currentTick,
                finalStacks));
        }

        #endregion

        #region Modify

        public void AddStacks(EffectInstanceId instanceId, int count)
        {
            lock (_lock)
            {
                _pendingStackChanges.Add(new StackChangeRequest(instanceId, count, isAbsolute: false));
            }
        }

        public void SetStacks(EffectInstanceId instanceId, int count)
        {
            lock (_lock)
            {
                _pendingStackChanges.Add(new StackChangeRequest(instanceId, count, isAbsolute: true));
            }
        }

        public void ExtendDuration(EffectInstanceId instanceId, TickDuration extension)
        {
            lock (_lock)
            {
                _pendingExtendDurations.Add(new ExtendDurationRequest(instanceId, extension));
            }
        }

        public void SetFlag(EffectInstanceId instanceId, FlagId flag, bool value)
        {
            lock (_lock)
            {
                _pendingSetFlags.Add(new SetFlagRequest(instanceId, flag, value));
            }
        }

        private void ExecuteStackChange(StackChangeRequest request)
        {
            var instance = _arena.Get(request.InstanceId);
            if (instance == null)
                return;

            var definition = _effectRegistry.Get(instance.DefinitionId);
            if (definition == null)
                return;

            var oldStacks = instance.CurrentStacks;
            int newStacks;

            if (request.IsAbsolute)
            {
                newStacks = request.Delta;
            }
            else
            {
                newStacks = oldStacks + request.Delta;
            }

            if (definition.StackConfig.MaxStacks > 0)
                newStacks = Math.Min(newStacks, definition.StackConfig.MaxStacks);
            newStacks = Math.Max(0, newStacks);

            if (newStacks == 0)
            {
                ExecuteRemove(new RemoveRequest(request.InstanceId, new RemovalReasonId(RemovalReasonId.Expired)));
                return;
            }

            instance.CurrentStacks = newStacks;

            if (oldStacks != newStacks)
            {
                OnStackChanged?.Invoke(new StackChangedEvent(request.InstanceId, oldStacks, newStacks, _currentTick));
            }
        }

        private void ExecuteExtendDuration(ExtendDurationRequest request)
        {
            var instance = _arena.Get(request.InstanceId);
            if (instance == null)
                return;

            if (instance.ExpiresAt == GameTick.MaxValue)
                return; // 永続は延長不要

            instance.ExpiresAt = instance.ExpiresAt + request.Extension;
        }

        private void ExecuteSetFlag(SetFlagRequest request)
        {
            var instance = _arena.Get(request.InstanceId);
            if (instance == null)
                return;

            instance.Flags = request.Value
                ? instance.Flags.With(request.Flag)
                : instance.Flags.Without(request.Flag);
        }

        #endregion

        #region Query

        public EffectInstance? GetInstance(EffectInstanceId instanceId)
        {
            lock (_lock)
            {
                return _arena.Get(instanceId);
            }
        }

        public IEnumerable<EffectInstance> GetEffects(ulong targetId)
        {
            lock (_lock)
            {
                if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                    return Enumerable.Empty<EffectInstance>();

                var result = new List<EffectInstance>();
                foreach (var id in ownerList)
                {
                    var instance = _arena.Get(id);
                    if (instance != null)
                        result.Add(instance);
                }
                return result;
            }
        }

        public IEnumerable<EffectInstance> GetEffectsByTag(ulong targetId, TagId tag)
        {
            lock (_lock)
            {
                if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                    return Enumerable.Empty<EffectInstance>();

                var result = new List<EffectInstance>();
                foreach (var id in ownerList)
                {
                    var instance = _arena.Get(id);
                    if (instance == null)
                        continue;

                    var definition = _effectRegistry.Get(instance.DefinitionId);
                    if (definition != null && definition.Tags.Contains(tag))
                        result.Add(instance);
                }
                return result;
            }
        }

        public bool HasEffect(ulong targetId, EffectId effectId)
        {
            lock (_lock)
            {
                if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                    return false;

                foreach (var id in ownerList)
                {
                    var instance = _arena.Get(id);
                    if (instance != null && instance.DefinitionId == effectId)
                        return true;
                }
                return false;
            }
        }

        public bool HasEffectWithTag(ulong targetId, TagId tag)
        {
            lock (_lock)
            {
                if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                    return false;

                foreach (var id in ownerList)
                {
                    var instance = _arena.Get(id);
                    if (instance == null)
                        continue;

                    var definition = _effectRegistry.Get(instance.DefinitionId);
                    if (definition != null && definition.Tags.Contains(tag))
                        return true;
                }
                return false;
            }
        }

        #endregion

        #region Result

        public TResult CalculateResult<TResult>(ulong targetId, TResult initial) where TResult : struct
        {
            lock (_lock)
            {
                if (!_sortedInstancesByOwner.TryGetValue(targetId, out var ownerList))
                    return initial;

                // リストは既にソート済みなのでそのまま適用
                var result = initial;
                foreach (var id in ownerList)
                {
                    var instance = _arena.Get(id);
                    if (instance == null)
                        continue;

                    var definition = _effectRegistry.Get(instance.DefinitionId);
                    if (definition != null)
                    {
                        definition.ApplyToResult(ref result, instance.CurrentStacks);
                    }
                }
                return result;
            }
        }

        #endregion

        #region Tick

        public void ProcessTick(GameTick currentTick)
        {
            _currentTick = currentTick;

            lock (_lock)
            {
                // 1. Apply リクエストを処理（ソート済みリストに挿入）
                foreach (var request in _pendingApplies)
                {
                    ExecuteApply(request);
                }
                _pendingApplies.Clear();

                // 2. スタック変更を処理
                foreach (var request in _pendingStackChanges)
                {
                    ExecuteStackChange(request);
                }
                _pendingStackChanges.Clear();

                // 3. 期間延長を処理
                foreach (var request in _pendingExtendDurations)
                {
                    ExecuteExtendDuration(request);
                }
                _pendingExtendDurations.Clear();

                // 4. フラグ設定を処理
                foreach (var request in _pendingSetFlags)
                {
                    ExecuteSetFlag(request);
                }
                _pendingSetFlags.Clear();

                // 5. 削除リクエストを処理
                foreach (var request in _pendingRemoves)
                {
                    ExecuteRemove(request);
                }
                _pendingRemoves.Clear();

                // 6. 期限切れインスタンスを収集・削除
                var expired = new List<EffectInstance>();
                foreach (var instance in _arena.GetAll())
                {
                    if (instance.IsExpired(currentTick))
                        expired.Add(instance);
                }

                foreach (var instance in expired)
                {
                    ExecuteRemove(new RemoveRequest(instance.InstanceId, new RemovalReasonId(RemovalReasonId.Expired)));
                }
            }
        }

        private void ExecuteApply(ApplyRequest request)
        {
            var instance = _arena.Get(request.InstanceId);
            if (instance == null)
                return;

            // ソート済みリストに挿入
            if (!_sortedInstancesByOwner.TryGetValue(request.TargetId, out var ownerList))
            {
                ownerList = new List<EffectInstanceId>();
                _sortedInstancesByOwner[request.TargetId] = ownerList;
            }

            // 挿入位置を二分探索で特定
            var insertIndex = FindInsertIndex(ownerList, request.Definition.Priority, request.Definition.Id);
            ownerList.Insert(insertIndex, request.InstanceId);

            // イベント発火
            OnEffectApplied?.Invoke(new EffectAppliedEvent(
                request.InstanceId,
                request.EffectId,
                instance.OwnerId,
                instance.SourceId,
                instance.AppliedAt,
                instance.CurrentStacks,
                wasMerged: false));
        }

        /// <summary>
        /// ソート済みリストへの挿入位置を二分探索で求める
        /// ソート順: Priority昇順 → EffectId昇順
        /// </summary>
        private int FindInsertIndex(List<EffectInstanceId> sortedList, int priority, EffectId effectId)
        {
            int low = 0;
            int high = sortedList.Count;

            while (low < high)
            {
                int mid = (low + high) / 2;
                var midInstance = _arena.Get(sortedList[mid]);
                if (midInstance == null)
                {
                    low = mid + 1;
                    continue;
                }

                var midDefinition = _effectRegistry.Get(midInstance.DefinitionId);
                if (midDefinition == null)
                {
                    low = mid + 1;
                    continue;
                }

                int cmp = midDefinition.Priority.CompareTo(priority);
                if (cmp == 0)
                    cmp = midDefinition.Id.Value.CompareTo(effectId.Value);

                if (cmp < 0)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        #endregion
    }
}
