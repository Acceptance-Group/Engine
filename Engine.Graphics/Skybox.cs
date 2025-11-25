using System;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;

namespace Engine.Graphics;

public class Skybox : Disposable
{
    private int _vao;
    private int _vbo;
    private Shader? _shader;

    private float[] _vertices =
    {
        -1.0f,  1.0f, -1.0f,
        -1.0f, -1.0f, -1.0f,
         1.0f, -1.0f, -1.0f,
         1.0f, -1.0f, -1.0f,
         1.0f,  1.0f, -1.0f,
        -1.0f,  1.0f, -1.0f,

        -1.0f, -1.0f,  1.0f,
        -1.0f, -1.0f, -1.0f,
        -1.0f,  1.0f, -1.0f,
        -1.0f,  1.0f, -1.0f,
        -1.0f,  1.0f,  1.0f,
        -1.0f, -1.0f,  1.0f,

         1.0f, -1.0f, -1.0f,
         1.0f, -1.0f,  1.0f,
         1.0f,  1.0f,  1.0f,
         1.0f,  1.0f,  1.0f,
         1.0f,  1.0f, -1.0f,
         1.0f, -1.0f, -1.0f,

        -1.0f, -1.0f,  1.0f,
        -1.0f,  1.0f,  1.0f,
         1.0f,  1.0f,  1.0f,
         1.0f,  1.0f,  1.0f,
         1.0f, -1.0f,  1.0f,
        -1.0f, -1.0f,  1.0f,

        -1.0f,  1.0f, -1.0f,
         1.0f,  1.0f, -1.0f,
         1.0f,  1.0f,  1.0f,
         1.0f,  1.0f,  1.0f,
        -1.0f,  1.0f,  1.0f,
        -1.0f,  1.0f, -1.0f,

        -1.0f, -1.0f, -1.0f,
        -1.0f, -1.0f,  1.0f,
         1.0f, -1.0f, -1.0f,
         1.0f, -1.0f, -1.0f,
        -1.0f, -1.0f,  1.0f,
         1.0f, -1.0f,  1.0f
    };

    public Skybox()
    {
        string vertexShader = @"
#version 330 core
layout (location = 0) in vec3 aPos;

out vec3 TexCoords;

uniform mat4 projection;
uniform mat4 view;

void main()
{
    TexCoords = aPos;
    vec4 pos = projection * view * vec4(aPos, 1.0);
    gl_Position = pos.xyzw;
}";

        string fragmentShader = @"
#version 330 core
out vec4 FragColor;

in vec3 TexCoords;

void main()
{
    vec3 direction = normalize(TexCoords);
    float gradient = (direction.y + 1.0) * 0.5;
    vec3 color = mix(vec3(0.5, 0.7, 1.0), vec3(0.1, 0.1, 0.2), gradient);
    FragColor = vec4(color, 1.0);
}";

        _shader = new Shader(vertexShader, fragmentShader);

        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* ptr = _vertices)
            {
                GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), (IntPtr)ptr, BufferUsageHint.StaticDraw);
            }
        }

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix)
    {
        if (_shader == null)
        {
            Logger.Error("Skybox: Shader is null!");
            return;
        }

        GL.DepthFunc(DepthFunction.Lequal);
        GL.Disable(EnableCap.CullFace);
        GL.DepthMask(false);
        
        _shader.Use();

        Matrix4 view = new Matrix4(
            viewMatrix.M11, viewMatrix.M12, viewMatrix.M13, 0,
            viewMatrix.M21, viewMatrix.M22, viewMatrix.M23, 0,
            viewMatrix.M31, viewMatrix.M32, viewMatrix.M33, 0,
            0, 0, 0, 1
        );

        _shader.SetMatrix4("view", view);
        _shader.SetMatrix4("projection", projectionMatrix);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 36);
        
        ErrorCode error = GL.GetError();
        if (error != ErrorCode.NoError)
        {
            Logger.Error($"Skybox: OpenGL error after DrawArrays: {error}");
        }
        
        GL.BindVertexArray(0);
        
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            _shader?.Dispose();
        }
    }
}

