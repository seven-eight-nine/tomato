using System;
using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// 即時クラフトを管理するマネージャー。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public sealed class CraftingManager<TItem>
    where TItem : class, IInventoryItem
{
    private readonly IRecipeRegistry _registry;
    private readonly IItemFactory<TItem> _itemFactory;

    /// <summary>クラフト完了時に発火するイベント</summary>
    public event Action<CraftingCompletedEvent<TItem>>? OnCraftingCompleted;

    /// <summary>
    /// CraftingManagerを作成する。
    /// </summary>
    /// <param name="registry">レシピレジストリ</param>
    /// <param name="itemFactory">アイテムファクトリ</param>
    public CraftingManager(IRecipeRegistry registry, IItemFactory<TItem> itemFactory)
    {
        _registry = registry;
        _itemFactory = itemFactory;
    }

    /// <summary>
    /// クラフトを実行する。材料は入力インベントリから消費され、結果は出力インベントリに追加される。
    /// </summary>
    /// <param name="recipe">レシピ</param>
    /// <param name="sourceInventory">材料を取得するインベントリ</param>
    /// <param name="outputInventory">結果を出力するインベントリ（nullの場合はsourceと同じ）</param>
    /// <param name="count">クラフト回数</param>
    /// <returns>クラフト結果</returns>
    public CraftingResult TryCraft(
        ICraftingRecipe recipe,
        IInventory<TItem> sourceInventory,
        IInventory<TItem>? outputInventory = null,
        int count = 1)
    {
        outputInventory ??= sourceInventory;

        // 材料チェック
        var missing = CheckIngredients(recipe, sourceInventory, count);
        if (missing.Count > 0)
        {
            return CraftingResult.InsufficientMaterials(missing);
        }

        // 出力先空きチェック
        int requiredSlots = 0;
        foreach (var output in recipe.Outputs)
        {
            requiredSlots++;
        }
        requiredSlots *= count;

        // スナップショット作成（ロールバック用）
        var sourceSnapshot = sourceInventory.CreateSnapshot();
        var outputSnapshot = sourceInventory != outputInventory ? outputInventory.CreateSnapshot() : null;

        try
        {
            // 材料を消費
            if (!ConsumeIngredients(recipe, sourceInventory, count))
            {
                sourceInventory.RestoreFromSnapshot(sourceSnapshot);
                return CraftingResult.InsufficientMaterials(CheckIngredients(recipe, sourceInventory, count));
            }

            // アイテムを生成
            var createdItems = new List<IInventoryItem>();
            for (int i = 0; i < count; i++)
            {
                foreach (var output in recipe.Outputs)
                {
                    TItem? item = null;

                    // レシピのファクトリを優先
                    if (output.ItemFactory != null)
                    {
                        var rawItem = output.ItemFactory(output.Count);
                        item = rawItem as TItem;
                    }

                    // グローバルファクトリにフォールバック
                    item ??= _itemFactory.Create(output.DefinitionId, output.Count);

                    if (item == null)
                    {
                        // ロールバック
                        sourceInventory.RestoreFromSnapshot(sourceSnapshot);
                        if (outputSnapshot != null)
                        {
                            outputInventory.RestoreFromSnapshot(outputSnapshot);
                        }
                        return CraftingResult.NoItemFactory();
                    }

                    var addResult = outputInventory.TryAdd(item, new AddContext(AddSource.Craft));
                    if (!addResult.Success)
                    {
                        // ロールバック
                        sourceInventory.RestoreFromSnapshot(sourceSnapshot);
                        if (outputSnapshot != null)
                        {
                            outputInventory.RestoreFromSnapshot(outputSnapshot);
                        }
                        return CraftingResult.OutputFull();
                    }

                    createdItems.Add(item);
                }
            }

            // イベント発火
            OnCraftingCompleted?.Invoke(new CraftingCompletedEvent<TItem>(
                recipe,
                sourceInventory.Id,
                outputInventory.Id,
                createdItems,
                count));

            return CraftingResult.Succeeded(createdItems);
        }
        catch
        {
            // 例外時はロールバック
            sourceInventory.RestoreFromSnapshot(sourceSnapshot);
            if (outputSnapshot != null)
            {
                outputInventory.RestoreFromSnapshot(outputSnapshot);
            }
            throw;
        }
    }

    /// <summary>
    /// レシピIDを指定してクラフトを実行する。
    /// </summary>
    public CraftingResult TryCraft(
        RecipeId recipeId,
        IInventory<TItem> sourceInventory,
        IInventory<TItem>? outputInventory = null,
        int count = 1)
    {
        var recipe = _registry.GetRecipe(recipeId);
        if (recipe == null)
        {
            return CraftingResult.RecipeNotFound();
        }

        return TryCraft(recipe, sourceInventory, outputInventory, count);
    }

    /// <summary>
    /// クラフト可能かどうかをチェックする。
    /// </summary>
    public bool CanCraft(ICraftingRecipe recipe, IInventory<TItem> sourceInventory, int count = 1)
    {
        var missing = CheckIngredients(recipe, sourceInventory, count);
        return missing.Count == 0;
    }

    /// <summary>
    /// 指定したレシピでクラフト可能な最大回数を取得する。
    /// </summary>
    public int GetMaxCraftCount(ICraftingRecipe recipe, IInventory<TItem> sourceInventory)
    {
        if (recipe.Ingredients.Count == 0)
        {
            return int.MaxValue;
        }

        int maxCount = int.MaxValue;
        foreach (var ingredient in recipe.Ingredients)
        {
            int available = sourceInventory.GetTotalStackCount(ingredient.DefinitionId);
            int possibleCount = available / ingredient.Count;
            maxCount = Math.Min(maxCount, possibleCount);
        }

        return maxCount;
    }

    /// <summary>
    /// 不足している材料をチェックする。
    /// </summary>
    public IReadOnlyList<CraftingIngredient> CheckIngredients(
        ICraftingRecipe recipe,
        IInventory<TItem> sourceInventory,
        int count = 1)
    {
        var missing = new List<CraftingIngredient>();

        foreach (var ingredient in recipe.Ingredients)
        {
            int required = ingredient.Count * count;
            int available = sourceInventory.GetTotalStackCount(ingredient.DefinitionId);
            if (available < required)
            {
                missing.Add(new CraftingIngredient(ingredient.DefinitionId, required - available));
            }
        }

        return missing;
    }

    private bool ConsumeIngredients(ICraftingRecipe recipe, IInventory<TItem> sourceInventory, int count)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            int toConsume = ingredient.Count * count;
            var items = new List<TItem>(sourceInventory.GetByDefinition(ingredient.DefinitionId));

            foreach (var item in items)
            {
                if (toConsume <= 0) break;

                int consumeFromThis = Math.Min(item.StackCount, toConsume);
                var result = sourceInventory.TryRemove(item.InstanceId, consumeFromThis);
                if (!result.Success)
                {
                    return false;
                }
                toConsume -= consumeFromThis;
            }

            if (toConsume > 0)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// クラフト完了イベント。
/// </summary>
/// <typeparam name="TItem">アイテムの型</typeparam>
public readonly struct CraftingCompletedEvent<TItem>
    where TItem : class, IInventoryItem
{
    /// <summary>使用されたレシピ</summary>
    public readonly ICraftingRecipe Recipe;

    /// <summary>材料を取得したインベントリのID</summary>
    public readonly InventoryId SourceInventoryId;

    /// <summary>出力先インベントリのID</summary>
    public readonly InventoryId OutputInventoryId;

    /// <summary>生成されたアイテム</summary>
    public readonly IReadOnlyList<IInventoryItem> CreatedItems;

    /// <summary>クラフト回数</summary>
    public readonly int CraftCount;

    public CraftingCompletedEvent(
        ICraftingRecipe recipe,
        InventoryId sourceInventoryId,
        InventoryId outputInventoryId,
        IReadOnlyList<IInventoryItem> createdItems,
        int craftCount)
    {
        Recipe = recipe;
        SourceInventoryId = sourceInventoryId;
        OutputInventoryId = outputInventoryId;
        CreatedItems = createdItems;
        CraftCount = craftCount;
    }
}
