using Tomato.EntityHandleSystem;
using Tomato.ActionSelector;

namespace Tomato.EntitySystem.Providers;

/// <summary>
/// Entity用の入力を提供するインターフェース。
/// </summary>
public interface IInputProvider
{
    /// <summary>
    /// EntityのInputStateを取得する。
    /// </summary>
    /// <param name="handle">EntityのAnyHandle</param>
    /// <returns>InputState</returns>
    InputState GetInputState(AnyHandle handle);
}
