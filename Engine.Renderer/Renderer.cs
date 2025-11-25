using OpenTK.Graphics.OpenGL4;
using Engine.Math;

namespace Engine.Renderer;

public static class Renderer
{
    public static void Clear(Color color)
    {
        GL.ClearColor(color.R, color.G, color.B, color.A);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public static void EnableDepthTest(bool enable)
    {
        if (enable)
            GL.Enable(EnableCap.DepthTest);
        else
            GL.Disable(EnableCap.DepthTest);
    }

    public static void EnableBlending(bool enable)
    {
        if (enable)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        else
        {
            GL.Disable(EnableCap.Blend);
        }
    }

    public static void SetViewport(int x, int y, int width, int height)
    {
        GL.Viewport(x, y, width, height);
    }

    public static void DrawIndexed(int indexCount)
    {
        GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);
    }

    public static void DrawArrays(int vertexCount)
    {
        GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
    }
}

public struct Color
{
    public float R;
    public float G;
    public float B;
    public float A;

    public Color(float r, float g, float b, float a = 1.0f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static Color Black => new Color(0, 0, 0);
    public static Color White => new Color(1, 1, 1);
    public static Color Red => new Color(1, 0, 0);
    public static Color Green => new Color(0, 1, 0);
    public static Color Blue => new Color(0, 0, 1);
}

