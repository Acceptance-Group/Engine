using Engine.Core;
using Engine.Math;
using Engine.Renderer;

namespace Engine.Graphics;

public class Material : Disposable
{
    private Shader? _shader;
    private Texture? _diffuseTexture;
    private Vector4 _color = Vector4.One;

    public Shader? Shader
    {
        get => _shader;
        set => _shader = value;
    }

    public Texture? DiffuseTexture
    {
        get => _diffuseTexture;
        set => _diffuseTexture = value;
    }

    public Vector4 Color
    {
        get => _color;
        set => _color = value;
    }

    public void Apply()
    {
        if (_shader == null)
            return;

        _shader.Use();

        if (_diffuseTexture != null)
        {
            _diffuseTexture.Bind(0);
            _shader.SetInt("uTexture", 0);
        }

        _shader.SetVector4("uColor", _color);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shader?.Dispose();
            _diffuseTexture?.Dispose();
        }
    }
}

