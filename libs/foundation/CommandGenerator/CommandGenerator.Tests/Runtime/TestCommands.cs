using System;
using Tomato.CommandGenerator;
using Tomato.EntityHandleSystem;

namespace Tomato.CommandGenerator.Tests;

/// <summary>
/// テスト用コマンド（Priority = 0）
/// </summary>
[Command<MessageHandlerQueue>(Priority = 0)]
public partial class TestCommand
{
    public Action? OnExecute;

    public void Execute()
    {
        OnExecute?.Invoke();
    }
}

/// <summary>
/// 高優先度テストコマンド（Priority = 100）
/// </summary>
[Command<MessageHandlerQueue>(Priority = 100)]
public partial class HighPriorityTestCommand
{
    public Action? OnExecute;

    public void Execute()
    {
        OnExecute?.Invoke();
    }
}

/// <summary>
/// 低優先度テストコマンド（Priority = -10）
/// </summary>
[Command<MessageHandlerQueue>(Priority = -10)]
public partial class LowPriorityTestCommand
{
    public Action? OnExecute;

    public void Execute()
    {
        OnExecute?.Invoke();
    }
}

/// <summary>
/// ダメージ風テストコマンド（Priority = 50）
/// </summary>
[Command<MessageHandlerQueue>(Priority = 50)]
public partial class TestDamageCommand
{
    public AnyHandle Target;
    public int Amount;
    public Action<AnyHandle, int>? OnExecute;

    public void Execute()
    {
        OnExecute?.Invoke(Target, Amount);
    }
}

/// <summary>
/// シグナルコマンド（Signal = true）
/// キューに1つしか入らない
/// </summary>
[Command<MessageHandlerQueue>(Signal = true)]
public partial class SignalCommand
{
    public Action? OnExecute;

    public void Execute()
    {
        OnExecute?.Invoke();
    }
}

/// <summary>
/// シグナルコマンド（Signal = true, Priority = 100）
/// 優先度付きシグナル
/// </summary>
[Command<MessageHandlerQueue>(Signal = true, Priority = 100)]
public partial class HighPrioritySignalCommand
{
    public Action? OnExecute;

    public void Execute()
    {
        OnExecute?.Invoke();
    }
}
