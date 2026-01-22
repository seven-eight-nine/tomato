using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 排他グループレジストリインターフェース
    /// </summary>
    public interface IGroupRegistry
    {
        /// <summary>グループを登録</summary>
        GroupId Register(string name);

        /// <summary>名前からグループIDを取得</summary>
        GroupId? GetByName(string name);

        /// <summary>グループ名を取得</summary>
        string? GetName(GroupId id);
    }
}
