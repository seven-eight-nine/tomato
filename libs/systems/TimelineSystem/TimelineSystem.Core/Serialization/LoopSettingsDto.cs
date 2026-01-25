namespace Tomato.TimelineSystem;

public record LoopSettingsDto
{
    public bool Enabled { get; init; }
    public int StartFrame { get; init; }
    public int EndFrame { get; init; }

    public static LoopSettingsDto FromLoopSettings(LoopSettings settings)
    {
        return new LoopSettingsDto
        {
            Enabled = settings.Enabled,
            StartFrame = settings.StartFrame,
            EndFrame = settings.EndFrame
        };
    }

    public LoopSettings ToLoopSettings()
    {
        if (!Enabled) return LoopSettings.None;
        return LoopSettings.Create(StartFrame, EndFrame);
    }
}
