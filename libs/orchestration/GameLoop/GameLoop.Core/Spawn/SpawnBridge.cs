using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.GameLoop.Context;
using Tomato.UnitLODSystem;

namespace Tomato.GameLoop.Spawn;

/// <summary>
/// UnitLODSystemとEntityContextRegistryを接続するブリッジ。
/// UnitのUnitPhaseChangedイベントを監視し、Entity登録/削除を行う。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class SpawnBridge<TCategory> : ISpawnCompletionHandler
    where TCategory : struct, Enum
{
    private readonly EntityContextRegistry<TCategory> _registry;
    private readonly IEntitySpawner _arena;
    private readonly IEntityInitializer<TCategory> _initializer;
    private readonly Dictionary<Unit, AnyHandle> _unitToHandle;
    private readonly Dictionary<Unit, bool> _unitWasStable;

    /// <summary>
    /// SpawnBridgeを生成する。
    /// </summary>
    public SpawnBridge(
        EntityContextRegistry<TCategory> registry,
        IEntitySpawner arena,
        IEntityInitializer<TCategory> initializer)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _arena = arena ?? throw new ArgumentNullException(nameof(arena));
        _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
        _unitToHandle = new Dictionary<Unit, AnyHandle>();
        _unitWasStable = new Dictionary<Unit, bool>();
    }

    /// <summary>
    /// UnitをこのブリッジにConnect/接続する。
    /// </summary>
    /// <param name="unit">接続するUnit</param>
    public void Connect(Unit unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        unit.UnitPhaseChanged += OnUnitPhaseChanged;
        _unitWasStable[unit] = false;
    }

    /// <summary>
    /// Unitをこのブリッジから切断する。
    /// </summary>
    /// <param name="unit">切断するUnit</param>
    public void Disconnect(Unit unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        unit.UnitPhaseChanged -= OnUnitPhaseChanged;
        _unitWasStable.Remove(unit);
    }

    /// <summary>
    /// UnitからAnyHandleを取得する。
    /// </summary>
    public AnyHandle? GetHandle(Unit unit)
    {
        return _unitToHandle.TryGetValue(unit, out var handle) ? handle : null;
    }

    /// <summary>
    /// UnitからEntityContextを取得する。
    /// </summary>
    public EntityContext<TCategory>? GetContext(Unit unit)
    {
        if (!_unitToHandle.TryGetValue(unit, out var handle))
            return null;

        return _registry.GetContext(handle);
    }

    /// <summary>
    /// Unitの現在の状態を確認し、必要に応じてEntityを登録/更新する。
    /// 毎フレームTickの後に呼び出すことを推奨。
    /// </summary>
    public void UpdateUnit(Unit unit)
    {
        if (unit == null) return;

        var wasStable = _unitWasStable.TryGetValue(unit, out var ws) && ws;
        var isStable = unit.IsStable;

        if (!wasStable && isStable)
        {
            // Unit became stable - activate
            OnUnitReady(unit);
        }
        else if (wasStable && !isStable && unit.TargetState == 0)
        {
            // Unit started unloading to 0 - will be removed
            OnUnitUnloading(unit);
        }

        _unitWasStable[unit] = isStable;

        // Check if fully unloaded
        if (unit.TargetState == 0 && isStable && !_unitToHandle.ContainsKey(unit))
        {
            // Already removed or never registered
        }
        else if (unit.TargetState == 0 && isStable && _unitToHandle.ContainsKey(unit))
        {
            OnUnitRemoved(unit);
        }
    }

    // ========================================
    // ISpawnCompletionHandler 実装
    // ========================================

    /// <summary>
    /// Unitが安定状態になった時に呼ばれる。
    /// </summary>
    public void OnUnitReady(Unit unit)
    {
        if (_unitToHandle.ContainsKey(unit))
        {
            // 既に登録済みの場合は再アクティブ化
            var existingHandle = _unitToHandle[unit];
            if (_registry.TryGetContext(existingHandle, out var existingContext) && existingContext != null)
            {
                existingContext.IsActive = true;
            }
            return;
        }

        // 1. EntityArenaでEntityをSpawn
        var handle = _arena.Spawn();

        // 2. EntityContextを登録
        var context = _registry.Register(handle);
        context.Unit = unit;
        context.IsActive = true;

        // 3. データリソースからEntity初期化
        _initializer.Initialize(context, unit, null);

        // 4. マッピングを保存
        _unitToHandle[unit] = handle;
    }

    /// <summary>
    /// Unitがアンロードを開始した時に呼ばれる。
    /// </summary>
    public void OnUnitUnloading(Unit unit)
    {
        if (_unitToHandle.TryGetValue(unit, out var handle))
        {
            if (_registry.TryGetContext(handle, out var context) && context != null)
            {
                context.IsActive = false;
            }
        }
    }

    /// <summary>
    /// Unitが完全にアンロードされた時に呼ばれる。
    /// </summary>
    public void OnUnitRemoved(Unit unit)
    {
        if (_unitToHandle.TryGetValue(unit, out var handle))
        {
            _registry.MarkForDeletion(handle);
            _unitToHandle.Remove(unit);
        }
    }

    // ========================================
    // 内部メソッド
    // ========================================

    private void OnUnitPhaseChanged(object sender, UnitPhaseChangedEventArgs args)
    {
        // Phase changes are tracked, but main logic is in UpdateUnit
        // This event can be used for fine-grained control if needed
    }
}
