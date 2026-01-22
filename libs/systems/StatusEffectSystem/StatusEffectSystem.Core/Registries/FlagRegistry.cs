using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// フラグレジストリ実装
    /// </summary>
    public sealed class FlagRegistry : IFlagRegistry
    {
        private readonly object _lock = new();
        private readonly Dictionary<FlagId, string> _idToName = new();
        private readonly Dictionary<string, FlagId> _nameToId = new();
        private int _nextId;

        public FlagId Register(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            lock (_lock)
            {
                if (_nameToId.ContainsKey(name))
                    throw new ArgumentException($"Flag '{name}' already registered");

                if (_nextId >= 64)
                    throw new InvalidOperationException("Maximum number of flags (64) reached");

                var id = new FlagId(_nextId++);
                _idToName[id] = name;
                _nameToId[name] = id;

                return id;
            }
        }

        public FlagId? GetByName(string name)
        {
            lock (_lock)
            {
                return _nameToId.TryGetValue(name, out var id) ? id : (FlagId?)null;
            }
        }

        public string? GetName(FlagId id)
        {
            lock (_lock)
            {
                return _idToName.TryGetValue(id, out var name) ? name : null;
            }
        }

        public FlagSet CreateSet(params FlagId[] flags)
        {
            var set = FlagSet.Empty;
            foreach (var flag in flags)
                set = set.With(flag);
            return set;
        }
    }
}
