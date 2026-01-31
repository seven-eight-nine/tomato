using System;

namespace Tomato.ActionExecutionSystem.MotionGraph;

/// <summary>
/// モーション遷移条件を生成するユーティリティ。
/// </summary>
public static class MotionTransitionCondition
{
    /// <summary>
    /// モーションが完了したかをチェックする遷移条件を生成。
    /// </summary>
    public static Func<MotionContext, bool> IsComplete()
    {
        return ctx =>
        {
            if (ctx.CurrentMotionState == null)
                return true;

            return ctx.CurrentMotionState.IsComplete(ctx);
        };
    }

    /// <summary>
    /// 常に遷移可能な条件を生成。
    /// </summary>
    public static Func<MotionContext, bool> Always()
    {
        return _ => true;
    }

    /// <summary>
    /// 遷移不可能な条件を生成。
    /// </summary>
    public static Func<MotionContext, bool> Never()
    {
        return _ => false;
    }

    /// <summary>
    /// 指定tick以上経過したかをチェックする遷移条件を生成。
    /// </summary>
    public static Func<MotionContext, bool> AfterTick(int tick)
    {
        return ctx => ctx.ElapsedTicks >= tick;
    }

    /// <summary>
    /// 指定tick範囲内かをチェックする遷移条件を生成。
    /// </summary>
    public static Func<MotionContext, bool> InTickRange(int startTick, int endTick)
    {
        return ctx => ctx.ElapsedTicks >= startTick && ctx.ElapsedTicks <= endTick;
    }

    /// <summary>
    /// 複数の条件をANDで結合。
    /// </summary>
    public static Func<MotionContext, bool> And(params Func<MotionContext, bool>[] conditions)
    {
        return ctx =>
        {
            for (int i = 0; i < conditions.Length; i++)
            {
                if (!conditions[i](ctx))
                    return false;
            }
            return true;
        };
    }

    /// <summary>
    /// 複数の条件をORで結合。
    /// </summary>
    public static Func<MotionContext, bool> Or(params Func<MotionContext, bool>[] conditions)
    {
        return ctx =>
        {
            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i](ctx))
                    return true;
            }
            return false;
        };
    }
}
