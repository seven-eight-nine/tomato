using System;
using System.Collections.Generic;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// Entity単位のアクション状態機械。
/// </summary>
public sealed class ActionStateMachine<TCategory> where TCategory : struct, Enum
{
    private readonly Dictionary<TCategory, IExecutableAction<TCategory>?> _currentActions;
    private readonly Dictionary<TCategory, IActionExecutor<TCategory>> _executors;

    public ActionStateMachine()
    {
        _currentActions = new Dictionary<TCategory, IExecutableAction<TCategory>?>();
        _executors = new Dictionary<TCategory, IActionExecutor<TCategory>>();

        // 全カテゴリを初期化
        foreach (TCategory category in Enum.GetValues(typeof(TCategory)))
        {
            _currentActions[category] = null;
        }
    }

    /// <summary>
    /// カテゴリごとのエグゼキュータを登録する。
    /// </summary>
    public void RegisterExecutor(TCategory category, IActionExecutor<TCategory> executor)
    {
        _executors[category] = executor;
    }

    /// <summary>
    /// 指定カテゴリの現在のアクションを取得する。
    /// </summary>
    public IExecutableAction<TCategory>? GetCurrentAction(TCategory category)
    {
        return _currentActions.TryGetValue(category, out var action) ? action : null;
    }

    /// <summary>
    /// アクションを開始する。
    /// </summary>
    public void StartAction(TCategory category, IExecutableAction<TCategory> action)
    {
        var current = _currentActions[category];

        // 現在のアクションを終了
        if (current != null)
        {
            current.OnExit();
            if (_executors.TryGetValue(category, out var executor))
            {
                executor.OnActionEnd(current);
            }
        }

        // 新しいアクションを開始
        _currentActions[category] = action;
        action.OnEnter();

        if (_executors.TryGetValue(category, out var exec))
        {
            exec.OnActionStart(action);
        }
    }

    /// <summary>
    /// 全アクションを更新する。
    /// </summary>
    public void Update(float deltaTime)
    {
        // 完了したアクションを収集（Dictionaryを反復中に変更できないため）
        var completedCategories = new List<TCategory>();

        foreach (var (category, action) in _currentActions)
        {
            if (action == null) continue;

            action.Update(deltaTime);

            if (_executors.TryGetValue(category, out var executor))
            {
                executor.OnActionUpdate(action, deltaTime);
            }

            // アクション完了チェック
            if (action.IsComplete)
            {
                completedCategories.Add(category);
            }
        }

        // 完了したアクションを処理
        foreach (var category in completedCategories)
        {
            var action = _currentActions[category];
            if (action != null)
            {
                action.OnExit();
                if (_executors.TryGetValue(category, out var executor))
                {
                    executor.OnActionEnd(action);
                }
                _currentActions[category] = null;
            }
        }
    }

    /// <summary>
    /// 指定カテゴリでアクションが実行中か。
    /// </summary>
    public bool IsRunning(TCategory category)
    {
        return _currentActions.TryGetValue(category, out var action) && action != null;
    }

    /// <summary>
    /// 指定カテゴリのアクションがキャンセル可能か。
    /// </summary>
    public bool CanCancel(TCategory category)
    {
        return _currentActions.TryGetValue(category, out var action) && action?.CanCancel == true;
    }
}
