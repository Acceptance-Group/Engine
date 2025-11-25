using OpenTK.Graphics.OpenGL4;
using Engine.Core;

namespace Engine.Renderer;

public class VertexBuffer : Disposable
{
    private readonly int _bufferID;
    private readonly int _vertexCount;
    private readonly VertexLayout _layout;

    public int BufferID => _bufferID;
    public int VertexCount => _vertexCount;
    public VertexLayout Layout => _layout;

    public VertexBuffer(float[] vertices, VertexLayout layout)
    {
        _layout = layout;
        _vertexCount = vertices.Length / layout.Stride;

        _bufferID = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _bufferID);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void Bind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, _bufferID);
    }

    public void Unbind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteBuffer(_bufferID);
        }
    }
}

