using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 効果定義レジストリインターフェース
    /// </summary>
    public interface IEffectRegistry
    {
        /// <summary>効果を登録</summary>
        EffectId Register(string internalName, Action<StatusEffectDefinitionBuilder> configure);

        /// <summary>効果定義を取得</summary>
        StatusEffectDefinition? Get(EffectId id);

        /// <summary>名前から効果IDを取得</summary>
        EffectId? GetByName(string internalName);

        /// <summary>効果が登録されているか</summary>
        bool Contains(EffectId id);

        /// <summary>全効果定義を取得</summary>
        IEnumerable<StatusEffectDefinition> GetAll();
    }
}
