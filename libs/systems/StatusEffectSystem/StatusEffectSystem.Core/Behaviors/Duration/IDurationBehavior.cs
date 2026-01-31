using Tomato.Time;

namespace Tomato.StatusEffectSystem
{
    /// <summary>
    /// 時間更新のコンテキスト
    /// </summary>
    public readonly struct DurationUpdateContext
    {
        public readonly EffectInstance ExistingInstance;
        public readonly TickDuration IncomingDuration;
        public readonly GameTick CurrentTick;

        public DurationUpdateContext(
            EffectInstance existingInstance,
            TickDuration incomingDuration,
            GameTick currentTick)
        {
            ExistingInstance = existingInstance;
            IncomingDuration = incomingDuration;
            CurrentTick = currentTick;
        }
    }

    /// <summary>
    /// 時間更新の振る舞い
    /// </summary>
    public interface IDurationBehavior
    {
        GameTick CalculateNewExpiry(in DurationUpdateContext context);
    }
}
