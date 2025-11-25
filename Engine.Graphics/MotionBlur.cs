using System;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class MotionBlur : PostProcessingEffect
{
    private Shader? _shader;
    private Shader? _copyShader;
    private uint _outputFramebuffer;
    private uint _outputTexture;
    private uint _quadVao;
    private uint _quadVbo;
    private uint _quadEbo;
    private int _width;
    private int _height;
    private readonly PostProcessingSettings _settings;
    private uint _depthTexture;
    private Matrix4 _currentViewProjection = Matrix4.Identity;
    private Matrix4 _previousViewProjection = Matrix4.Identity;
    private Matrix4 _inverseCurrentViewProjection = Matrix4.Identity;
    private bool _hasCameraHistory;
    private float _deltaTime = 1.0f / 60.0f;
    private bool _isActive;

    public uint Texture => _outputTexture;

    public MotionBlur(int width, int height, PostProcessingSettings settings)
    {
        _width = width;
        _height = height;
        _settings = settings;
        _isActive = settings.MotionBlurEnabled;
        Enabled = _isActive;
        CreateFramebuffer();
        CreateShader();
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
            _hasCameraHistory = false;
        }
    }

    public void SetDepthTexture(uint depthTexture)
    {
        _depthTexture = depthTexture;
    }

    public void UpdateCameraData(Matrix4 viewProjection, float deltaTime)
    {
        _deltaTime = System.MathF.Max(deltaTime, 1e-4f);

        if (!_hasCameraHistory)
        {
            _currentViewProjection = viewProjection;
            _previousViewProjection = viewProjection;
            _inverseCurrentViewProjection = viewProjection.Inverse();
            _hasCameraHistory = true;
            return;
        }

        _previousViewProjection = _currentViewProjection;
        _currentViewProjection = viewProjection;
        _inverseCurrentViewProjection = viewProjection.Inverse();
    }

    private void CreateFramebuffer()
    {
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

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            throw new Exception($"Motion blur framebuffer incomplete: {status}");
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
uniform mat4 uInverseViewProjection;
uniform mat4 uPreviousViewProjection;
uniform float uBlurScale;
uniform float uMaxSampleDistance;
uniform float uDepthThreshold;
uniform int uSampleCount;
uniform int uUseDepth;

vec2 ReconstructMotion(vec2 uv, float depth)
{
    if (depth >= 1.0)
    {
        return vec2(0.0);
    }

    vec4 currentClip = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 worldPos = uInverseViewProjection * currentClip;
    worldPos /= worldPos.w;
    vec4 previousClip = uPreviousViewProjection * worldPos;
    previousClip /= previousClip.w;
    vec2 previousUv = previousClip.xy * 0.5 + 0.5;
    return uv - previousUv;
}

vec2 ReconstructMotionScreenSpace(vec2 uv)
{
    vec4 currentClip = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
    vec4 worldPos = uInverseViewProjection * currentClip;
    worldPos /= worldPos.w;
    vec4 previousClip = uPreviousViewProjection * worldPos;
    previousClip /= previousClip.w;
    vec2 previousUv = previousClip.xy * 0.5 + 0.5;
    return uv - previousUv;
}

vec3 SampleColor(vec2 uv)
{
    vec2 clampedUv = clamp(uv, vec2(0.0), vec2(1.0));
    return texture(uColorTexture, clampedUv).rgb;
}

float SampleDepth(vec2 uv)
{
    vec2 clampedUv = clamp(uv, vec2(0.0), vec2(1.0));
    return texture(uDepthTexture, clampedUv).r;
}

void main()
{
    vec2 uv = TexCoord;
    vec3 baseColor = texture(uColorTexture, uv).rgb;

    vec2 velocity;
    float depth = 0.5;
    
    if (uUseDepth == 1)
    {
        depth = texture(uDepthTexture, uv).r;
        velocity = ReconstructMotion(uv, depth);
    }
    else
    {
        velocity = ReconstructMotionScreenSpace(uv);
    }

    vec2 blurVector = velocity * uBlurScale;
    float blurLength = length(blurVector);

    if (blurLength < 1e-4)
    {
        FragColor = vec4(baseColor, 1.0);
        return;
    }

    blurLength = min(blurLength, uMaxSampleDistance);
    vec2 direction = blurVector / max(blurLength, 1e-4);

    int samples = max(uSampleCount, 2);
    vec3 accumulated = baseColor;
    float totalWeight = 1.0;

    for (int i = 0; i < samples; ++i)
    {
        float t = float(i) / float(samples - 1);
        float centered = (t - 0.5) * 2.0;
        vec2 sampleUv = uv + direction * centered * blurLength;
        vec3 sampleColor = SampleColor(sampleUv);
        
        float weight = 1.0;
        if (uUseDepth == 1)
        {
            float sampleDepth = SampleDepth(sampleUv);
            float depthFade = smoothstep(0.0, uDepthThreshold, abs(sampleDepth - depth));
            weight = 1.0 - depthFade;
        }
        
        float falloff = 1.0 - abs(centered);
        weight *= falloff;
        accumulated += sampleColor * weight;
        totalWeight += weight;
    }

    FragColor = vec4(accumulated / max(totalWeight, 0.0001), 1.0);
}";

        _shader = new Shader(vertexShader, fragmentShader);
    }

    private void CreateCopyShader()
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
uniform sampler2D uTexture;
void main()
{
    FragColor = texture(uTexture, TexCoord);
}";

        _copyShader = new Shader(vertexShader, fragmentShader);
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
        if (!Enabled || _shader == null || !_settings.MotionBlurEnabled)
            return;

        if (!_hasCameraHistory)
        {
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            
            if (_width != width || _height != height)
            {
                Resize(width, height);
            }
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
            GL.Viewport(0, 0, _width, _height);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            
            if (_copyShader == null)
            {
                CreateCopyShader();
            }
            
            _copyShader!.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
            _copyShader.SetInt("uTexture", 0);
            RenderQuad();
            
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
            return;
        }

        if (_width != width || _height != height)
        {
            Resize(width, height);
        }

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _shader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _shader.SetInt("uColorTexture", 0);

        _shader.SetMatrix4("uInverseViewProjection", _inverseCurrentViewProjection);
        _shader.SetMatrix4("uPreviousViewProjection", _previousViewProjection);
        _shader.SetFloat("uBlurScale", ComputeBlurScale());
        _shader.SetFloat("uMaxSampleDistance", System.MathF.Max(0.05f, _settings.MotionBlurMaxSampleDistance));
        _shader.SetFloat("uDepthThreshold", 0.02f);
        _shader.SetInt("uSampleCount", System.Math.Clamp(_settings.MotionBlurSampleCount, 4, 32));
        _shader.SetInt("uUseDepth", _depthTexture > 0 ? 1 : 0);
        
        if (_depthTexture > 0)
        {
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
            _shader.SetInt("uDepthTexture", 1);
        }

        RenderQuad();

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
    }

    private float ComputeBlurScale()
    {
        float shutter = System.Math.Clamp(_settings.MotionBlurShutterAngle / 360.0f, 0.0f, 1.0f);
        float intensity = System.Math.Clamp(_settings.MotionBlurIntensity, 0.0f, 2.0f);
        float timeScale = System.Math.Clamp(_deltaTime * 60.0f, 0.1f, 4.0f);
        return shutter * intensity * timeScale;
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
        GL.DeleteFramebuffer(_outputFramebuffer);
        CreateFramebuffer();
    }

    protected override void DisposeEffect(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteFramebuffer(_outputFramebuffer);
            GL.DeleteTexture(_outputTexture);
            GL.DeleteVertexArray(_quadVao);
            GL.DeleteBuffer(_quadVbo);
            GL.DeleteBuffer(_quadEbo);
            _shader?.Dispose();
            _copyShader?.Dispose();
        }
    }
}

