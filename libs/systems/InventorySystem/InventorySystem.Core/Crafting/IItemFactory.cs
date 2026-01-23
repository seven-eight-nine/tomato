using System;

namespace Tomato.InventorySystem;

/// <summary>
/// アイテム定義IDからアイテムインスタンスを生成するファクトリ。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public interface IItemFactory<TItem>
    where TItem : class, IInventoryItem
{
    /// <summary>
    /// 指定した定義IDのアイテムを生成する。
    /// </summary>
    /// <param name="definitionId">アイテム定義ID</param>
    /// <param name="stackCount">スタック数</param>
    /// <returns>生成されたアイテム、または生成できない場合はnull</returns>
    TItem? Create(ItemDefinitionId definitionId, int stackCount);
}

/// <summary>
/// デリゲートベースのアイテムファクトリ実装。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class DelegateItemFactory<TItem> : IItemFactory<TItem>
    where TItem : class, IInventoryItem
{
    private readonly Func<ItemDefinitionId, int, TItem?> _factory;

    public DelegateItemFactory(Func<ItemDefinitionId, int, TItem?> factory)
    {
        _factory = factory;
    }

    public TItem? Create(ItemDefinitionId definitionId, int stackCount)
    {
        return _factory(definitionId, stackCount);
    }
}
