namespace Tomato.StatusEffectSystem
{
    /// <summary>時間をリフレッシュ（新しい時間で上書き）</summary>
    public sealed class RefreshDurationBehavior : IDurationBehavior
    {
        public static readonly RefreshDurationBehavior Instance = new();
        private RefreshDurationBehavior() { }

        public GameTick CalculateNewExpiry(in DurationUpdateContext context)
        {
            if (context.IncomingDuration.IsInfinite)
                return GameTick.MaxValue;
            return context.CurrentTick + context.IncomingDuration;
        }
    }

    /// <summary>時間を延長</summary>
    public sealed class ExtendDurationBehavior : IDurationBehavior
    {
        public static readonly ExtendDurationBehavior Instance = new();
        private ExtendDurationBehavior() { }

        public GameTick CalculateNewExpiry(in DurationUpdateContext context)
        {
            if (context.ExistingInstance.ExpiresAt == GameTick.MaxValue)
                return GameTick.MaxValue;
            if (context.IncomingDuration.IsInfinite)
                return GameTick.MaxValue;
            return context.ExistingInstance.ExpiresAt + context.IncomingDuration;
        }
    }

    /// <summary>長い方を採用</summary>
    public sealed class LongestDurationBehavior : IDurationBehavior
    {
        public static readonly LongestDurationBehavior Instance = new();
        private LongestDurationBehavior() { }

        public GameTick CalculateNewExpiry(in DurationUpdateContext context)
        {
            var newExpiry = context.CurrentTick + context.IncomingDuration;
            return newExpiry > context.ExistingInstance.ExpiresAt
                ? newExpiry
                : context.ExistingInstance.ExpiresAt;
        }
    }

    /// <summary>時間を変更しない</summary>
    public sealed class KeepDurationBehavior : IDurationBehavior
    {
        public static readonly KeepDurationBehavior Instance = new();
        private KeepDurationBehavior() { }

        public GameTick CalculateNewExpiry(in DurationUpdateContext context)
            => context.ExistingInstance.ExpiresAt;
    }
}
