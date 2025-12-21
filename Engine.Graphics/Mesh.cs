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
    private float[]? _cachedVertexData;
    private uint[]? _cachedIndexData;

    public VertexArray VertexArray => _vertexArray;
    public VertexBuffer VertexBuffer => _vertexBuffer;
    public IndexBuffer? IndexBuffer => _indexBuffer;
    public int IndexCount => _indexCount;
    public int VertexCount => _vertexCount;

    public Mesh(float[] vertices, uint[]? indices = null, VertexLayout? layout = null)
    {
        var vertexLayout = layout ?? new VertexLayout().Add("Position", 3).Add("Normal", 3).Add("TexCoord", 2);
        _vertexBuffer = new VertexBuffer(vertices, vertexLayout);
        _vertexCount = vertices.Length / vertexLayout.Stride;
        _cachedVertexData = (float[])vertices.Clone();

        if (indices != null && indices.Length > 0)
        {
            _indexBuffer = new IndexBuffer(indices);
            _indexCount = indices.Length;
            _cachedIndexData = (uint[])indices.Clone();
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

    public float[] GetVertexData()
    {
        if (_cachedVertexData == null)
        {
            _cachedVertexData = _vertexBuffer.GetData();
        }
        return _cachedVertexData;
    }
    
    public uint[]? GetIndexData()
    {
        if (_indexBuffer == null)
            return null;
            
        if (_cachedIndexData == null)
        {
            _cachedIndexData = _indexBuffer.GetData();
        }
        return _cachedIndexData;
    }
    
    public int GetVertexStride()
    {
        return _vertexBuffer.Layout.Stride;
    }
    
    public (Vector3 min, Vector3 max) GetBoundingBox()
    {
        float[] vertexData = GetVertexData();
        int stride = _vertexBuffer.Layout.Stride;
        
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        
        if (_indexBuffer != null)
        {
            uint[]? indices = GetIndexData();
            if (indices != null)
            {
                foreach (uint index in indices)
                {
                    int offset = (int)index * stride;
                    if (offset + 2 < vertexData.Length)
                    {
                        Vector3 pos = new Vector3(vertexData[offset], vertexData[offset + 1], vertexData[offset + 2]);
                        min = new Vector3(MathF.Min(min.X, pos.X), MathF.Min(min.Y, pos.Y), MathF.Min(min.Z, pos.Z));
                        max = new Vector3(MathF.Max(max.X, pos.X), MathF.Max(max.Y, pos.Y), MathF.Max(max.Z, pos.Z));
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < _vertexCount; i++)
            {
                int offset = i * stride;
                if (offset + 2 < vertexData.Length)
                {
                    Vector3 pos = new Vector3(vertexData[offset], vertexData[offset + 1], vertexData[offset + 2]);
                    min = new Vector3(MathF.Min(min.X, pos.X), MathF.Min(min.Y, pos.Y), MathF.Min(min.Z, pos.Z));
                    max = new Vector3(MathF.Max(max.X, pos.X), MathF.Max(max.Y, pos.Y), MathF.Max(max.Z, pos.Z));
                }
            }
        }
        
        return (min, max);
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

