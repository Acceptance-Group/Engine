namespace Engine.Graphics;

public class PostProcessingSettings
{
    public bool MotionBlurEnabled { get; set; } = false;
    public float MotionBlurIntensity { get; set; } = 0.4f;
    
    public bool BloomEnabled { get; set; } = false;
    public float BloomIntensity { get; set; } = 1.2f;
    public float BloomThreshold { get; set; } = 0.65f;
    public float BloomRadius { get; set; } = 6.0f;
    
    public bool VignetteEnabled { get; set; } = false;
    public float VignetteIntensity { get; set; } = 0.5f;
    public float VignetteRadius { get; set; } = 0.75f;
    public float VignetteSmoothness { get; set; } = 0.5f;
}


