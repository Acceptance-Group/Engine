using System;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class SSAO : PostProcessingEffect
{
    private Shader? _shader;
    private uint _framebuffer;
    private uint _texture;
    private int _width;
    private int _height;
    private readonly PostProcessingSettings _settings;
    private uint _depthTexture;
    private Matrix4 _projection = Matrix4.Identity;
    private Matrix4 _inverseProjection = Matrix4.Identity;

    public uint Texture => _texture;

    public SSAO(int width, int height, PostProcessingSettings settings)
    {
        _width = width;
        _height = height;
        _settings = settings;
        CreateFramebuffer();
        CreateShader();
    }

    public void SetDepthTexture(uint depthTexture)
    {
        _depthTexture = depthTexture;
    }

    public void UpdateCameraData(Matrix4 projection)
    {
        _projection = projection;
        _inverseProjection = projection.Inverse();
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

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"SSAO framebuffer incomplete: {status}");
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void CreateShader()
    {
        const string vertexShader = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main()
{
    TexCoord = aTexCoord;
    gl_Position = vec4(aPosition, 0.0, 1.0);
}";

        const string fragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D uColorTexture;
uniform sampler2D uDepthTexture;
uniform mat4 uProjection;
uniform mat4 uInverseProjection;
uniform float uRadius;
uniform float uBias;
uniform float uPower;

void main()
{
    vec3 color = texture(uColorTexture, TexCoord).rgb;
    float depthCenter = texture(uDepthTexture, TexCoord).r;
    if (depthCenter >= 1.0)
    {
        FragColor = vec4(color, 1.0);
        return;
    }

    vec2 texel = vec2(1.0 / float(textureSize(uDepthTexture, 0).x),
                      1.0 / float(textureSize(uDepthTexture, 0).y));

    vec2 offsets[8];
    offsets[0] = vec2(1, 0);
    offsets[1] = vec2(-1, 0);
    offsets[2] = vec2(0, 1);
    offsets[3] = vec2(0, -1);
    offsets[4] = vec2(1, 1);
    offsets[5] = vec2(-1, 1);
    offsets[6] = vec2(1, -1);
    offsets[7] = vec2(-1, -1);

    float occlusion = 0.0;
    int sampleCount = 8;

    float depthScale = max(depthCenter, 0.15);
    float radiusScale = uRadius / depthScale;

    for (int i = 0; i < sampleCount; i++)
    {
        vec2 dir = normalize(offsets[i]);
        vec2 sampleUv = TexCoord + dir * radiusScale * texel;

        if (sampleUv.x < 0.0 || sampleUv.x > 1.0 || sampleUv.y < 0.0 || sampleUv.y > 1.0)
            continue;

        float sampleDepth = texture(uDepthTexture, sampleUv).r;
        if (sampleDepth >= 1.0)
            continue;

        float diff = depthCenter - sampleDepth - uBias;
        if (diff > 0.0)
        {
            float w = diff / (diff + uBias);
            occlusion += w;
        }
    }

    occlusion = clamp(occlusion / float(sampleCount), 0.0, 1.0);
    float ao = pow(1.0 - occlusion, uPower);
    FragColor = vec4(color * ao, 1.0);
}";

        _shader = new Shader(vertexShader, fragmentShader);
    }

    public override void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height)
    {
        if (!Enabled || _shader == null || _depthTexture == 0 || !_settings.SSAOEnabled)
            return;

        if (_width != width || _height != height)
        {
            Resize(width, height);
        }

        uint target = _framebuffer;
        if (targetFramebuffer > 0)
            target = (uint)targetFramebuffer;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, target);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        _shader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _shader.SetInt("uColorTexture", 0);

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        _shader.SetInt("uDepthTexture", 1);

        _shader.SetMatrix4("uProjection", _projection);
        _shader.SetMatrix4("uInverseProjection", _inverseProjection);
        _shader.SetFloat("uRadius", _settings.SSAORadius);
        _shader.SetFloat("uBias", _settings.SSAOBias);
        _shader.SetFloat("uPower", _settings.SSAOPower);

        RenderQuad();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void RenderQuad()
    {
        float[] quadVertices =
        {
            -1.0f, -1.0f, 0.0f, 0.0f,
             1.0f, -1.0f, 1.0f, 0.0f,
             1.0f,  1.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, 0.0f, 1.0f
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


