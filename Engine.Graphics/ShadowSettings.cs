namespace Engine.Graphics;

public enum ShadowQuality
{
    Low = 0,
    Medium = 1,
    High = 2,
    Ultra = 3,
    Cinematic = 4
}

public class ShadowSettings
{
    public bool Enabled { get; set; } = true;
    public int ShadowMapResolution { get; set; } = 4096;
    public float DepthBias { get; set; } = 0.005f;
    public float NormalBias { get; set; } = 1.0f;
    public float ShadowDistance { get; set; } = 500.0f;
    public float ShadowOpacity { get; set; } = 0.8f;
    
    public bool SoftShadows { get; set; } = true;
    public ShadowQuality Quality { get; set; } = ShadowQuality.Ultra;
    
    public bool UseCascadedShadowMaps { get; set; } = true;
    public int CascadeCount { get; set; } = 8;
    public float[] CascadeSplits { get; set; } = new float[] { 0.05f, 0.15f, 0.25f, 0.30f, 0.35f, 0.50f, 0.75f, 1.0f };
    public float CascadeBlendArea { get; set; } = 0.05f;
}

