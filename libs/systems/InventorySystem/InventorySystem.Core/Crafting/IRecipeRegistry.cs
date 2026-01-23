using System.Collections.Generic;

namespace Tomato.InventorySystem;

/// <summary>
/// レシピ登録・検索のインターフェース。
/// </summary>
public interface IRecipeRegistry
{
    /// <summary>レシピを登録する</summary>
    void Register(ICraftingRecipe recipe);

    /// <summary>複数のレシピを登録する</summary>
    void RegisterRange(IEnumerable<ICraftingRecipe> recipes);

    /// <summary>レシピを取得する</summary>
    ICraftingRecipe? GetRecipe(RecipeId id);

    /// <summary>すべてのレシピを取得する</summary>
    IEnumerable<ICraftingRecipe> GetAllRecipes();

    /// <summary>指定した出力を生産できるレシピを取得する</summary>
    IEnumerable<ICraftingRecipe> GetRecipesForOutput(ItemDefinitionId outputDefinitionId);

    /// <summary>指定したタグを持つレシピを取得する</summary>
    IEnumerable<ICraftingRecipe> GetRecipesByTag(string tag);

    /// <summary>レシピが登録されているかどうか</summary>
    bool Contains(RecipeId id);
}

/// <summary>
/// レシピレジストリの実装。
/// </summary>
public sealed class RecipeRegistry : IRecipeRegistry
{
    private readonly Dictionary<RecipeId, ICraftingRecipe> _recipes = new();
    private readonly Dictionary<ItemDefinitionId, List<ICraftingRecipe>> _byOutput = new();
    private readonly Dictionary<string, List<ICraftingRecipe>> _byTag = new();

    public void Register(ICraftingRecipe recipe)
    {
        _recipes[recipe.Id] = recipe;

        // 出力でインデックス
        foreach (var output in recipe.Outputs)
        {
            if (!_byOutput.TryGetValue(output.DefinitionId, out var list))
            {
                list = new List<ICraftingRecipe>();
                _byOutput[output.DefinitionId] = list;
            }
            list.Add(recipe);
        }

        // タグでインデックス
        foreach (var tag in recipe.Tags)
        {
            if (!_byTag.TryGetValue(tag, out var list))
            {
                list = new List<ICraftingRecipe>();
                _byTag[tag] = list;
            }
            list.Add(recipe);
        }
    }

    public void RegisterRange(IEnumerable<ICraftingRecipe> recipes)
    {
        foreach (var recipe in recipes)
        {
            Register(recipe);
        }
    }

    public ICraftingRecipe? GetRecipe(RecipeId id)
    {
        return _recipes.TryGetValue(id, out var recipe) ? recipe : null;
    }

    public IEnumerable<ICraftingRecipe> GetAllRecipes()
    {
        return _recipes.Values;
    }

    public IEnumerable<ICraftingRecipe> GetRecipesForOutput(ItemDefinitionId outputDefinitionId)
    {
        return _byOutput.TryGetValue(outputDefinitionId, out var list)
            ? list
            : System.Array.Empty<ICraftingRecipe>();
    }

    public IEnumerable<ICraftingRecipe> GetRecipesByTag(string tag)
    {
        return _byTag.TryGetValue(tag, out var list)
            ? list
            : System.Array.Empty<ICraftingRecipe>();
    }

    public bool Contains(RecipeId id)
    {
        return _recipes.ContainsKey(id);
    }
}
