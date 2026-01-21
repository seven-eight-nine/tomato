namespace Tomato.HandleSystem;

/// <summary>
/// 汎用Arenaの共通インターフェース。
/// 型消去されたハンドルからの有効性チェックに使用します。
/// </summary>
public interface IArena
{
    /// <summary>
    /// 指定したインデックスと世代番号のハンドルが有効かどうかを返します。
    /// </summary>
    /// <param name="index">オブジェクトのインデックス</param>
    /// <param name="generation">世代番号</param>
    /// <returns>有効な場合true</returns>
    bool IsValid(int index, int generation);
}
