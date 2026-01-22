namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// スタック源の識別
    /// </summary>
    public interface IStackSourceIdentifier
    {
        bool IsSameSource(EffectInstance existing, ulong incomingSourceId);
    }

    /// <summary>どのソースでも同一扱い</summary>
    public sealed class AnySourceIdentifier : IStackSourceIdentifier
    {
        public static readonly AnySourceIdentifier Instance = new();
        private AnySourceIdentifier() { }

        public bool IsSameSource(EffectInstance existing, ulong incomingSourceId) => true;
    }

    /// <summary>ソースごとに独立</summary>
    public sealed class PerSourceIdentifier : IStackSourceIdentifier
    {
        public static readonly PerSourceIdentifier Instance = new();
        private PerSourceIdentifier() { }

        public bool IsSameSource(EffectInstance existing, ulong incomingSourceId)
            => existing.SourceId == incomingSourceId;
    }
}
