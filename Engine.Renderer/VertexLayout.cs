using System.Collections.Generic;

namespace Engine.Renderer;

public class VertexElement
{
    public int Count { get; }
    public string Name { get; }

    public VertexElement(string name, int count)
    {
        Name = name;
        Count = count;
    }
}

public class VertexLayout
{
    public List<VertexElement> Elements { get; } = new List<VertexElement>();
    public int Stride { get; private set; }

    public VertexLayout()
    {
        Stride = 0;
    }

    public VertexLayout Add(string name, int count)
    {
        Elements.Add(new VertexElement(name, count));
        Stride += count;
        return this;
    }
}

