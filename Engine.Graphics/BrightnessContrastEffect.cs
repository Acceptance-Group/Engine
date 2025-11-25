using Engine.Core;
using Engine.Renderer;

namespace Engine.Graphics;

public class BrightnessContrastEffect : PostProcessingEffect
{
    private Shader? _shader;
    private int _quadVAO;
    private int _quadVBO;

    public float Brightness { get; set; } = 0.0f;
    public float Contrast { get; set; } = 1.0f;

    public BrightnessContrastEffect()
    {
        CreateShader();
        CreateQuad();
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
    gl_Position = vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}";

        string fragmentShader = @"
#version 330 core
out vec4 FragColor;

in vec2 TexCoord;

uniform sampler2D uScreenTexture;
uniform float uBrightness;
uniform float uContrast;

void main()
{
    vec3 color = texture(uScreenTexture, TexCoord).rgb;
    color = (color - 0.5) * uContrast + 0.5 + uBrightness;
    FragColor = vec4(color, 1.0);
}";

        _shader = new Shader(vertexShader, fragmentShader);
    }

    private void CreateQuad()
    {
        float[] quadVertices = {
            -1.0f,  1.0f,  0.0f, 1.0f,
            -1.0f, -1.0f,  0.0f, 0.0f,
             1.0f, -1.0f,  1.0f, 0.0f,
            -1.0f,  1.0f,  0.0f, 1.0f,
             1.0f, -1.0f,  1.0f, 0.0f,
             1.0f,  1.0f,  1.0f, 1.0f
        };

        _quadVAO = OpenTK.Graphics.OpenGL4.GL.GenVertexArray();
        _quadVBO = OpenTK.Graphics.OpenGL4.GL.GenBuffer();

        OpenTK.Graphics.OpenGL4.GL.BindVertexArray(_quadVAO);
        OpenTK.Graphics.OpenGL4.GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.ArrayBuffer, _quadVBO);
        OpenTK.Graphics.OpenGL4.GL.BufferData(OpenTK.Graphics.OpenGL4.BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, OpenTK.Graphics.OpenGL4.BufferUsageHint.StaticDraw);

        OpenTK.Graphics.OpenGL4.GL.EnableVertexAttribArray(0);
        OpenTK.Graphics.OpenGL4.GL.VertexAttribPointer(0, 2, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        OpenTK.Graphics.OpenGL4.GL.EnableVertexAttribArray(1);
        OpenTK.Graphics.OpenGL4.GL.VertexAttribPointer(1, 2, OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
    }

    public override void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height)
    {
        if (!Enabled || _shader == null)
            return;

        OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, targetFramebuffer);
        OpenTK.Graphics.OpenGL4.GL.Viewport(0, 0, width, height);
        OpenTK.Graphics.OpenGL4.GL.Clear(OpenTK.Graphics.OpenGL4.ClearBufferMask.ColorBufferBit);

        _shader.Use();
        _shader.SetFloat("uBrightness", Brightness);
        _shader.SetFloat("uContrast", Contrast);

        OpenTK.Graphics.OpenGL4.GL.ActiveTexture(OpenTK.Graphics.OpenGL4.TextureUnit.Texture0);
        OpenTK.Graphics.OpenGL4.GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, (int)sourceTexture);
        _shader.SetInt("uScreenTexture", 0);

        OpenTK.Graphics.OpenGL4.GL.BindVertexArray(_quadVAO);
        OpenTK.Graphics.OpenGL4.GL.DrawArrays(OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles, 0, 6);
        OpenTK.Graphics.OpenGL4.GL.BindVertexArray(0);
    }

    protected override void DisposeEffect(bool disposing)
    {
        if (disposing)
        {
            _shader?.Dispose();
            OpenTK.Graphics.OpenGL4.GL.DeleteVertexArray(_quadVAO);
            OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(_quadVBO);
        }
    }
}

