using System;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;

namespace Engine.Graphics;

public class Mesh : Disposable
{
    private readonly VertexArray _vertexArray;
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer? _indexBuffer;
    private readonly int _indexCount;
    private readonly int _vertexCount;

    public VertexArray VertexArray => _vertexArray;
    public int IndexCount => _indexCount;
    public int VertexCount => _vertexCount;

    public Mesh(float[] vertices, uint[]? indices = null, VertexLayout? layout = null)
    {
        var vertexLayout = layout ?? new VertexLayout().Add("Position", 3).Add("Normal", 3).Add("TexCoord", 2);
        _vertexBuffer = new VertexBuffer(vertices, vertexLayout);
        _vertexCount = vertices.Length / vertexLayout.Stride;

        if (indices != null && indices.Length > 0)
        {
            _indexBuffer = new IndexBuffer(indices);
            _indexCount = indices.Length;
        }
        else
        {
            _indexCount = 0;
        }

        _vertexArray = new VertexArray();
        _vertexArray.SetVertexBuffer(_vertexBuffer);
        if (_indexBuffer != null)
            _vertexArray.SetIndexBuffer(_indexBuffer);
    }

    public void Draw()
    {
        _vertexArray.Draw();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vertexArray.Dispose();
            _vertexBuffer.Dispose();
            _indexBuffer?.Dispose();
        }
    }
}

