namespace Tomato.TimelineSystem;

public class LoopSettingsDto
{
    public bool Enabled { get; set; }
    public int StartFrame { get; set; }
    public int EndFrame { get; set; }

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
