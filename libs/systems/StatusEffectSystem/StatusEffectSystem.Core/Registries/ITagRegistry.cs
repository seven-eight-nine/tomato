using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// タグレジストリインターフェース
    /// </summary>
    public interface ITagRegistry
    {
        /// <summary>タグを登録</summary>
        TagId Register(string name, TagId? parentId = null);

        /// <summary>タグ定義を取得</summary>
        TagDefinition? Get(TagId id);

        /// <summary>名前からタグIDを取得</summary>
        TagId? GetByName(string name);

        /// <summary>TagSetを作成</summary>
        TagSet CreateSet(params TagId[] tags);

        /// <summary>タグが祖先の子孫かどうか</summary>
        bool IsDescendantOf(TagId tag, TagId ancestor);

        /// <summary>全タグ定義を取得</summary>
        IEnumerable<TagDefinition> GetAll();
    }
}
