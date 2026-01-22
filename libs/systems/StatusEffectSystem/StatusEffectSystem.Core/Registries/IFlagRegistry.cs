using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// フラグレジストリインターフェース
    /// </summary>
    public interface IFlagRegistry
    {
        /// <summary>フラグを登録（最大64個）</summary>
        FlagId Register(string name);

        /// <summary>名前からフラグIDを取得</summary>
        FlagId? GetByName(string name);

        /// <summary>フラグ名を取得</summary>
        string? GetName(FlagId id);

        /// <summary>FlagSetを作成</summary>
        FlagSet CreateSet(params FlagId[] flags);
    }
}
