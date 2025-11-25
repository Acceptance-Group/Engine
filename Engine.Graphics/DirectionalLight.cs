using Engine.Math;

namespace Engine.Graphics;

public class DirectionalLight
{
    public Vector3 Direction { get; set; } = new Vector3(-0.5f, -1.0f, -0.5f);
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1.0f;
    public bool CastShadows { get; set; } = true;
}

