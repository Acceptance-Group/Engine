using System;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class SMAA : Disposable
{
    private Shader? _edgeShader;
    private Shader? _blendShader;
    private Shader? _neighborhoodShader;
    private uint _edgeFramebuffer;
    private uint _edgeTexture;
    private uint _blendFramebuffer;
    private uint _blendTexture;
    private uint _finalFramebuffer;
    private uint _finalTexture;
    private int _width;
    private int _height;

    public uint Texture => _finalTexture;

    public SMAA(int width, int height)
    {
        _width = width;
        _height = height;
        CreateFramebuffers();
        CreateShaders();
    }

    private void CreateFramebuffers()
    {
        _edgeFramebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _edgeFramebuffer);
        _edgeTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _edgeTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _edgeTexture, 0);

        _blendFramebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blendFramebuffer);
        _blendTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _blendTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _blendTexture, 0);

        _finalFramebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _finalFramebuffer);
        _finalTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _finalTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _finalTexture, 0);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void CreateShaders()
    {
        string edgeVertexShader = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
void main() { TexCoord = aTexCoord; gl_Position = vec4(aPosition, 0.0, 1.0); }";

        string edgeFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform vec2 uTextureSize;
void main() {
    vec2 offset = 1.0 / uTextureSize;
    
    vec3 luma = vec3(0.299, 0.587, 0.114);
    
    vec3 c = texture(uTexture, TexCoord).rgb;
    vec3 l = texture(uTexture, TexCoord + vec2(-offset.x, 0.0)).rgb;
    vec3 r = texture(uTexture, TexCoord + vec2(offset.x, 0.0)).rgb;
    vec3 t = texture(uTexture, TexCoord + vec2(0.0, -offset.y)).rgb;
    vec3 b = texture(uTexture, TexCoord + vec2(0.0, offset.y)).rgb;
    
    float lumaC = dot(c, luma);
    float lumaL = dot(l, luma);
    float lumaR = dot(r, luma);
    float lumaT = dot(t, luma);
    float lumaB = dot(b, luma);
    
    float edgeH = abs(lumaC - lumaL) + abs(lumaC - lumaR);
    float edgeV = abs(lumaC - lumaT) + abs(lumaC - lumaB);
    
    float threshold = 0.05;
    edgeH = smoothstep(threshold * 0.5, threshold * 1.5, edgeH);
    edgeV = smoothstep(threshold * 0.5, threshold * 1.5, edgeV);
    
    FragColor = vec4(edgeH, edgeV, 0.0, 1.0);
}";

        _edgeShader = new Shader(edgeVertexShader, edgeFragmentShader);

        string blendFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform sampler2D uEdgeTexture;
uniform vec2 uTextureSize;
void main() {
    vec2 offset = 1.0 / uTextureSize;
    vec4 edge = texture(uEdgeTexture, TexCoord);
    
    float blendH = edge.r;
    float blendV = edge.g;
    
    FragColor = vec4(blendH, blendV, 0.0, 1.0);
}";

        _blendShader = new Shader(edgeVertexShader, blendFragmentShader);

        string neighborhoodFragmentShader = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform sampler2D uBlendTexture;
uniform vec2 uTextureSize;
void main() {
    vec2 offset = 1.0 / uTextureSize;
    vec4 color = texture(uTexture, TexCoord);
    vec4 blend = texture(uBlendTexture, TexCoord);
    
    float blendH = blend.r;
    float blendV = blend.g;
    
    if (blendH > 0.01 || blendV > 0.01) {
        vec4 left = texture(uTexture, TexCoord + vec2(-offset.x, 0.0));
        vec4 right = texture(uTexture, TexCoord + vec2(offset.x, 0.0));
        vec4 top = texture(uTexture, TexCoord + vec2(0.0, -offset.y));
        vec4 bottom = texture(uTexture, TexCoord + vec2(0.0, offset.y));
        
        vec4 leftTop = texture(uTexture, TexCoord + vec2(-offset.x, -offset.y));
        vec4 rightTop = texture(uTexture, TexCoord + vec2(offset.x, -offset.y));
        vec4 leftBottom = texture(uTexture, TexCoord + vec2(-offset.x, offset.y));
        vec4 rightBottom = texture(uTexture, TexCoord + vec2(offset.x, offset.y));
        
        vec4 neighbors = (left + right + top + bottom + leftTop + rightTop + leftBottom + rightBottom) * 0.125;
        
        float blendAmount = max(blendH, blendV) * 0.8;
        FragColor = mix(color, neighbors, blendAmount);
    } else {
        FragColor = color;
    }
}";

        _neighborhoodShader = new Shader(edgeVertexShader, neighborhoodFragmentShader);
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

    public void Render(uint sourceTexture)
    {
        if (_edgeShader == null || _blendShader == null || _neighborhoodShader == null)
            return;

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _edgeFramebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _edgeShader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _edgeShader.SetInt("uTexture", 0);
        _edgeShader.SetVector2("uTextureSize", new Vector2(_width, _height));
        RenderQuad();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _blendFramebuffer);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _blendShader.Use();
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _blendShader.SetInt("uTexture", 0);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _edgeTexture);
        _blendShader.SetInt("uEdgeTexture", 1);
        _blendShader.SetVector2("uTextureSize", new Vector2(_width, _height));
        RenderQuad();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _finalFramebuffer);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _neighborhoodShader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, sourceTexture);
        _neighborhoodShader.SetInt("uTexture", 0);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _blendTexture);
        _neighborhoodShader.SetInt("uBlendTexture", 1);
        _neighborhoodShader.SetVector2("uTextureSize", new Vector2(_width, _height));
        RenderQuad();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Resize(int width, int height)
    {
        if (_width == width && _height == height)
            return;

        _width = width;
        _height = height;

        GL.DeleteTexture(_edgeTexture);
        GL.DeleteTexture(_blendTexture);
        GL.DeleteTexture(_finalTexture);
        GL.DeleteFramebuffer(_edgeFramebuffer);
        GL.DeleteFramebuffer(_blendFramebuffer);
        GL.DeleteFramebuffer(_finalFramebuffer);
        CreateFramebuffers();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteFramebuffer(_edgeFramebuffer);
            GL.DeleteFramebuffer(_blendFramebuffer);
            GL.DeleteFramebuffer(_finalFramebuffer);
            GL.DeleteTexture(_edgeTexture);
            GL.DeleteTexture(_blendTexture);
            GL.DeleteTexture(_finalTexture);
            _edgeShader?.Dispose();
            _blendShader?.Dispose();
            _neighborhoodShader?.Dispose();
        }
    }
}

