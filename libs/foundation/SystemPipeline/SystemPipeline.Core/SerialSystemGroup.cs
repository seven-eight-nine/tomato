using System.Collections.Generic;

namespace Tomato.SystemPipeline;

/// <summary>
/// 直列実行グループ。
/// 追加された順序でシステムを1つずつ実行します。
/// </summary>
/// <example>
/// <code>
/// var group = new SerialSystemGroup(
///     new PhysicsSystem(),
///     new CollisionSystem(),
///     new DamageSystem()
/// );
///
/// // PhysicsSystem → CollisionSystem → DamageSystem の順で実行
/// pipeline.Execute(group, deltaTime);
/// </code>
/// </example>
public sealed class SerialSystemGroup : ISystemGroup
{
    private readonly List<IExecutable> _items;

    public bool IsEnabled { get; set; } = true;
    public int Count => _items.Count;
    public IExecutable this[int index] => _items[index];
    public IEnumerable<IExecutable> Items => _items;

    /// <summary>
    /// 空のグループを作成します。
    /// </summary>
    public SerialSystemGroup()
    {
        _items = new List<IExecutable>();
    }

    /// <summary>
    /// 指定した要素でグループを作成します。
    /// </summary>
    /// <param name="items">実行順序に並べた要素（ISystem または ISystemGroup）</param>
    public SerialSystemGroup(params IExecutable[] items)
    {
        _items = new List<IExecutable>(items);
    }

    /// <summary>
    /// 指定したシステムでグループを作成します。
    /// </summary>
    /// <param name="systems">実行順序に並べたシステム</param>
    public SerialSystemGroup(params ISystem[] systems)
    {
        _items = new List<IExecutable>(systems.Length);
        foreach (var system in systems)
        {
            _items.Add(new SystemExecutableWrapper(system));
        }
    }

    /// <summary>
    /// グループ内の全要素を順番に実行します。
    /// </summary>
    public void Execute(IEntityRegistry registry, in SystemContext context)
    {
        if (!IsEnabled) return;

        foreach (var item in _items)
        {
            if (context.CancellationToken.IsCancellationRequested) return;
            if (!item.IsEnabled) continue;

            item.Execute(registry, in context);
        }
    }

    public void Add(IExecutable item)
    {
        _items.Add(item);
    }

    /// <summary>
    /// システムを末尾に追加します。
    /// </summary>
    public void Add(ISystem system)
    {
        _items.Add(new SystemExecutableWrapper(system));
    }

    public void Insert(int index, IExecutable item)
    {
        _items.Insert(index, item);
    }

    /// <summary>
    /// システムを指定した位置に挿入します。
    /// </summary>
    public void Insert(int index, ISystem system)
    {
        _items.Insert(index, new SystemExecutableWrapper(system));
    }

    public bool Remove(IExecutable item)
    {
        return _items.Remove(item);
    }

    /// <summary>
    /// システムを削除します。
    /// </summary>
    public bool Remove(ISystem system)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] is SystemExecutableWrapper wrapper && wrapper.System == system)
            {
                _items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
}
