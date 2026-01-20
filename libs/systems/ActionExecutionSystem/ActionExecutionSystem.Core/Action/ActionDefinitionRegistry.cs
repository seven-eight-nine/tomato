using System;
using System.Collections.Generic;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// アクション定義の登録と取得を行うレジストリ。
/// ActionIdをキーとしてActionDefinitionを管理する。
/// </summary>
public class ActionDefinitionRegistry<TCategory> where TCategory : struct, Enum
{
    private readonly Dictionary<string, ActionDefinition<TCategory>> _definitions = new();

    /// <summary>
    /// アクション定義を登録する。
    /// </summary>
    public void Register(ActionDefinition<TCategory> definition)
    {
        _definitions[definition.ActionId] = definition;
    }

    /// <summary>
    /// アクション定義を取得する。
    /// </summary>
    /// <returns>見つからない場合はnull</returns>
    public ActionDefinition<TCategory>? Get(string actionId)
    {
        return _definitions.TryGetValue(actionId, out var def) ? def : null;
    }

    /// <summary>
    /// アクション定義が存在するか確認する。
    /// </summary>
    public bool Contains(string actionId)
    {
        return _definitions.ContainsKey(actionId);
    }

    /// <summary>
    /// 登録されている全アクションIDを取得する。
    /// </summary>
    public IEnumerable<string> GetAllActionIds()
    {
        return _definitions.Keys;
    }
}
