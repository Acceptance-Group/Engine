using System;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class FXAA : Disposable
{
    private Shader? _shader;
    private uint _framebuffer;
    private uint _texture;
    private int _width;
    private int _height;

    public uint Texture => _texture;

    public FXAA(int width, int height)
    {
        _width = width;
        _height = height;
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
            throw new Exception("FXAA framebuffer is not complete!");
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

void main()
{
    TexCoord = aTexCoord;
    gl_Position = vec4(aPosition, 0.0, 1.0);
}";

        string fragmentShader = @"
#version 330 core
out vec4 FragColor;

in vec2 TexCoord;

uniform sampler2D uTexture;
uniform vec2 uTextureSize;

#define FXAA_REDUCE_MIN (1.0/128.0)
#define FXAA_REDUCE_MUL (1.0/8.0)
#define FXAA_SPAN_MAX 8.0

void main()
{
    vec2 texCoordOffset = 1.0 / uTextureSize;
    
    vec3 rgbNW = texture(uTexture, TexCoord + vec2(-1.0, -1.0) * texCoordOffset).rgb;
    vec3 rgbNE = texture(uTexture, TexCoord + vec2(1.0, -1.0) * texCoordOffset).rgb;
    vec3 rgbSW = texture(uTexture, TexCoord + vec2(-1.0, 1.0) * texCoordOffset).rgb;
    vec3 rgbSE = texture(uTexture, TexCoord + vec2(1.0, 1.0) * texCoordOffset).rgb;
    vec3 rgbM = texture(uTexture, TexCoord).rgb;
    
    vec3 luma = vec3(0.299, 0.587, 0.114);
    float lumaNW = dot(rgbNW, luma);
    float lumaNE = dot(rgbNE, luma);
    float lumaSW = dot(rgbSW, luma);
    float lumaSE = dot(rgbSE, luma);
    float lumaM = dot(rgbM, luma);
    
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    
    vec2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y = ((lumaNW + lumaSW) - (lumaNE + lumaSE));
    
    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * FXAA_REDUCE_MUL), FXAA_REDUCE_MIN);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = min(vec2(FXAA_SPAN_MAX, FXAA_SPAN_MAX), max(vec2(-FXAA_SPAN_MAX, -FXAA_SPAN_MAX), dir * rcpDirMin)) * texCoordOffset;
    
    vec3 rgbA = 0.5 * (texture(uTexture, TexCoord + dir * (1.0/3.0 - 0.5)).rgb + texture(uTexture, TexCoord + dir * (2.0/3.0 - 0.5)).rgb);
    vec3 rgbB = rgbA * 0.5 + 0.25 * (texture(uTexture, TexCoord + dir * -0.5).rgb + texture(uTexture, TexCoord + dir * 0.5).rgb);
    
    float lumaB = dot(rgbB, luma);
    
    if ((lumaB < lumaMin) || (lumaB > lumaMax))
    {
        FragColor = vec4(rgbA, 1.0);
    }
    else
    {
        FragColor = vec4(rgbB, 1.0);
    }
}";

        _shader = new Shader(vertexShader, fragmentShader);
    }

    public void Render(uint sourceTexture)
    {
        if (_shader == null)
            return;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        _shader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _shader.SetInt("uTexture", 0);
        _shader.SetVector2("uTextureSize", new Vector2(_width, _height));

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

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteFramebuffer(_framebuffer);
            GL.DeleteTexture(_texture);
            _shader?.Dispose();
        }
    }
}

