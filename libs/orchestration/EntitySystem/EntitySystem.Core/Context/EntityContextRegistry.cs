using System;
using System.Collections.Generic;
using Tomato.EntityHandleSystem;
using Tomato.SystemPipeline;

namespace Tomato.EntitySystem.Context;

/// <summary>
/// EntityContextの登録と検索を行うレジストリ。
/// IEntityRegistryを実装し、SystemPipelineと連携する。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public sealed class EntityContextRegistry<TCategory> : IEntityRegistry
    where TCategory : struct, Enum
{
    private readonly Dictionary<AnyHandle, EntityContext<TCategory>> _contexts;
    private readonly List<AnyHandle> _activeEntities;
    private readonly List<AnyHandle> _markedForDeletion;
    private readonly object _lock = new object();

    /// <summary>
    /// アクティブなEntity数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _activeEntities.Count;
            }
        }
    }

    /// <summary>
    /// EntityContextRegistryを生成する。
    /// </summary>
    public EntityContextRegistry()
    {
        _contexts = new Dictionary<AnyHandle, EntityContext<TCategory>>();
        _activeEntities = new List<AnyHandle>();
        _markedForDeletion = new List<AnyHandle>();
    }

    /// <summary>
    /// Entityが存在するか確認する。
    /// </summary>
    public bool Exists(AnyHandle handle)
    {
        lock (_lock)
        {
            return handle.IsValid && _contexts.ContainsKey(handle);
        }
    }

    /// <summary>
    /// 全Entityを取得する。
    /// </summary>
    public IReadOnlyList<AnyHandle> GetAllEntities()
    {
        lock (_lock)
        {
            // コピーを返してイテレーション中の変更を防ぐ
            return new List<AnyHandle>(_activeEntities);
        }
    }

    /// <summary>
    /// 指定した型のエンティティを取得する（現在は全Entityを返す）。
    /// </summary>
    public IReadOnlyList<AnyHandle> GetEntitiesOfType<TArena>() where TArena : class
    {
        // EntityContextRegistryでは型によるフィルタリングは行わない
        // 全てのEntityを返し、各システムで必要に応じてフィルタリングする
        return GetAllEntities();
    }

    // ========================================
    // Context管理
    // ========================================

    /// <summary>
    /// 新しいEntityContextを登録する。
    /// </summary>
    /// <param name="handle">登録するEntityのAnyHandle</param>
    /// <returns>作成されたEntityContext</returns>
    /// <exception cref="ArgumentException">既に登録されている場合</exception>
    public EntityContext<TCategory> Register(AnyHandle handle)
    {
        lock (_lock)
        {
            if (_contexts.ContainsKey(handle))
            {
                throw new ArgumentException($"Entity already registered: {handle}");
            }

            var context = new EntityContext<TCategory>(handle);
            _contexts[handle] = context;
            _activeEntities.Add(handle);
            return context;
        }
    }

    /// <summary>
    /// EntityContextを取得する。
    /// </summary>
    public bool TryGetContext(AnyHandle handle, out EntityContext<TCategory>? context)
    {
        lock (_lock)
        {
            return _contexts.TryGetValue(handle, out context);
        }
    }

    /// <summary>
    /// EntityContextを取得する。存在しない場合はnull。
    /// </summary>
    public EntityContext<TCategory>? GetContext(AnyHandle handle)
    {
        lock (_lock)
        {
            return _contexts.TryGetValue(handle, out var context) ? context : null;
        }
    }

    /// <summary>
    /// Entityを削除マークする。
    /// 実際の削除はProcessDeletions()で行われる。
    /// </summary>
    public void MarkForDeletion(AnyHandle handle)
    {
        lock (_lock)
        {
            if (_contexts.TryGetValue(handle, out var context))
            {
                if (!context.IsMarkedForDeletion)
                {
                    context.IsMarkedForDeletion = true;
                    _markedForDeletion.Add(handle);
                }
            }
        }
    }

    /// <summary>
    /// 削除マークされたEntity一覧を取得する。
    /// </summary>
    public IReadOnlyList<AnyHandle> GetMarkedForDeletion()
    {
        lock (_lock)
        {
            return new List<AnyHandle>(_markedForDeletion);
        }
    }

    /// <summary>
    /// 削除マークされたEntityを実際に削除する。
    /// </summary>
    public void ProcessDeletions()
    {
        lock (_lock)
        {
            foreach (var handle in _markedForDeletion)
            {
                if (_contexts.TryGetValue(handle, out var context))
                {
                    context.Reset();
                    _contexts.Remove(handle);
                    _activeEntities.Remove(handle);
                }
            }
            _markedForDeletion.Clear();
        }
    }

    /// <summary>
    /// 全てのEntityContextをクリアする。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var context in _contexts.Values)
            {
                context.Reset();
            }
            _contexts.Clear();
            _activeEntities.Clear();
            _markedForDeletion.Clear();
        }
    }
}
