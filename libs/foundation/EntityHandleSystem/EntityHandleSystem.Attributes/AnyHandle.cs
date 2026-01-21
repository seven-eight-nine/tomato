using System;
using Tomato.HandleSystem;

namespace Tomato.EntityHandleSystem;

/// <summary>
/// 型消去されたエンティティハンドル。
/// 異なる種類のEntityHandleを一つのコンテナに格納するために使用します。
///
/// <example>
/// <code>
/// var mixedContainer = new EntityContainer&lt;AnyHandle&gt;();
/// mixedContainer.Add(bossHandle.ToAnyHandle());
/// mixedContainer.Add(enemyHandle.ToAnyHandle());
/// mixedContainer.Add(playerHandle.ToAnyHandle());
///
/// var iterator = mixedContainer.GetIterator();
/// while (iterator.MoveNext())
/// {
///     // すべてのエンティティを処理
///     if (iterator.Current.IsValid)
///     {
///         // ...
///     }
/// }
/// </code>
/// </example>
/// </summary>
public readonly struct AnyHandle : IEntityHandle, IEquatable<AnyHandle>
{
    private readonly IEntityArena _arena;
    private readonly int _index;
    private readonly int _generation;

    /// <summary>
    /// AnyHandleを作成します。
    /// 通常はEntityHandle.ToAnyHandle()を使用してください。
    /// </summary>
    /// <param name="arena">Arenaへの参照</param>
    /// <param name="index">エンティティのインデックス</param>
    /// <param name="generation">世代番号</param>
    public AnyHandle(IEntityArena arena, int index, int generation)
    {
        _arena = arena;
        _index = index;
        _generation = generation;
    }

    /// <summary>
    /// ハンドルが有効かどうかを返します。
    /// </summary>
    public bool IsValid => _arena != null && _arena.IsValid(_index, _generation);

    /// <summary>
    /// 無効なAnyHandleを返します。
    /// </summary>
    public static AnyHandle Invalid => default;

    /// <summary>
    /// エンティティのインデックスを取得します。
    /// </summary>
    public int Index => _index;

    /// <summary>
    /// 世代番号を取得します。
    /// </summary>
    public int Generation => _generation;

    /// <summary>
    /// Arenaを指定した型にキャストを試みます。
    /// </summary>
    /// <typeparam name="TArena">キャスト先のArena型</typeparam>
    /// <param name="arena">キャスト成功時のArena</param>
    /// <returns>キャスト成功時true</returns>
    public bool TryAs<TArena>(out TArena arena) where TArena : class, IEntityArena
    {
        arena = _arena as TArena;
        return arena != null;
    }

    /// <summary>
    /// 指定したコンポーネント型に対してアクションを実行します。
    /// Arena が IComponentArena&lt;TComponent&gt; を実装している場合のみ実行されます。
    ///
    /// <para>これにより、異なるエンティティ型を横串で操作できます。</para>
    ///
    /// <example>
    /// <code>
    /// // すべてのエンティティの PositionComponent に重力を適用
    /// foreach (var handle in mixedHandles)
    /// {
    ///     handle.TryExecute&lt;PositionComponent&gt;((ref PositionComponent p) =&gt;
    ///     {
    ///         p.Y -= 9.8f * deltaTime;
    ///     });
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="TComponent">コンポーネントの型</typeparam>
    /// <param name="action">コンポーネントに対して実行するアクション</param>
    /// <returns>アクションが実行された場合は true、Arena がコンポーネントを持たない場合やハンドルが無効な場合は false</returns>
    public bool TryExecute<TComponent>(RefAction<TComponent> action)
    {
        if (_arena is IComponentArena<TComponent> componentArena)
        {
            return componentArena.TryExecuteComponent(_index, _generation, action);
        }
        return false;
    }

    public bool Equals(AnyHandle other)
    {
        return ReferenceEquals(_arena, other._arena)
            && _index == other._index
            && _generation == other._generation;
    }

    public override bool Equals(object obj)
    {
        return obj is AnyHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (_arena?.GetHashCode() ?? 0);
            hash = hash * 31 + _index;
            hash = hash * 31 + _generation;
            return hash;
        }
    }

    public static bool operator ==(AnyHandle left, AnyHandle right) => left.Equals(right);
    public static bool operator !=(AnyHandle left, AnyHandle right) => !left.Equals(right);
}
