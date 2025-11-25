using System;
using Engine.Core;
using Engine.Renderer;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class MotionBlur : PostProcessingEffect
{
    private Shader? _blendShader;
    private Shader? _copyShader;
    private uint _outputFramebuffer;
    private uint _outputTexture;
    private uint _historyFramebuffer;
    private uint _historyTexture;
    private uint _quadVao;
    private uint _quadVbo;
    private uint _quadEbo;
    private int _width;
    private int _height;
    private bool _hasHistory;
    private bool _isActive;
    private readonly PostProcessingSettings _settings;

    public uint Texture => _outputTexture;

    public MotionBlur(int width, int height, PostProcessingSettings settings)
    {
        _width = width;
        _height = height;
        _settings = settings;
        _isActive = settings.MotionBlurEnabled;
        Enabled = _isActive;
        CreateFramebuffers();
        CreateShaders();
        CreateFullscreenQuad();
    }

    public void SetActive(bool active)
    {
        if (_isActive == active)
            return;

        _isActive = active;
        Enabled = active;

        if (!active)
        {
            _hasHistory = false;
        }
    }

    private void CreateFramebuffers()
    {
        _outputFramebuffer = CreateFramebufferWithTexture(out _outputTexture);
        _historyFramebuffer = CreateFramebufferWithTexture(out _historyTexture);
        _hasHistory = false;
    }

    private uint CreateFramebufferWithTexture(out uint texture)
    {
        uint framebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);

        texture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"Motion blur framebuffer incomplete: {status}");
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return framebuffer;
    }

    private void CreateShaders()
    {
        const string vertexShader = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main() { TexCoord = aTexCoord; gl_Position = vec4(aPosition, 0.0, 1.0); }";

        const string blendFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uCurrentTexture;
uniform sampler2D uHistoryTexture;
uniform float uBlendFactor;
uniform int uHasHistory;
void main()
{
    vec4 currentColor = texture(uCurrentTexture, TexCoord);
    vec4 historyColor = currentColor;
    if (uHasHistory == 1)
    {
        historyColor = texture(uHistoryTexture, TexCoord);
    }
    float blend = clamp(uBlendFactor, 0.0, 0.95);
    FragColor = mix(currentColor, historyColor, blend);
}";

        const string copyFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
void main()
{
    FragColor = texture(uTexture, TexCoord);
}";

        _blendShader = new Shader(vertexShader, blendFragmentShader);
        _copyShader = new Shader(vertexShader, copyFragmentShader);
    }

    private void CreateFullscreenQuad()
    {
        float[] vertices =
        {
            -1.0f, -1.0f, 0.0f, 0.0f,
             1.0f, -1.0f, 1.0f, 0.0f,
             1.0f,  1.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, 0.0f, 1.0f
        };

        ushort[] indices = { 0, 1, 2, 2, 3, 0 };

        _quadVao = (uint)GL.GenVertexArray();
        _quadVbo = (uint)GL.GenBuffer();
        _quadEbo = (uint)GL.GenBuffer();

        GL.BindVertexArray(_quadVao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
        unsafe
        {
            fixed (float* ptr = vertices)
            {
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), (IntPtr)ptr, BufferUsageHint.StaticDraw);
            }
        }

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _quadEbo);
        unsafe
        {
            fixed (ushort* ptr = indices)
            {
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(ushort), (IntPtr)ptr, BufferUsageHint.StaticDraw);
            }
        }

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    public override void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height)
    {
        if (!Enabled || _blendShader == null || _copyShader == null || !_settings.MotionBlurEnabled)
            return;

        if (_width != width || _height != height)
        {
            Resize(width, height);
        }

        EnsureHistoryInitialized(sourceTexture);

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _blendShader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _blendShader.SetInt("uCurrentTexture", 0);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _historyTexture);
        _blendShader.SetInt("uHistoryTexture", 1);
        float blendFactor = System.Math.Clamp(_settings.MotionBlurIntensity, 0.0f, 0.95f);
        _blendShader.SetFloat("uBlendFactor", blendFactor);
        _blendShader.SetInt("uHasHistory", _hasHistory ? 1 : 0);

        RenderQuad();

        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _outputFramebuffer);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _historyFramebuffer);
        GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

        if (targetFramebuffer > 0)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _outputFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targetFramebuffer);
            GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }
        else
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        _hasHistory = true;
    }

    private void EnsureHistoryInitialized(uint sourceTexture)
    {
        if (_hasHistory)
            return;

        CopyToFramebuffer(sourceTexture, _historyFramebuffer);
        CopyToFramebuffer(sourceTexture, _outputFramebuffer);
        _hasHistory = true;
    }

    private void CopyToFramebuffer(uint sourceTexture, uint targetFramebuffer)
    {
        if (_copyShader == null)
            return;

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFramebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _copyShader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _copyShader.SetInt("uTexture", 0);

        RenderQuad();
    }

    private void RenderQuad()
    {
        GL.BindVertexArray(_quadVao);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedShort, 0);
        GL.BindVertexArray(0);
    }

    public void Resize(int width, int height)
    {
        if (_width == width && _height == height)
            return;

        _width = width;
        _height = height;

        GL.DeleteTexture(_outputTexture);
        GL.DeleteTexture(_historyTexture);
        GL.DeleteFramebuffer(_outputFramebuffer);
        GL.DeleteFramebuffer(_historyFramebuffer);
        CreateFramebuffers();
    }

    protected override void DisposeEffect(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteFramebuffer(_outputFramebuffer);
            GL.DeleteFramebuffer(_historyFramebuffer);
            GL.DeleteTexture(_outputTexture);
            GL.DeleteTexture(_historyTexture);
            GL.DeleteVertexArray(_quadVao);
            GL.DeleteBuffer(_quadVbo);
            GL.DeleteBuffer(_quadEbo);
            _blendShader?.Dispose();
            _copyShader?.Dispose();
        }
    }
}

