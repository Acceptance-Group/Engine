using System;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;

namespace Engine.Renderer;

public class ShadowMap : Disposable
{
    private readonly uint _framebuffer;
    private readonly uint _depthTexture;
    private readonly int _resolution;

    public uint DepthTexture => _depthTexture;
    public int Resolution => _resolution;

    public ShadowMap(int resolution = 2048)
    {
        _resolution = resolution;

        _framebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

        _depthTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, resolution, resolution, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        float[] borderColor = { 1.0f, 1.0f, 1.0f, 1.0f };
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, borderColor);

        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTexture, 0);
        GL.DrawBuffer(DrawBufferMode.None);
        GL.ReadBuffer(ReadBufferMode.None);

        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception("Shadow map framebuffer is not complete!");
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Bind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, _resolution, _resolution);
        GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    public void Unbind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void BindTexture(int unit = 0)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteFramebuffer(_framebuffer);
            GL.DeleteTexture(_depthTexture);
        }
    }
}

