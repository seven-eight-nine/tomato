using System;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// アクションの実行ロジックを実装するインターフェース。
/// </summary>
public interface IActionExecutor<TCategory> where TCategory : struct, Enum
{
    /// <summary>アクション開始時。</summary>
    void OnActionStart(IExecutableAction<TCategory> action);

    /// <summary>アクションtick時。</summary>
    void OnActionTick(IExecutableAction<TCategory> action, int deltaTicks);

    /// <summary>アクション終了時。</summary>
    void OnActionEnd(IExecutableAction<TCategory> action);
}
