using System;

namespace Tomato.TimelineSystem;

public interface IBlendCalculator
{
    void CalculateWeights(Span<OverlapInfo> overlaps);
}
