using System;
using System.Collections.Generic;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 効果定義レジストリ実装
    /// </summary>
    public sealed class EffectRegistry : IEffectRegistry
    {
        private readonly object _lock = new();
        private readonly Dictionary<EffectId, StatusEffectDefinition> _definitions = new();
        private readonly Dictionary<string, EffectId> _nameToId = new();
        private int _nextId;

        public EffectId Register(string internalName, Action<StatusEffectDefinitionBuilder> configure)
        {
            if (string.IsNullOrEmpty(internalName))
                throw new ArgumentNullException(nameof(internalName));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            lock (_lock)
            {
                if (_nameToId.ContainsKey(internalName))
                    throw new ArgumentException($"Effect '{internalName}' already registered");

                var id = new EffectId(_nextId++);
                var builder = new StatusEffectDefinitionBuilder(id, internalName);
                configure(builder);
                var definition = builder.Build();

                _definitions[id] = definition;
                _nameToId[internalName] = id;

                return id;
            }
        }

        public StatusEffectDefinition? Get(EffectId id)
        {
            lock (_lock)
            {
                return _definitions.TryGetValue(id, out var def) ? def : null;
            }
        }

        public EffectId? GetByName(string internalName)
        {
            lock (_lock)
            {
                return _nameToId.TryGetValue(internalName, out var id) ? id : (EffectId?)null;
            }
        }

        public bool Contains(EffectId id)
        {
            lock (_lock)
            {
                return _definitions.ContainsKey(id);
            }
        }

        public IEnumerable<StatusEffectDefinition> GetAll()
        {
            lock (_lock)
            {
                return new List<StatusEffectDefinition>(_definitions.Values);
            }
        }
    }
}
