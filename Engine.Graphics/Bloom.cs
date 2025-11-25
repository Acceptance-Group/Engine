using System;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class Bloom : PostProcessingEffect
{
    private Shader? _extractShader;
    private Shader? _blurShader;
    private Shader? _combineShader;
    private uint _extractFramebuffer;
    private uint _extractTexture;
    private uint _blurFramebuffer1;
    private uint _blurTexture1;
    private uint _blurFramebuffer2;
    private uint _blurTexture2;
    private uint _outputFramebuffer;
    private uint _outputTexture;
    private int _width;
    private int _height;
    private PostProcessingSettings _settings;

    public uint Texture => _outputTexture;

    public Bloom(int width, int height, PostProcessingSettings settings)
    {
        _width = width;
        _height = height;
        _settings = settings;
        CreateFramebuffers();
        CreateShaders();
    }

    private void CreateFramebuffers()
    {
        _extractFramebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _extractFramebuffer);
        _extractTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _extractTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _extractTexture, 0);

        _blurFramebuffer1 = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFramebuffer1);
        _blurTexture1 = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _blurTexture1);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _blurTexture1, 0);

        _blurFramebuffer2 = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFramebuffer2);
        _blurTexture2 = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _blurTexture2);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _blurTexture2, 0);

        _outputFramebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        _outputTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _outputTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _outputTexture, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void CreateShaders()
    {
        string vertexShader = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main() { TexCoord = aTexCoord; gl_Position = vec4(aPosition, 0.0, 1.0); }";

        string extractFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform float uThreshold;
uniform float uSoftKnee;
void main() {
    vec3 color = texture(uTexture, TexCoord).rgb;
    float brightness = dot(color, vec3(0.2126, 0.7152, 0.0722));
    float knee = uSoftKnee * 0.5;
    float contribution = smoothstep(uThreshold - knee, uThreshold + knee, brightness);
    FragColor = vec4(color * contribution, 1.0);
}";

        string blurFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform vec2 uDirection;
uniform float uRadius;
void main() {
    vec2 offset = uDirection * uRadius;
    vec4 color = texture(uTexture, TexCoord) * 0.2270270270;
    color += texture(uTexture, TexCoord + offset * 1.0) * 0.1945945946;
    color += texture(uTexture, TexCoord - offset * 1.0) * 0.1945945946;
    color += texture(uTexture, TexCoord + offset * 2.0) * 0.1216216216;
    color += texture(uTexture, TexCoord - offset * 2.0) * 0.1216216216;
    color += texture(uTexture, TexCoord + offset * 3.0) * 0.0540540541;
    color += texture(uTexture, TexCoord - offset * 3.0) * 0.0540540541;
    color += texture(uTexture, TexCoord + offset * 4.0) * 0.0162162162;
    color += texture(uTexture, TexCoord - offset * 4.0) * 0.0162162162;
    FragColor = color;
}";

        string combineFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform sampler2D uBloomTexture;
uniform float uIntensity;
uniform float uScatter;
void main() {
    vec4 original = texture(uTexture, TexCoord);
    vec4 bloom = texture(uBloomTexture, TexCoord);
    vec3 bloomColor = bloom.rgb;
    bloomColor = pow(bloomColor, vec3(1.0 / uScatter));
    FragColor = vec4(original.rgb + bloomColor * uIntensity, original.a);
}";

        _extractShader = new Shader(vertexShader, extractFragmentShader);
        _blurShader = new Shader(vertexShader, blurFragmentShader);
        _combineShader = new Shader(vertexShader, combineFragmentShader);
    }

    public override void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height)
    {
        if (!Enabled || _extractShader == null || _blurShader == null || _combineShader == null || !_settings.BloomEnabled)
            return;

        if (_width != width || _height != height)
        {
            Resize(width, height);
        }

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _extractFramebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _extractShader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _extractShader.SetInt("uTexture", 0);
        _extractShader.SetFloat("uThreshold", _settings.BloomThreshold);
        _extractShader.SetFloat("uSoftKnee", _settings.BloomSoftKnee);
        RenderQuad();

        float radius = _settings.BloomDiffusion / _width;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFramebuffer1);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _blurShader.Use();
        GL.BindTexture(TextureTarget.Texture2D, _extractTexture);
        _blurShader.SetInt("uTexture", 0);
        _blurShader.SetVector2("uDirection", new Vector2(radius, 0.0f));
        _blurShader.SetFloat("uRadius", radius);
        RenderQuad();

        radius = _settings.BloomDiffusion / _height;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blurFramebuffer2);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _blurShader.Use();
        GL.BindTexture(TextureTarget.Texture2D, _blurTexture1);
        _blurShader.SetVector2("uDirection", new Vector2(0.0f, radius));
        _blurShader.SetFloat("uRadius", radius);
        RenderQuad();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _combineShader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _combineShader.SetInt("uTexture", 0);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _blurTexture2);
        _combineShader.SetInt("uBloomTexture", 1);
        _combineShader.SetFloat("uIntensity", _settings.BloomIntensity);
        _combineShader.SetFloat("uScatter", System.Math.Max(0.1f, _settings.BloomScatter));
        RenderQuad();

        if (targetFramebuffer > 0)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _outputFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targetFramebuffer);
            GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        }

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

        GL.DeleteTexture(_extractTexture);
        GL.DeleteTexture(_blurTexture1);
        GL.DeleteTexture(_blurTexture2);
        GL.DeleteTexture(_outputTexture);
        GL.DeleteFramebuffer(_extractFramebuffer);
        GL.DeleteFramebuffer(_blurFramebuffer1);
        GL.DeleteFramebuffer(_blurFramebuffer2);
        GL.DeleteFramebuffer(_outputFramebuffer);
        CreateFramebuffers();
    }

    protected override void DisposeEffect(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteFramebuffer(_extractFramebuffer);
            GL.DeleteFramebuffer(_blurFramebuffer1);
            GL.DeleteFramebuffer(_blurFramebuffer2);
            GL.DeleteFramebuffer(_outputFramebuffer);
            GL.DeleteTexture(_extractTexture);
            GL.DeleteTexture(_blurTexture1);
            GL.DeleteTexture(_blurTexture2);
            GL.DeleteTexture(_outputTexture);
            _extractShader?.Dispose();
            _blurShader?.Dispose();
            _combineShader?.Dispose();
        }
    }
}

