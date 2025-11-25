using OpenTK.Graphics.OpenGL4;
using Engine.Core;

namespace Engine.Renderer;

public class VertexArray : Disposable
{
    private readonly int _arrayID;
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;

    public int ArrayID => _arrayID;

    public VertexArray()
    {
        _arrayID = GL.GenVertexArray();
    }

    public void SetVertexBuffer(VertexBuffer vertexBuffer)
    {
        _vertexBuffer = vertexBuffer;
        Bind();
        vertexBuffer.Bind();

        var layout = vertexBuffer.Layout;
        int offset = 0;
        for (int i = 0; i < layout.Elements.Count; i++)
        {
            var element = layout.Elements[i];
            GL.EnableVertexAttribArray(i);
            GL.VertexAttribPointer(i, element.Count, VertexAttribPointerType.Float, false, layout.Stride * sizeof(float), offset * sizeof(float));
            offset += element.Count;
        }

        Unbind();
        vertexBuffer.Unbind();
    }

    public void SetIndexBuffer(IndexBuffer indexBuffer)
    {
        _indexBuffer = indexBuffer;
    }

    public void Bind()
    {
        GL.BindVertexArray(_arrayID);
    }

    public void Unbind()
    {
        GL.BindVertexArray(0);
    }

    public void Draw()
    {
        if (_indexBuffer != null)
        {
            Bind();
            _indexBuffer.Bind();
            GL.DrawElements(PrimitiveType.Triangles, _indexBuffer.IndexCount, DrawElementsType.UnsignedInt, 0);
            Unbind();
            _indexBuffer.Unbind();
        }
        else if (_vertexBuffer != null)
        {
            Bind();
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexBuffer.VertexCount);
            Unbind();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteVertexArray(_arrayID);
        }
    }
}

