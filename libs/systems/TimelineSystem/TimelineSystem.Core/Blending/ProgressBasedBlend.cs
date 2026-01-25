using System;

namespace Tomato.TimelineSystem;

public sealed class ProgressBasedBlend : IBlendCalculator
{
    public static readonly ProgressBasedBlend Instance = new();

    private ProgressBasedBlend() { }

    public void CalculateWeights(Span<OverlapInfo> overlaps)
    {
        if (overlaps.Length == 0) return;

        if (overlaps.Length == 1)
        {
            overlaps[0] = new OverlapInfo(overlaps[0].Clip, overlaps[0].Progress, 1.0f);
            return;
        }

        float totalProgress = 0f;
        for (int i = 0; i < overlaps.Length; i++)
        {
            totalProgress += overlaps[i].Progress;
        }

        if (totalProgress <= 0f)
        {
            float equalWeight = 1.0f / overlaps.Length;
            for (int i = 0; i < overlaps.Length; i++)
            {
                overlaps[i] = new OverlapInfo(overlaps[i].Clip, overlaps[i].Progress, equalWeight);
            }
            return;
        }

        for (int i = 0; i < overlaps.Length; i++)
        {
            float weight = overlaps[i].Progress / totalProgress;
            overlaps[i] = new OverlapInfo(overlaps[i].Clip, overlaps[i].Progress, weight);
        }
    }
}
