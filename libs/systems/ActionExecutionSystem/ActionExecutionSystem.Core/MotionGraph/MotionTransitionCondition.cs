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
    /// 指定フレーム以上経過したかをチェックする遷移条件を生成。
    /// </summary>
    public static Func<MotionContext, bool> AfterFrame(int frame)
    {
        return ctx => ctx.ElapsedFrames >= frame;
    }

    /// <summary>
    /// 指定フレーム範囲内かをチェックする遷移条件を生成。
    /// </summary>
    public static Func<MotionContext, bool> InFrameRange(int startFrame, int endFrame)
    {
        return ctx => ctx.ElapsedFrames >= startFrame && ctx.ElapsedFrames <= endFrame;
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
