namespace Engine.Graphics;

public enum AntiAliasingMode
{
    None,
    MSAA2x,
    MSAA4x,
    MSAA8x,
    FXAA,
    SMAA
}

public enum AntiAliasingQuality
{
    Low,
    Medium,
    High,
    Ultra
}

public class AntiAliasingSettings
{
    public AntiAliasingMode Mode { get; set; } = AntiAliasingMode.FXAA;
    public AntiAliasingQuality Quality { get; set; } = AntiAliasingQuality.Ultra;
    public bool Enabled => Mode != AntiAliasingMode.None;
}

