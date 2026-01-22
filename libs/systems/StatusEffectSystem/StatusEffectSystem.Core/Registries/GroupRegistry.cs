using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 排他グループレジストリ実装
    /// </summary>
    public sealed class GroupRegistry : IGroupRegistry
    {
        private readonly object _lock = new();
        private readonly Dictionary<GroupId, string> _idToName = new();
        private readonly Dictionary<string, GroupId> _nameToId = new();
        private int _nextId = 1; // 0はNone用に予約

        public GroupId Register(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            lock (_lock)
            {
                if (_nameToId.ContainsKey(name))
                    throw new ArgumentException($"Group '{name}' already registered");

                var id = new GroupId(_nextId++);
                _idToName[id] = name;
                _nameToId[name] = id;

                return id;
            }
        }

        public GroupId? GetByName(string name)
        {
            lock (_lock)
            {
                return _nameToId.TryGetValue(name, out var id) ? id : (GroupId?)null;
            }
        }

        public string? GetName(GroupId id)
        {
            lock (_lock)
            {
                return _idToName.TryGetValue(id, out var name) ? name : null;
            }
        }
    }
}
