using System;
using Tomato.ActionSelector;

namespace Tomato.ActionExecutionSystem;

/// <summary>
/// 標準的な実行中アクション実装。
/// ActionDefinitionに基づいてアクションの状態を管理する。
/// </summary>
public sealed class StandardExecutableAction<TCategory> : IExecutableAction<TCategory>
    where TCategory : struct, Enum
{
    private readonly ActionDefinition<TCategory> _definition;
    private readonly IActionJudgment<TCategory, InputState, GameState>[] _transitionTargets;

    private int _elapsedTicks;

    public StandardExecutableAction(
        ActionDefinition<TCategory> definition,
        IActionJudgment<TCategory, InputState, GameState>[]? transitionTargets = null)
    {
        _definition = definition;
        _transitionTargets = transitionTargets ?? Array.Empty<IActionJudgment<TCategory, InputState, GameState>>();
    }

    public string ActionId => _definition.ActionId;
    public string Label => _definition.ActionId;
    public TCategory Category => _definition.Category;
    public int ElapsedTicks => _elapsedTicks;

    public bool IsComplete => _elapsedTicks >= _definition.TotalFrames;

    public bool CanCancel => _definition.CancelWindow.Contains(_elapsedTicks);

    public IMotionData? MotionData => _definition.MotionData;

    public void OnEnter()
    {
        _elapsedTicks = 0;
    }

    public void OnExit()
    {
        // 終了時の処理（必要に応じてオーバーライド）
    }

    public void Tick(int deltaTicks)
    {
        _elapsedTicks += deltaTicks;
    }

    public ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>> GetTransitionableJudgments()
    {
        return CanCancel ? _transitionTargets : ReadOnlySpan<IActionJudgment<TCategory, InputState, GameState>>.Empty;
    }
}
