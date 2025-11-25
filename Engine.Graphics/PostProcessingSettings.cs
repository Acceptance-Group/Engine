namespace Engine.Graphics;

public class PostProcessingSettings
{
    public bool MotionBlurEnabled { get; set; } = false;
    public float MotionBlurIntensity { get; set; } = 0.4f;
    public float MotionBlurShutterAngle { get; set; } = 180.0f;
    public int MotionBlurSampleCount { get; set; } = 12;
    public float MotionBlurMaxSampleDistance { get; set; } = 0.8f;
    
    public bool BloomEnabled { get; set; } = false;
    public float BloomIntensity { get; set; } = 1.2f;
    public float BloomThreshold { get; set; } = 0.8f;
    public float BloomSoftKnee { get; set; } = 0.5f;
    public float BloomDiffusion { get; set; } = 6.0f;
    public float BloomScatter { get; set; } = 0.75f;
    public bool BloomHighQuality { get; set; } = true;
    
    public bool VignetteEnabled { get; set; } = false;
    public float VignetteIntensity { get; set; } = 0.5f;
    public float VignetteRadius { get; set; } = 0.75f;
    public float VignetteSmoothness { get; set; } = 0.5f;
}


