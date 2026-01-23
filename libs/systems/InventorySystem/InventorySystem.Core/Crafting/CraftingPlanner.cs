using System;
using System.Collections.Generic;
using System.Linq;

namespace Tomato.InventorySystem;

/// <summary>
/// 再帰的なクラフト計画を立てるプランナー。
/// 必要な材料を許可されたレシピから探索し、クラフト手順を生成する。
/// </summary>
public sealed class CraftingPlanner
{
    private readonly IRecipeRegistry _registry;
    private readonly HashSet<RecipeId> _allowedRecipes;
    private readonly int _maxDepth;

    /// <summary>
    /// CraftingPlannerを作成する。
    /// </summary>
    /// <param name="registry">レシピレジストリ</param>
    /// <param name="allowedRecipes">自動クラフトに使用可能なレシピID（nullで全レシピ許可）</param>
    /// <param name="maxDepth">再帰の最大深さ</param>
    public CraftingPlanner(
        IRecipeRegistry registry,
        IEnumerable<RecipeId>? allowedRecipes = null,
        int maxDepth = 10)
    {
        _registry = registry;
        _allowedRecipes = allowedRecipes != null
            ? new HashSet<RecipeId>(allowedRecipes)
            : new HashSet<RecipeId>(_registry.GetAllRecipes().Select(r => r.Id));
        _maxDepth = maxDepth;
    }

    /// <summary>
    /// 指定したレシピを実行するためのクラフト計画を立てる。
    /// </summary>
    /// <typeparam name="TItem">アイテムの型</typeparam>
    /// <param name="targetRecipe">目標レシピ</param>
    /// <param name="sourceInventory">材料を取得するインベントリ</param>
    /// <param name="count">目標クラフト回数</param>
    /// <returns>クラフト計画</returns>
    public CraftingPlan CreatePlan<TItem>(
        ICraftingRecipe targetRecipe,
        IInventory<TItem> sourceInventory,
        int count = 1)
        where TItem : class, IInventoryItem
    {
        var steps = new List<CraftingStep>();
        var availableItems = new Dictionary<ItemDefinitionId, int>();

        // 現在の在庫を記録
        foreach (var item in sourceInventory.GetAll())
        {
            if (!availableItems.ContainsKey(item.DefinitionId))
            {
                availableItems[item.DefinitionId] = 0;
            }
            availableItems[item.DefinitionId] += item.StackCount;
        }

        // 再帰的に計画を立てる
        var missingItems = new List<CraftingIngredient>();
        var visited = new HashSet<RecipeId>();

        bool success = PlanRecursive(
            targetRecipe,
            count,
            availableItems,
            steps,
            missingItems,
            visited,
            0);

        if (!success)
        {
            return CraftingPlan.Failed(missingItems);
        }

        return CraftingPlan.Succeeded(steps);
    }

    /// <summary>
    /// 指定した出力を生産するためのクラフト計画を立てる。
    /// </summary>
    public CraftingPlan CreatePlanForOutput<TItem>(
        ItemDefinitionId outputDefinitionId,
        IInventory<TItem> sourceInventory,
        int count = 1)
        where TItem : class, IInventoryItem
    {
        // 出力を生産できるレシピを探す
        var recipes = _registry.GetRecipesForOutput(outputDefinitionId)
            .Where(r => _allowedRecipes.Contains(r.Id))
            .ToList();

        if (recipes.Count == 0)
        {
            return CraftingPlan.Failed(new[] { new CraftingIngredient(outputDefinitionId, count) });
        }

        // 最初に見つかったレシピで計画を立てる
        // TODO: 最適なレシピを選択するロジック
        var recipe = recipes[0];

        // 必要なクラフト回数を計算
        var output = recipe.Outputs.First(o => o.DefinitionId == outputDefinitionId);
        int craftCount = (count + output.Count - 1) / output.Count;

        return CreatePlan(recipe, sourceInventory, craftCount);
    }

    private bool PlanRecursive(
        ICraftingRecipe recipe,
        int count,
        Dictionary<ItemDefinitionId, int> availableItems,
        List<CraftingStep> steps,
        List<CraftingIngredient> missingItems,
        HashSet<RecipeId> visited,
        int depth)
    {
        if (depth > _maxDepth)
        {
            return false;
        }

        // 循環参照チェック
        if (visited.Contains(recipe.Id))
        {
            return false;
        }
        visited.Add(recipe.Id);

        try
        {
            // 各材料について
            foreach (var ingredient in recipe.Ingredients)
            {
                int required = ingredient.Count * count;
                int available = availableItems.TryGetValue(ingredient.DefinitionId, out var a) ? a : 0;

                if (available >= required)
                {
                    // 在庫で賄える
                    continue;
                }

                int shortage = required - available;

                // 不足分を生産できるレシピを探す
                var subRecipes = _registry.GetRecipesForOutput(ingredient.DefinitionId)
                    .Where(r => _allowedRecipes.Contains(r.Id))
                    .ToList();

                if (subRecipes.Count == 0)
                {
                    // 生産できない → 不足として記録
                    missingItems.Add(new CraftingIngredient(ingredient.DefinitionId, shortage));
                    continue;
                }

                // 最初のレシピで生産を試みる
                var subRecipe = subRecipes[0];
                var output = subRecipe.Outputs.First(o => o.DefinitionId == ingredient.DefinitionId);
                int subCraftCount = (shortage + output.Count - 1) / output.Count;

                // 再帰的に計画
                if (!PlanRecursive(subRecipe, subCraftCount, availableItems, steps, missingItems, visited, depth + 1))
                {
                    return false;
                }

                // サブレシピの出力を在庫に追加
                foreach (var subOutput in subRecipe.Outputs)
                {
                    if (!availableItems.ContainsKey(subOutput.DefinitionId))
                    {
                        availableItems[subOutput.DefinitionId] = 0;
                    }
                    availableItems[subOutput.DefinitionId] += subOutput.Count * subCraftCount;
                }
            }

            // このレシピをステップとして追加
            steps.Add(new CraftingStep(recipe, count));

            // 材料を消費、出力を追加
            foreach (var ingredient in recipe.Ingredients)
            {
                int required = ingredient.Count * count;
                if (availableItems.TryGetValue(ingredient.DefinitionId, out var currentAmount))
                {
                    availableItems[ingredient.DefinitionId] = currentAmount - required;
                }
            }

            foreach (var output in recipe.Outputs)
            {
                if (!availableItems.ContainsKey(output.DefinitionId))
                {
                    availableItems[output.DefinitionId] = 0;
                }
                availableItems[output.DefinitionId] += output.Count * count;
            }

            return missingItems.Count == 0;
        }
        finally
        {
            visited.Remove(recipe.Id);
        }
    }
}

/// <summary>
/// クラフト計画。
/// </summary>
public sealed class CraftingPlan
{
    /// <summary>計画が実行可能かどうか</summary>
    public bool IsExecutable { get; }

    /// <summary>実行するステップリスト（実行順）</summary>
    public IReadOnlyList<CraftingStep> Steps { get; }

    /// <summary>不足している材料（実行不可の場合）</summary>
    public IReadOnlyList<CraftingIngredient> MissingItems { get; }

    /// <summary>総ステップ数</summary>
    public int TotalSteps => Steps.Count;

    /// <summary>総クラフト回数</summary>
    public int TotalCraftOperations => Steps.Sum(s => s.Count);

    private CraftingPlan(
        bool isExecutable,
        IReadOnlyList<CraftingStep> steps,
        IReadOnlyList<CraftingIngredient> missingItems)
    {
        IsExecutable = isExecutable;
        Steps = steps;
        MissingItems = missingItems;
    }

    public static CraftingPlan Succeeded(IReadOnlyList<CraftingStep> steps) =>
        new(true, steps, Array.Empty<CraftingIngredient>());

    public static CraftingPlan Failed(IReadOnlyList<CraftingIngredient> missingItems) =>
        new(false, Array.Empty<CraftingStep>(), missingItems);

    /// <summary>
    /// 計画を実行する。
    /// </summary>
    public CraftingResult TryExecute<TItem>(
        CraftingManager<TItem> craftingManager,
        IInventory<TItem> sourceInventory,
        IInventory<TItem>? outputInventory = null)
        where TItem : class, IInventoryItem
    {
        if (!IsExecutable)
        {
            return CraftingResult.InsufficientMaterials(MissingItems);
        }

        outputInventory ??= sourceInventory;
        var allCreatedItems = new List<IInventoryItem>();

        foreach (var step in Steps)
        {
            var result = craftingManager.TryCraft(step.Recipe, sourceInventory, sourceInventory, step.Count);
            if (!result.Success)
            {
                return result;
            }
            if (result.CreatedItems != null)
            {
                allCreatedItems.AddRange(result.CreatedItems);
            }
        }

        return CraftingResult.Succeeded(allCreatedItems);
    }

    public override string ToString()
    {
        if (!IsExecutable)
        {
            return $"CraftingPlan(NotExecutable, Missing={MissingItems.Count})";
        }
        return $"CraftingPlan(Steps={Steps.Count}, TotalOps={TotalCraftOperations})";
    }
}

/// <summary>
/// クラフト計画の1ステップ。
/// </summary>
public readonly struct CraftingStep
{
    /// <summary>実行するレシピ</summary>
    public readonly ICraftingRecipe Recipe;

    /// <summary>クラフト回数</summary>
    public readonly int Count;

    public CraftingStep(ICraftingRecipe recipe, int count)
    {
        Recipe = recipe;
        Count = count;
    }

    public override string ToString() => $"Step({Recipe.Name} x{Count})";
}
