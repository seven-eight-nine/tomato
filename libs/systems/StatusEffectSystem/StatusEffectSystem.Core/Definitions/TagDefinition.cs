using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// タグ定義（マスタデータ）
    /// </summary>
    public sealed class TagDefinition
    {
        public TagId Id { get; }
        public string Name { get; }
        public TagId? ParentId { get; }

        internal TagDefinition(TagId id, string name, TagId? parentId)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ParentId = parentId;
        }
    }
}
