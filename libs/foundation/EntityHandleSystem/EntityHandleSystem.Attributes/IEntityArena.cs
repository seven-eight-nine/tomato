namespace Tomato.EntityHandleSystem
{
    /// <summary>
    /// Arenaの共通インターフェース。
    /// VoidHandleからの有効性チェックに使用します。
    /// </summary>
    public interface IEntityArena
    {
        /// <summary>
        /// 指定したインデックスと世代番号のハンドルが有効かどうかを返します。
        /// </summary>
        /// <param name="index">エンティティのインデックス</param>
        /// <param name="generation">世代番号</param>
        /// <returns>有効な場合true</returns>
        bool IsValid(int index, int generation);
    }
}
