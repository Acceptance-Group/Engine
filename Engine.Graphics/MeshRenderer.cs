using Engine.Core;
using Engine.Math;
using Engine.Renderer;

namespace Engine.Graphics;

public class MeshRenderer : Component
{
    private Mesh? _mesh;
    private Material? _material;
    private Shader? _defaultShader;

    public Mesh? Mesh
    {
        get => _mesh;
        set => _mesh = value;
    }

    public Material? Material
    {
        get => _material;
        set => _material = value;
    }

    public void Render(Matrix4 viewProjection)
    {
        if (_mesh == null || Transform == null)
            return;

        var material = _material;
        if (material == null)
        {
            if (_defaultShader == null)
                return;
            material = new Material 
            { 
                Shader = _defaultShader,
                Color = new Engine.Math.Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            };
        }

        material.Apply();

        if (material.Shader != null)
        {
            Matrix4 modelMatrix = Transform.WorldMatrix;
            Matrix4 mvp = viewProjection * modelMatrix;

            material.Shader.SetMatrix4("uModel", modelMatrix);
            material.Shader.SetMatrix4("uMVP", mvp);
        }

        _mesh.Draw();
    }

    public void SetDefaultShader(Shader shader)
    {
        _defaultShader = shader;
    }
}

