using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tomato.SystemPipeline;

/// <summary>
/// 並列実行グループ。
/// 追加されたシステムを並列に実行します。
/// </summary>
/// <remarks>
/// <para>
/// グループ内の全要素が同時に実行される可能性があります。
/// そのため、グループ内の要素間でデータ依存関係がないことを保証する必要があります。
/// </para>
/// <para>
/// 依存関係のある処理は SerialSystemGroup に分けて、
/// 独立した処理のみを ParallelSystemGroup に入れてください。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // AI, Animation, Audio は互いに独立しているので並列実行可能
/// var parallelGroup = new ParallelSystemGroup(
///     new AISystem(),
///     new AnimationSystem(),
///     new AudioSystem()
/// );
///
/// var mainLoop = new SerialSystemGroup(
///     new InputSystem(),
///     parallelGroup,           // ここで並列実行
///     new PhysicsSystem(),
///     new RenderSystem()
/// );
/// </code>
/// </example>
public sealed class ParallelSystemGroup : ISystemGroup
{
    private readonly List<IExecutable> _items;

    public bool IsEnabled { get; set; } = true;
    public int Count => _items.Count;
    public IExecutable this[int index] => _items[index];
    public IEnumerable<IExecutable> Items => _items;

    /// <summary>
    /// 空のグループを作成します。
    /// </summary>
    public ParallelSystemGroup()
    {
        _items = new List<IExecutable>();
    }

    /// <summary>
    /// 指定した要素でグループを作成します。
    /// </summary>
    /// <param name="items">並列実行する要素（ISystem または ISystemGroup）</param>
    public ParallelSystemGroup(params IExecutable[] items)
    {
        _items = new List<IExecutable>(items);
    }

    /// <summary>
    /// 指定したシステムでグループを作成します。
    /// </summary>
    /// <param name="systems">並列実行するシステム</param>
    public ParallelSystemGroup(params ISystem[] systems)
    {
        _items = new List<IExecutable>(systems.Length);
        foreach (var system in systems)
        {
            _items.Add(new SystemExecutableWrapper(system));
        }
    }

    /// <summary>
    /// グループ内の全要素を並列に実行します。
    /// </summary>
    public void Execute(IEntityRegistry registry, in SystemContext context)
    {
        if (!IsEnabled) return;
        if (_items.Count == 0) return;

        var localContext = context;
        var cancellationToken = context.CancellationToken;

        Parallel.ForEach(_items, item =>
        {
            if (cancellationToken.IsCancellationRequested) return;
            if (!item.IsEnabled) return;

            item.Execute(registry, in localContext);
        });
    }

    public void Add(IExecutable item)
    {
        _items.Add(item);
    }

    /// <summary>
    /// システムを追加します。
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
