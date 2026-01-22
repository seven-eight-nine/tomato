using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// タグレジストリ実装
    /// </summary>
    public sealed class TagRegistry : ITagRegistry
    {
        private readonly object _lock = new();
        private readonly Dictionary<TagId, TagDefinition> _definitions = new();
        private readonly Dictionary<string, TagId> _nameToId = new();
        private int _nextId;

        public TagId Register(string name, TagId? parentId = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            lock (_lock)
            {
                if (_nameToId.ContainsKey(name))
                    throw new ArgumentException($"Tag '{name}' already registered");

                if (parentId.HasValue && !_definitions.ContainsKey(parentId.Value))
                    throw new ArgumentException($"Parent tag {parentId.Value} not found");

                var id = new TagId(_nextId++);
                var definition = new TagDefinition(id, name, parentId);

                _definitions[id] = definition;
                _nameToId[name] = id;

                return id;
            }
        }

        public TagDefinition? Get(TagId id)
        {
            lock (_lock)
            {
                return _definitions.TryGetValue(id, out var def) ? def : null;
            }
        }

        public TagId? GetByName(string name)
        {
            lock (_lock)
            {
                return _nameToId.TryGetValue(name, out var id) ? id : (TagId?)null;
            }
        }

        public TagSet CreateSet(params TagId[] tags)
        {
            var set = TagSet.Empty;
            foreach (var tag in tags)
                set = set.With(tag);
            return set;
        }

        public bool IsDescendantOf(TagId tag, TagId ancestor)
        {
            lock (_lock)
            {
                var current = tag;
                while (_definitions.TryGetValue(current, out var def))
                {
                    if (!def.ParentId.HasValue)
                        return false;
                    if (def.ParentId.Value == ancestor)
                        return true;
                    current = def.ParentId.Value;
                }
                return false;
            }
        }

        public IEnumerable<TagDefinition> GetAll()
        {
            lock (_lock)
            {
                return new List<TagDefinition>(_definitions.Values);
            }
        }
    }
}
