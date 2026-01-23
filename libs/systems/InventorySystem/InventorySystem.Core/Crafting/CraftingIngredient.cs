using System;

namespace Tomato.InventorySystem;

/// <summary>
/// クラフトに必要な材料を表す。
/// </summary>
public readonly struct CraftingIngredient
{
    /// <summary>材料のアイテム定義ID</summary>
    public readonly ItemDefinitionId DefinitionId;

    /// <summary>必要数量</summary>
    public readonly int Count;

    public CraftingIngredient(ItemDefinitionId definitionId, int count)
    {
        DefinitionId = definitionId;
        Count = count;
    }

    public CraftingIngredient(int definitionId, int count)
        : this(new ItemDefinitionId(definitionId), count)
    {
    }

    public override string ToString() => $"Ingredient({DefinitionId}, x{Count})";
}

/// <summary>
/// クラフトの出力を表す。
/// </summary>
public readonly struct CraftingOutput
{
    /// <summary>出力アイテムの定義ID</summary>
    public readonly ItemDefinitionId DefinitionId;

    /// <summary>出力数量</summary>
    public readonly int Count;

    /// <summary>出力アイテムを生成するファクトリ（nullの場合は外部で指定）</summary>
    public readonly Func<int, IInventoryItem>? ItemFactory;

    public CraftingOutput(ItemDefinitionId definitionId, int count, Func<int, IInventoryItem>? itemFactory = null)
    {
        DefinitionId = definitionId;
        Count = count;
        ItemFactory = itemFactory;
    }

    public CraftingOutput(int definitionId, int count, Func<int, IInventoryItem>? itemFactory = null)
        : this(new ItemDefinitionId(definitionId), count, itemFactory)
    {
    }

    public override string ToString() => $"Output({DefinitionId}, x{Count})";
}
