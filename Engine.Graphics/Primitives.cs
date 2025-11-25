using System;
using System.Collections.Generic;
using Engine.Math;

namespace Engine.Graphics;

public static class Primitives
{
    public static Mesh CreateCube(float size = 1.0f)
    {
        float s = size * 0.5f;

        float[] vertices = new float[]
        {
            -s, -s, -s,  0,  0, -1,  0, 0,
             s, -s, -s,  0,  0, -1,  1, 0,
             s,  s, -s,  0,  0, -1,  1, 1,
             s,  s, -s,  0,  0, -1,  1, 1,
            -s,  s, -s,  0,  0, -1,  0, 1,
            -s, -s, -s,  0,  0, -1,  0, 0,

            -s, -s,  s,  0,  0,  1,  0, 0,
             s, -s,  s,  0,  0,  1,  1, 0,
             s,  s,  s,  0,  0,  1,  1, 1,
             s,  s,  s,  0,  0,  1,  1, 1,
            -s,  s,  s,  0,  0,  1,  0, 1,
            -s, -s,  s,  0,  0,  1,  0, 0,

            -s,  s,  s, -1,  0,  0,  1, 0,
            -s,  s, -s, -1,  0,  0,  1, 1,
            -s, -s, -s, -1,  0,  0,  0, 1,
            -s, -s, -s, -1,  0,  0,  0, 1,
            -s, -s,  s, -1,  0,  0,  0, 0,
            -s,  s,  s, -1,  0,  0,  1, 0,

             s,  s,  s,  1,  0,  0,  1, 0,
             s,  s, -s,  1,  0,  0,  1, 1,
             s, -s, -s,  1,  0,  0,  0, 1,
             s, -s, -s,  1,  0,  0,  0, 1,
             s, -s,  s,  1,  0,  0,  0, 0,
             s,  s,  s,  1,  0,  0,  1, 0,

            -s, -s, -s,  0, -1,  0,  0, 1,
             s, -s, -s,  0, -1,  0,  1, 1,
             s, -s,  s,  0, -1,  0,  1, 0,
             s, -s,  s,  0, -1,  0,  1, 0,
            -s, -s,  s,  0, -1,  0,  0, 0,
            -s, -s, -s,  0, -1,  0,  0, 1,

            -s,  s, -s,  0,  1,  0,  0, 1,
             s,  s, -s,  0,  1,  0,  1, 1,
             s,  s,  s,  0,  1,  0,  1, 0,
             s,  s,  s,  0,  1,  0,  1, 0,
            -s,  s,  s,  0,  1,  0,  0, 0,
            -s,  s, -s,  0,  1,  0,  0, 1
        };

        return new Mesh(vertices);
    }

    public static Mesh CreateSphere(float radius = 1.0f, int segments = 32)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        for (int y = 0; y <= segments; y++)
        {
            float theta = MathF.PI * y / segments;
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (int x = 0; x <= segments; x++)
            {
                float phi = 2.0f * MathF.PI * x / segments;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                Vector3 position = new Vector3(
                    cosPhi * sinTheta,
                    cosTheta,
                    sinPhi * sinTheta
                ) * radius;

                Vector3 normal = position.Normalized();
                Vector2 texCoord = new Vector2((float)x / segments, (float)y / segments);

                vertices.Add(position.X);
                vertices.Add(position.Y);
                vertices.Add(position.Z);
                vertices.Add(normal.X);
                vertices.Add(normal.Y);
                vertices.Add(normal.Z);
                vertices.Add(texCoord.X);
                vertices.Add(texCoord.Y);
            }
        }

        for (int y = 0; y < segments; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                uint first = (uint)(y * (segments + 1) + x);
                uint second = (uint)(first + segments + 1);

                indices.Add(first);
                indices.Add(second);
                indices.Add(first + 1);

                indices.Add(second);
                indices.Add(second + 1);
                indices.Add(first + 1);
            }
        }

        return new Mesh(vertices.ToArray(), indices.ToArray());
    }
}

