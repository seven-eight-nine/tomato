using System;

namespace Tomato.CharacterSpawnSystem;

/// <summary>
/// 状態変更イベント引数
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public CharacterInternalState OldState { get; private set; }
    public CharacterInternalState NewState { get; private set; }

    public StateChangedEventArgs(CharacterInternalState oldState, CharacterInternalState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// 状態変更イベントハンドラ
/// </summary>
public delegate void StateChangedEventHandler(object sender, StateChangedEventArgs e);
