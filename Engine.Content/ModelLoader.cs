using System;
using System.Collections.Generic;
using System.Linq;
using Assimp;
using Engine.Core;
using Engine.Graphics;
using Engine.Math;

namespace Engine.Content;

public static class ModelLoader
{
    public static Engine.Graphics.Mesh? LoadMesh(string filePath)
    {
        using var context = new AssimpContext();
        var scene = context.ImportFile(filePath, PostProcessSteps.Triangulate | PostProcessSteps.FlipUVs | PostProcessSteps.GenerateNormals);

        if (scene == null || !scene.HasMeshes)
            return null;

        var allVertices = new List<float>();
        var allIndices = new List<uint>();
        uint indexOffset = 0;

        foreach (Assimp.Mesh mesh in scene.Meshes)
        {
            if (!mesh.HasVertices)
                continue;

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                var vertex = mesh.Vertices[i];
                allVertices.Add(vertex.X);
                allVertices.Add(vertex.Y);
                allVertices.Add(vertex.Z);

                if (mesh.HasNormals)
                {
                    var normal = mesh.Normals[i];
                    allVertices.Add(normal.X);
                    allVertices.Add(normal.Y);
                    allVertices.Add(normal.Z);
                }
                else
                {
                    allVertices.Add(0);
                    allVertices.Add(1);
                    allVertices.Add(0);
                }

                if (mesh.HasTextureCoords(0))
                {
                    var texCoord = mesh.TextureCoordinateChannels[0][i];
                    allVertices.Add(texCoord.X);
                    allVertices.Add(texCoord.Y);
                }
                else
                {
                    allVertices.Add(0);
                    allVertices.Add(0);
                }
            }

            if (mesh.HasFaces)
            {
                foreach (var face in mesh.Faces)
                {
                    if (face.IndexCount == 3)
                    {
                        allIndices.Add(indexOffset + (uint)face.Indices[0]);
                        allIndices.Add(indexOffset + (uint)face.Indices[1]);
                        allIndices.Add(indexOffset + (uint)face.Indices[2]);
                    }
                }
            }

            indexOffset += (uint)mesh.VertexCount;
        }

        if (allVertices.Count == 0)
            return null;

        var layout = new Engine.Renderer.VertexLayout()
            .Add("Position", 3)
            .Add("Normal", 3)
            .Add("TexCoord", 2);

        return new Engine.Graphics.Mesh(allVertices.ToArray(), allIndices.ToArray(), layout);
    }
}

