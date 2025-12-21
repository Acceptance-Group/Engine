using OpenTK.Graphics.OpenGL4;
using Engine.Core;

namespace Engine.Renderer;

public class IndexBuffer : Disposable
{
    private readonly int _bufferID;
    private readonly int _indexCount;

    public int BufferID => _bufferID;
    public int IndexCount => _indexCount;

    public IndexBuffer(uint[] indices)
    {
        _indexCount = indices.Length;

        _bufferID = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _bufferID);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    public void Bind()
    {
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _bufferID);
    }

    public void Unbind()
    {
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    public uint[] GetData()
    {
        Bind();
        int size = _indexCount * sizeof(uint);
        uint[] data = new uint[_indexCount];
        GL.GetBufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, size, data);
        Unbind();
        return data;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteBuffer(_bufferID);
        }
    }
}

