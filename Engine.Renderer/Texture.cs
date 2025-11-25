using System;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;

namespace Engine.Renderer;

public class Texture : Disposable
{
    private readonly int _textureID;
    private readonly int _width;
    private readonly int _height;

    public int TextureID => _textureID;
    public int Width => _width;
    public int Height => _height;

    public Texture(int width, int height, IntPtr data, PixelInternalFormat internalFormat = PixelInternalFormat.Rgba, PixelFormat format = PixelFormat.Rgba)
    {
        _width = width;
        _height = height;

        _textureID = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _textureID);
        GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, format, PixelType.UnsignedByte, data);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Bind(int unit = 0)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(TextureTarget.Texture2D, _textureID);
    }

    public void Unbind()
    {
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteTexture(_textureID);
        }
    }
}

