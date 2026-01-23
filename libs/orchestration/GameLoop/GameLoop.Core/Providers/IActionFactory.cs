using System;
using Tomato.ActionExecutionSystem;

namespace Tomato.GameLoop.Providers;

/// <summary>
/// アクションを生成するファクトリインターフェース。
/// </summary>
/// <typeparam name="TCategory">アクションカテゴリのenum型</typeparam>
public interface IActionFactory<TCategory> where TCategory : struct, Enum
{
    /// <summary>
    /// アクションIDからExecutableActionを生成する。
    /// </summary>
    /// <param name="actionId">アクションID</param>
    /// <param name="category">カテゴリ</param>
    /// <returns>生成されたExecutableAction、または存在しない場合null</returns>
    IExecutableAction<TCategory>? Create(string actionId, TCategory category);
}
