namespace Engine.Graphics;

public class PathTracingSettings
{
    public bool Enabled { get; set; } = false;
    public int RayDepth { get; set; } = 2;
    public int SamplesPerPixel { get; set; } = 4;
    public int MaxSamples { get; set; } = 1024;
    public bool EnableDirectLight { get; set; } = true;
    public bool EnableShadows { get; set; } = true;
    public bool EnableReflections { get; set; } = true;
}

