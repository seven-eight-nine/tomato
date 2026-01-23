using Tomato.CharacterSpawnSystem;

namespace Tomato.GameLoop.Spawn;

/// <summary>
/// CharacterSpawnControllerのスポーン完了を通知するインターフェース。
/// CharacterSpawnSystemと他システムの橋渡し役。
/// </summary>
public interface ISpawnCompletionHandler
{
    /// <summary>
    /// キャラクターがアクティブになった時に呼ばれる。
    /// </summary>
    /// <param name="controller">CharacterSpawnController</param>
    void OnCharacterActivated(CharacterSpawnController controller);

    /// <summary>
    /// キャラクターが非アクティブになった時に呼ばれる。
    /// </summary>
    /// <param name="controller">CharacterSpawnController</param>
    void OnCharacterDeactivated(CharacterSpawnController controller);

    /// <summary>
    /// キャラクターが完全に削除された時に呼ばれる。
    /// </summary>
    /// <param name="characterId">キャラクターID</param>
    void OnCharacterRemoved(string characterId);
}
