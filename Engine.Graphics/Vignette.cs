using System;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class Vignette : PostProcessingEffect
{
    private Shader? _shader;
    private uint _framebuffer;
    private uint _texture;
    private int _width;
    private int _height;
    private PostProcessingSettings _settings;

    public uint Texture => _texture;

    public Vignette(int width, int height, PostProcessingSettings settings)
    {
        _width = width;
        _height = height;
        _settings = settings;
        CreateFramebuffer();
        CreateShader();
    }

    private void CreateFramebuffer()
    {
        _framebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

        _texture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, 0);

        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception("Vignette framebuffer is not complete!");
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void CreateShader()
    {
        string vertexShader = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main() { TexCoord = aTexCoord; gl_Position = vec4(aPosition, 0.0, 1.0); }";

        string fragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform float uIntensity;
uniform float uRadius;
uniform float uSmoothness;
void main() {
    vec4 color = texture(uTexture, TexCoord);
    vec2 center = vec2(0.5, 0.5);
    float dist = distance(TexCoord, center);
    float vignette = smoothstep(uRadius, uRadius - uSmoothness, dist);
    vignette = 1.0 - (1.0 - vignette) * uIntensity;
    FragColor = color * vignette;
}";

        _shader = new Shader(vertexShader, fragmentShader);
    }

    public override void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height)
    {
        if (!Enabled || _shader == null || !_settings.VignetteEnabled)
            return;

        if (_width != width || _height != height)
        {
            Resize(width, height);
        }

        if (targetFramebuffer > 0)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFramebuffer);
        }
        else
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        }
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        _shader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _shader.SetInt("uTexture", 0);
        _shader.SetFloat("uIntensity", _settings.VignetteIntensity);
        _shader.SetFloat("uRadius", _settings.VignetteRadius);
        _shader.SetFloat("uSmoothness", _settings.VignetteSmoothness);

        RenderQuad();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void RenderQuad()
    {
        float[] quadVertices = {
            -1.0f, -1.0f,  0.0f, 0.0f,
             1.0f, -1.0f,  1.0f, 0.0f,
             1.0f,  1.0f,  1.0f, 1.0f,
            -1.0f,  1.0f,  0.0f, 1.0f
        };

        ushort[] quadIndices = { 0, 1, 2, 2, 3, 0 };

        uint vao = (uint)GL.GenVertexArray();
        uint vbo = (uint)GL.GenBuffer();
        uint ebo = (uint)GL.GenBuffer();

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        unsafe
        {
            fixed (float* ptr = quadVertices)
            {
                GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), (IntPtr)ptr, BufferUsageHint.StaticDraw);
            }
        }

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        unsafe
        {
            fixed (ushort* ptr = quadIndices)
            {
                GL.BufferData(BufferTarget.ElementArrayBuffer, quadIndices.Length * sizeof(ushort), (IntPtr)ptr, BufferUsageHint.StaticDraw);
            }
        }

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedShort, 0);

        GL.BindVertexArray(0);
        GL.DeleteVertexArray(vao);
        GL.DeleteBuffer(vbo);
        GL.DeleteBuffer(ebo);
    }

    public void Resize(int width, int height)
    {
        if (_width == width && _height == height)
            return;

        _width = width;
        _height = height;

        GL.DeleteTexture(_texture);
        GL.DeleteFramebuffer(_framebuffer);
        CreateFramebuffer();
    }

    protected override void DisposeEffect(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteFramebuffer(_framebuffer);
            GL.DeleteTexture(_texture);
            _shader?.Dispose();
        }
    }
}

