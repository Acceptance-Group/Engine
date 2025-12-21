using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;

namespace Engine.Graphics;

public class PathTracer : PostProcessingEffect
{
    private const int LOCAL_SIZE_X = 8;
    private const int LOCAL_SIZE_Y = 8;
    
    private Shader? _computeShader;
    private uint _resultTexture;
    private int _width;
    private int _height;
    private PathTracingSettings _settings;
    private Camera? _camera;
    private int _frameCount;
    private uint _basicDataUBO;
    private DirectionalLight? _directionalLight;
    private uint _depthTexture;
    private uint _colorTexture;
    
    private uint _triangleSSBO;
    private uint _bvhSSBO;
    private int _triangleCount;
    private int _bvhNodeCount;
    private Scene? _currentScene;
    private int _sceneHash;
    private int _lastSceneHash = -1;
    private int _cameraHash;

    public uint Texture => _resultTexture;

    public PathTracer(int width, int height, PathTracingSettings settings)
    {
        _width = width;
        _height = height;
        _settings = settings;
        CreateTextures();
        CreateUBOs();
        CreateSSBOs();
        CreateShader();
    }

    public void SetTextures(uint colorTexture, uint depthTexture, uint normalTexture = 0)
    {
        _colorTexture = colorTexture;
        _depthTexture = depthTexture;
    }

    public void UpdateScene(Scene scene)
    {
        _currentScene = scene;
        int newHash = ComputeSceneHash(scene);
        int newCameraHash = 0;
        if (_camera != null)
        {
            newCameraHash = _camera.ViewProjectionMatrix.GetHashCode();
            newCameraHash ^= _camera.Position.GetHashCode();
        }
        
        bool sceneChanged = (newHash != _sceneHash);
        bool cameraChanged = (newCameraHash != _cameraHash);
        
        if (sceneChanged)
        {
            _lastSceneHash = _sceneHash;
            _sceneHash = newHash;
            _frameCount = 0;
        }
        
        if (sceneChanged || cameraChanged)
        {
            _cameraHash = newCameraHash;
            UpdateGeometry(scene);
            if (sceneChanged)
            {
                _frameCount = 0;
            }
        }
    }

    public void UpdateCamera(Camera camera)
    {
        bool cameraMoved = false;
        if (_camera != null && camera != null)
        {
            float positionThreshold = 0.001f;
            Vector3 posDiff = _camera.Position - camera.Position;
            bool positionChanged = posDiff.LengthSquared > positionThreshold * positionThreshold;
            
            bool viewChanged = _camera.ViewMatrix != camera.ViewMatrix;
            bool projectionChanged = _camera.ProjectionMatrix != camera.ProjectionMatrix;
            
            cameraMoved = positionChanged || viewChanged || projectionChanged;
        }
        
        _camera = camera;
        
        if (cameraMoved)
        {
            _frameCount = 0;
        }
        
        UpdateBasicDataUBO();
    }
    
    public void SetDirectionalLight(DirectionalLight? light)
    {
        _directionalLight = light;
        UpdateBasicDataUBO();
    }

    private int ComputeSceneHash(Scene scene)
    {
        var renderers = scene.FindObjectsOfType<MeshRenderer>();
        int hash = 0;
        foreach (var renderer in renderers)
        {
            if (renderer.Mesh != null && renderer.Transform != null)
            {
                hash ^= renderer.Mesh.VertexCount.GetHashCode();
                hash ^= renderer.Mesh.IndexCount.GetHashCode();
                hash ^= renderer.Transform.Position.GetHashCode();
                hash ^= renderer.Transform.Scale.GetHashCode();
            }
        }
        return hash;
    }

    private struct FrustumPlane
    {
        public Vector3 Normal;
        public float Distance;
    }

    private FrustumPlane[] ExtractFrustumPlanes(Matrix4 viewProj)
    {
        FrustumPlane[] planes = new FrustumPlane[6];
        
        planes[0].Normal.X = viewProj.M14 + viewProj.M11;
        planes[0].Normal.Y = viewProj.M24 + viewProj.M21;
        planes[0].Normal.Z = viewProj.M34 + viewProj.M31;
        planes[0].Distance = viewProj.M44 + viewProj.M41;
        
        planes[1].Normal.X = viewProj.M14 - viewProj.M11;
        planes[1].Normal.Y = viewProj.M24 - viewProj.M21;
        planes[1].Normal.Z = viewProj.M34 - viewProj.M31;
        planes[1].Distance = viewProj.M44 - viewProj.M41;
        
        planes[2].Normal.X = viewProj.M14 + viewProj.M12;
        planes[2].Normal.Y = viewProj.M24 + viewProj.M22;
        planes[2].Normal.Z = viewProj.M34 + viewProj.M32;
        planes[2].Distance = viewProj.M44 + viewProj.M42;
        
        planes[3].Normal.X = viewProj.M14 - viewProj.M12;
        planes[3].Normal.Y = viewProj.M24 - viewProj.M22;
        planes[3].Normal.Z = viewProj.M34 - viewProj.M32;
        planes[3].Distance = viewProj.M44 - viewProj.M42;
        
        planes[4].Normal.X = viewProj.M14 + viewProj.M13;
        planes[4].Normal.Y = viewProj.M24 + viewProj.M23;
        planes[4].Normal.Z = viewProj.M34 + viewProj.M33;
        planes[4].Distance = viewProj.M44 + viewProj.M43;
        
        planes[5].Normal.X = viewProj.M14 - viewProj.M13;
        planes[5].Normal.Y = viewProj.M24 - viewProj.M23;
        planes[5].Normal.Z = viewProj.M34 - viewProj.M33;
        planes[5].Distance = viewProj.M44 - viewProj.M43;
        
        for (int i = 0; i < 6; i++)
        {
            float length = planes[i].Normal.Length;
            if (length > 0.0001f)
            {
                planes[i].Normal = planes[i].Normal / length;
                planes[i].Distance = planes[i].Distance / length;
            }
        }
        
        return planes;
    }

    private bool IsTriangleInFrustum(Triangle tri, FrustumPlane[] planes, float margin = 0.0f)
    {
        Vector3[] vertices = { tri.V0, tri.V1, tri.V2 };
        
        for (int p = 0; p < 6; p++)
        {
            bool allOutside = true;
            for (int v = 0; v < 3; v++)
            {
                float distance = Vector3.Dot(planes[p].Normal, vertices[v]) + planes[p].Distance + margin;
                if (distance >= -0.01f)
                {
                    allOutside = false;
                    break;
                }
            }
            if (allOutside)
                return false;
        }
        
        return true;
    }

    private void UpdateGeometry(Scene scene)
    {
        var renderers = scene.FindObjectsOfType<MeshRenderer>();
        var triangles = new List<Triangle>();
        
        FrustumPlane[]? frustumPlanes = null;
        float frustumMargin = 0.0f;
        if (_camera != null)
        {
            Matrix4 viewProj = _camera.ViewProjectionMatrix;
            frustumPlanes = ExtractFrustumPlanes(viewProj);
            
            if (_settings.EnableReflections)
            {
                float farDist = _camera.FarPlane;
                float fov = _camera.FOV;
                float tanHalfFov = MathF.Tan(fov * 0.5f);
                
                float farHeight = tanHalfFov * farDist;
                float farWidth = farHeight * _camera.AspectRatio;
                float farDiagonal = MathF.Sqrt(farWidth * farWidth + farHeight * farHeight);
                
                frustumMargin = farDiagonal * 0.5f;
            }
        }
        
        foreach (var renderer in renderers)
        {
            if (renderer.Mesh == null || renderer.Transform == null)
                continue;
                
            var mesh = renderer.Mesh;
            var transform = renderer.Transform;
            Matrix4 modelMatrix = transform.WorldMatrix;
            
            float[] vertexData = mesh.GetVertexData();
            uint[]? indexData = mesh.GetIndexData();
            int stride = mesh.GetVertexStride();
            
            Vector3 materialAlbedo = Vector3.One;
            if (renderer.Material != null)
            {
                var color = renderer.Material.Color;
                materialAlbedo = new Vector3(color.X, color.Y, color.Z);
            }
            
            if (indexData != null && indexData.Length > 0)
            {
                for (int i = 0; i < indexData.Length; i += 3)
                {
                    if (i + 2 >= indexData.Length) break;
                    
                    var tri = ExtractTriangle(vertexData, indexData, i, stride, modelMatrix, materialAlbedo);
                    if (tri.HasValue)
                    {
                        if (frustumPlanes == null || IsTriangleInFrustum(tri.Value, frustumPlanes, frustumMargin))
                        {
                            triangles.Add(tri.Value);
                        }
                    }
                }
            }
            else
            {
                int vertexCount = vertexData.Length / stride;
                for (int i = 0; i < vertexCount; i += 3)
                {
                    if (i + 2 >= vertexCount) break;
                    var tri = ExtractTriangle(vertexData, null, i, stride, modelMatrix, materialAlbedo);
                    if (tri.HasValue)
                    {
                        if (frustumPlanes == null || IsTriangleInFrustum(tri.Value, frustumPlanes, frustumMargin))
                        {
                            triangles.Add(tri.Value);
                        }
                    }
                }
            }
        }
        
        _triangleCount = triangles.Count;

        if (_triangleCount == 0)
        {
            return;
        }
        
        var bvh = BuildBVH(triangles);
        _bvhNodeCount = bvh.Count;
        
        UploadGeometry(triangles, bvh);
    }

    private Triangle? ExtractTriangle(float[] vertexData, uint[]? indices, int startIndex, int stride, Matrix4 transform, Vector3 materialAlbedo)
    {
        Vector3[] positions = new Vector3[3];
        Vector3[] normals = new Vector3[3];
        
        for (int i = 0; i < 3; i++)
        {
            int idx;
            if (indices != null)
            {
                if (startIndex + i >= indices.Length) return null;
                idx = (int)indices[startIndex + i] * stride;
            }
            else
            {
                idx = (startIndex + i) * stride;
            }
            
            if (idx + 2 >= vertexData.Length) return null;
            
            Vector3 pos = new Vector3(vertexData[idx], vertexData[idx + 1], vertexData[idx + 2]);
            Vector4 pos4 = new Vector4(pos.X, pos.Y, pos.Z, 1.0f);
            Vector4 transformedPos = transform * pos4;
            positions[i] = new Vector3(transformedPos.X, transformedPos.Y, transformedPos.Z);
            
            if (idx + 5 < vertexData.Length)
            {
                Vector3 normal = new Vector3(vertexData[idx + 3], vertexData[idx + 4], vertexData[idx + 5]);
                Vector4 normal4 = new Vector4(normal.X, normal.Y, normal.Z, 0.0f);
                Vector4 transformedNormal = transform * normal4;
                normals[i] = new Vector3(transformedNormal.X, transformedNormal.Y, transformedNormal.Z).Normalized();
            }
            else
            {
                normals[i] = new Vector3(0, 1, 0);
            }
        }
        
        return new Triangle
        {
            V0 = positions[0],
            V1 = positions[1],
            V2 = positions[2],
            N0 = normals[0],
            N1 = normals[1],
            N2 = normals[2],
            Albedo = materialAlbedo
        };
    }

    private List<BVHNode> BuildBVH(List<Triangle> triangles)
    {
        var nodes = new List<BVHNode>();
        BuildBVHRecursive(triangles, 0, triangles.Count, nodes, 0);
        _bvhNodeCount = nodes.Count;
        return nodes;
    }

    private int BuildBVHRecursive(List<Triangle> triangles, int start, int end, List<BVHNode> nodes, int depth)
    {
        if (start >= end) return -1;
        
        BVHNode node = new BVHNode();
        int nodeIndex = nodes.Count;
        
        if (end - start == 1)
        {
            var tri = triangles[start];
            node.Min = new Vector3(
                MathF.Min(MathF.Min(tri.V0.X, tri.V1.X), tri.V2.X),
                MathF.Min(MathF.Min(tri.V0.Y, tri.V1.Y), tri.V2.Y),
                MathF.Min(MathF.Min(tri.V0.Z, tri.V1.Z), tri.V2.Z)
            );
            node.Max = new Vector3(
                MathF.Max(MathF.Max(tri.V0.X, tri.V1.X), tri.V2.X),
                MathF.Max(MathF.Max(tri.V0.Y, tri.V1.Y), tri.V2.Y),
                MathF.Max(MathF.Max(tri.V0.Z, tri.V1.Z), tri.V2.Z)
            );
            node.TriangleIndex = start;
            node.LeftChild = -1;
            node.RightChild = -1;
            nodes.Add(node);
            return nodeIndex;
        }
        
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = start; i < end; i++)
        {
            var tri = triangles[i];
            Vector3 triMin = new Vector3(
                MathF.Min(MathF.Min(tri.V0.X, tri.V1.X), tri.V2.X),
                MathF.Min(MathF.Min(tri.V0.Y, tri.V1.Y), tri.V2.Y),
                MathF.Min(MathF.Min(tri.V0.Z, tri.V1.Z), tri.V2.Z)
            );
            Vector3 triMax = new Vector3(
                MathF.Max(MathF.Max(tri.V0.X, tri.V1.X), tri.V2.X),
                MathF.Max(MathF.Max(tri.V0.Y, tri.V1.Y), tri.V2.Y),
                MathF.Max(MathF.Max(tri.V0.Z, tri.V1.Z), tri.V2.Z)
            );
            min = new Vector3(MathF.Min(min.X, triMin.X), MathF.Min(min.Y, triMin.Y), MathF.Min(min.Z, triMin.Z));
            max = new Vector3(MathF.Max(max.X, triMax.X), MathF.Max(max.Y, triMax.Y), MathF.Max(max.Z, triMax.Z));
        }
        
        node.Min = min;
        node.Max = max;
        nodes.Add(node);
        
        int axis = depth % 3;
        float center = 0.0f;
        if (axis == 0)
            center = (min.X + max.X) * 0.5f;
        else if (axis == 1)
            center = (min.Y + max.Y) * 0.5f;
        else
            center = (min.Z + max.Z) * 0.5f;
        
        int mid = start;
        for (int i = start; i < end; i++)
        {
            var tri = triangles[i];
            Vector3 triCenter = (tri.V0 + tri.V1 + tri.V2) / 3.0f;
            float triCenterAxis = axis == 0 ? triCenter.X : (axis == 1 ? triCenter.Y : triCenter.Z);
            if (triCenterAxis < center)
            {
                var temp = triangles[mid];
                triangles[mid] = triangles[i];
                triangles[i] = temp;
                mid++;
            }
        }
        
        if (mid == start || mid == end)
            mid = (start + end) / 2;
        
        node.LeftChild = BuildBVHRecursive(triangles, start, mid, nodes, depth + 1);
        node.RightChild = BuildBVHRecursive(triangles, mid, end, nodes, depth + 1);
        
        nodes[nodeIndex] = node;
        
        return nodeIndex;
    }

    private void UploadGeometry(List<Triangle> triangles, List<BVHNode> bvhNodes)
    {
        unsafe
        {
            int triangleSize = sizeof(float) * 21;
            
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _triangleSSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, triangles.Count * triangleSize, IntPtr.Zero, BufferUsageHint.StaticDraw);
            
            float* triangleData = (float*)GL.MapBuffer(BufferTarget.ShaderStorageBuffer, BufferAccess.WriteOnly);
            for (int i = 0; i < triangles.Count; i++)
            {
                var tri = triangles[i];
                int offset = i * 21;
                triangleData[offset + 0] = tri.V0.X; triangleData[offset + 1] = tri.V0.Y; triangleData[offset + 2] = tri.V0.Z;
                triangleData[offset + 3] = tri.V1.X; triangleData[offset + 4] = tri.V1.Y; triangleData[offset + 5] = tri.V1.Z;
                triangleData[offset + 6] = tri.V2.X; triangleData[offset + 7] = tri.V2.Y; triangleData[offset + 8] = tri.V2.Z;
                triangleData[offset + 9] = tri.N0.X; triangleData[offset + 10] = tri.N0.Y; triangleData[offset + 11] = tri.N0.Z;
                triangleData[offset + 12] = tri.N1.X; triangleData[offset + 13] = tri.N1.Y; triangleData[offset + 14] = tri.N1.Z;
                triangleData[offset + 15] = tri.N2.X; triangleData[offset + 16] = tri.N2.Y; triangleData[offset + 17] = tri.N2.Z;
                triangleData[offset + 18] = tri.Albedo.X; triangleData[offset + 19] = tri.Albedo.Y; triangleData[offset + 20] = tri.Albedo.Z;
            }
            GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            
            int bvhNodeSize = sizeof(float) * 9;
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _bvhSSBO);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, bvhNodes.Count * bvhNodeSize, IntPtr.Zero, BufferUsageHint.StaticDraw);
            
            float* bvhData = (float*)GL.MapBuffer(BufferTarget.ShaderStorageBuffer, BufferAccess.WriteOnly);
            for (int i = 0; i < bvhNodes.Count; i++)
            {
                var node = bvhNodes[i];
                int offset = i * 9;
                bvhData[offset + 0] = node.Min.X;
                bvhData[offset + 1] = node.Min.Y;
                bvhData[offset + 2] = node.Min.Z;
                bvhData[offset + 3] = node.Max.X;
                bvhData[offset + 4] = node.Max.Y;
                bvhData[offset + 5] = node.Max.Z;
                bvhData[offset + 6] = (float)node.LeftChild;
                bvhData[offset + 7] = (float)node.RightChild;
                bvhData[offset + 8] = (float)node.TriangleIndex;
            }
            GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }
    }

    private void CreateTextures()
    {
        _resultTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _resultTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private void CreateUBOs()
    {
        _basicDataUBO = (uint)GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer, _basicDataUBO);
        int basicDataSize = sizeof(float) * 16 * 2 + sizeof(float) * 4 * 3;
        GL.BufferData(BufferTarget.UniformBuffer, basicDataSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _basicDataUBO);
        GL.BindBuffer(BufferTarget.UniformBuffer, 0);
    }

    private void CreateSSBOs()
    {
        _triangleSSBO = (uint)GL.GenBuffer();
        _bvhSSBO = (uint)GL.GenBuffer();
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _triangleSSBO);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _bvhSSBO);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    private void UpdateBasicDataUBO()
    {
        if (_camera == null) return;
        
        Matrix4 invProj = _camera.ProjectionMatrix.Inverse();
        Matrix4 invView = _camera.ViewMatrix.Inverse();

        GL.BindBuffer(BufferTarget.UniformBuffer, _basicDataUBO);
        
        unsafe
        {
            float* data = stackalloc float[16];
            
            data[0] = invProj.M11; data[1] = invProj.M21; data[2] = invProj.M31; data[3] = invProj.M41;
            data[4] = invProj.M12; data[5] = invProj.M22; data[6] = invProj.M32; data[7] = invProj.M42;
            data[8] = invProj.M13; data[9] = invProj.M23; data[10] = invProj.M33; data[11] = invProj.M43;
            data[12] = invProj.M14; data[13] = invProj.M24; data[14] = invProj.M34; data[15] = invProj.M44;
            
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, sizeof(float) * 16, (IntPtr)data);
            
            data[0] = invView.M11; data[1] = invView.M21; data[2] = invView.M31; data[3] = invView.M41;
            data[4] = invView.M12; data[5] = invView.M22; data[6] = invView.M32; data[7] = invView.M42;
            data[8] = invView.M13; data[9] = invView.M23; data[10] = invView.M33; data[11] = invView.M43;
            data[12] = invView.M14; data[13] = invView.M24; data[14] = invView.M34; data[15] = invView.M44;
            
            GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(sizeof(float) * 16), sizeof(float) * 16, (IntPtr)data);
            
            data[0] = _camera.Position.X;
            data[1] = _camera.Position.Y;
            data[2] = _camera.Position.Z;
            data[3] = 0.0f;
            
            GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(sizeof(float) * 32), sizeof(float) * 4, (IntPtr)data);
            
            if (_directionalLight != null)
            {
                Vector3 dir = _directionalLight.Direction;
                float len = (float)System.Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
                if (len > 0.0001f)
                {
                    dir = new Vector3(dir.X / len, dir.Y / len, dir.Z / len);
                }
                data[0] = dir.X;
                data[1] = dir.Y;
                data[2] = dir.Z;
                data[3] = 0.0f;
                
                GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(sizeof(float) * 36), sizeof(float) * 4, (IntPtr)data);
                
                Vector3 lightColor = _directionalLight.Color * _directionalLight.Intensity;
                if (lightColor.X < 0.0001f && lightColor.Y < 0.0001f && lightColor.Z < 0.0001f)
                {
                    lightColor = Vector3.One * 1.0f;
                }
                data[0] = lightColor.X;
                data[1] = lightColor.Y;
                data[2] = lightColor.Z;
                data[3] = 0.0f;
            }
            else
            {
                data[0] = 0.0f;
                data[1] = 0.0f;
                data[2] = 0.0f;
                data[3] = 0.0f;
                GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(sizeof(float) * 36), sizeof(float) * 4, (IntPtr)data);
                
                data[0] = 1.0f;
                data[1] = 1.0f;
                data[2] = 1.0f;
                data[3] = 0.0f;
                GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(sizeof(float) * 40), sizeof(float) * 4, (IntPtr)data);
            }
        }

        GL.BindBuffer(BufferTarget.UniformBuffer, 0);
    }

    private void CreateShader()
    {
        string computeShaderSource = @"
#version 450 core
#define EPSILON 0.0001
#define PI 3.14159265359
#define TWO_OVER_PI 2.0 / PI
#define INF 1e30

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(binding = 0, rgba32f) restrict uniform image2D ImgResult;
uniform sampler2D DepthTexture;
uniform sampler2D ColorTexture;

layout(std140, binding = 0) uniform BasicDataUBO
{
    mat4 InvProjection;
    mat4 InvView;
    vec3 ViewPos;
    vec3 LightDirection;
    vec3 LightColor;
} basicDataUBO;

layout(std430, binding = 1) readonly buffer TriangleBuffer
{
    float triangles[];
};

layout(std430, binding = 2) readonly buffer BVHBuffer
{
    float bvhNodes[];
};

uniform int rayDepth;
uniform int SPP;
uniform int thisRendererFrame;
uniform vec2 screenSize;
uniform int triangleCount;
uniform int bvhNodeCount;
uniform int sceneHash;
uniform int lastSceneHash;
uniform int enableDirectLight;
uniform int enableShadows;
uniform int enableReflections;
uniform int shouldUpdateReflections;

uint rndSeed;

struct Ray
{
    vec3 origin;
    vec3 direction;
};

struct HitInfo
{
    bool hit;
    float t;
    vec3 position;
    vec3 normal;
    vec3 albedo;
};

struct AABBIntersectResult
{
    bool hit;
    float tMin;
    float tMax;
};

struct TriangleIntersectResult
{
    bool hit;
    float t;
    float u;
    float v;
    vec3 normal;
};

AABBIntersectResult RayAABBIntersect(vec3 rayOrigin, vec3 rayInvDir, vec3 aabbMin, vec3 aabbMax)
{
    AABBIntersectResult result;
    vec3 t1 = (aabbMin - rayOrigin) * rayInvDir;
    vec3 t2 = (aabbMax - rayOrigin) * rayInvDir;
    vec3 tMinVec = min(t1, t2);
    vec3 tMaxVec = max(t1, t2);
    result.tMin = max(max(tMinVec.x, tMinVec.y), tMinVec.z);
    result.tMax = min(min(tMaxVec.x, tMaxVec.y), tMaxVec.z);
    
    bool insideAABB = rayOrigin.x >= aabbMin.x && rayOrigin.x <= aabbMax.x &&
                       rayOrigin.y >= aabbMin.y && rayOrigin.y <= aabbMax.y &&
                       rayOrigin.z >= aabbMin.z && rayOrigin.z <= aabbMax.z;
    
    result.hit = result.tMax >= result.tMin && (result.tMax > 0.0 || insideAABB);
    return result;
}

TriangleIntersectResult RayTriangleIntersect(vec3 rayOrigin, vec3 rayDir, vec3 v0, vec3 v1, vec3 v2)
{
    TriangleIntersectResult result;
    result.hit = false;
    result.t = INF;
    result.u = 0.0;
    result.v = 0.0;
    result.normal = vec3(0.0);
    
    vec3 edge1 = v1 - v0;
    vec3 edge2 = v2 - v0;
    vec3 h = cross(rayDir, edge2);
    float a = dot(edge1, h);
    
    if (abs(a) < EPSILON)
        return result;
    
    float f = 1.0 / a;
    vec3 s = rayOrigin - v0;
    float u = f * dot(s, h);
    
    if (u < 0.0 || u > 1.0)
        return result;
    
    vec3 q = cross(s, edge1);
    float v = f * dot(rayDir, q);
    
    if (v < 0.0 || u + v > 1.0)
        return result;
    
    float t = f * dot(edge2, q);
    
    if (t < EPSILON || t > INF)
        return result;
    
    result.t = t;
    result.u = u;
    result.v = v;
    vec3 n = cross(edge1, edge2);
    float len = length(n);
    if (len > EPSILON)
        result.normal = n / len;
    else
        result.normal = vec3(0.0, 1.0, 0.0);
    result.hit = true;
    return result;
}

uint GetPCGHash(inout uint seed)
{
    seed = seed * 747796405u + 2891336453u;
    uint word = ((seed >> ((seed >> 28u) + 4u)) ^ seed) * 277803737u;
    return (word >> 22u) ^ word;
}

float GetRandomFloat01()
{
    return float(GetPCGHash(rndSeed)) / 4294967296.0;
}

vec2 WorldToScreen(vec3 worldPos)
{
    vec4 viewPos = basicDataUBO.InvView * vec4(worldPos, 1.0);
    vec4 clipPos = basicDataUBO.InvProjection * viewPos;
    
    if (abs(clipPos.w) < 0.0001)
        return vec2(-1.0, -1.0);
    
    clipPos.xyz /= clipPos.w;
    return clipPos.xy * 0.5 + 0.5;
}

vec3 ReconstructPosition(vec2 uv, float depth)
{
    vec4 clipPos = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewPos = basicDataUBO.InvProjection * clipPos;
    
    if (abs(viewPos.w) < 0.0001)
        return vec3(0.0);
    
    viewPos.xyz /= viewPos.w;
    vec4 worldPos4 = basicDataUBO.InvView * vec4(viewPos.xyz, 1.0);
    
    return worldPos4.xyz;
}

vec3 CosineSampleHemisphere(vec3 normal)
{
    float u1 = GetRandomFloat01();
    float u2 = GetRandomFloat01();
    
    float r = sqrt(u1);
    float theta = 2.0 * PI * u2;
    
    float x = r * cos(theta);
    float y = r * sin(theta);
    float z = sqrt(max(0.0, 1.0 - u1));
    
    vec3 tangent = abs(normal.z) < 0.9 ? vec3(1.0, 0.0, 0.0) : vec3(0.0, 1.0, 0.0);
    vec3 bitangent = normalize(cross(normal, tangent));
    tangent = cross(bitangent, normal);
    
    return normalize(tangent * x + bitangent * y + normal * z);
}

HitInfo TraceRay(Ray ray)
{
    HitInfo hit;
    hit.hit = false;
    hit.t = 1e30;
    hit.position = vec3(0.0);
    hit.normal = vec3(0.0, 1.0, 0.0);
    hit.albedo = vec3(1.0);
    
    if (bvhNodeCount == 0 || triangleCount == 0)
        return hit;
    
    if (bvhNodes.length() < 9 || triangles.length() < 21)
        return hit;
    
    vec3 rayInvDir = vec3(
        abs(ray.direction.x) > 0.0001 ? 1.0 / ray.direction.x : (ray.direction.x >= 0.0 ? 1e30 : -1e30),
        abs(ray.direction.y) > 0.0001 ? 1.0 / ray.direction.y : (ray.direction.y >= 0.0 ? 1e30 : -1e30),
        abs(ray.direction.z) > 0.0001 ? 1.0 / ray.direction.z : (ray.direction.z >= 0.0 ? 1e30 : -1e30)
    );
    
    int stack[64];
    int stackPtr = 0;
    stack[stackPtr++] = 0;
    
    while (stackPtr > 0 && stackPtr < 64)
    {
        int nodeIndex = stack[--stackPtr];
        if (nodeIndex < 0 || nodeIndex >= bvhNodeCount)
            continue;
            
        int nodeOffset = nodeIndex * 9;
        if (nodeOffset + 8 >= bvhNodes.length())
            continue;
        
        vec3 nodeMin = vec3(bvhNodes[nodeOffset + 0], bvhNodes[nodeOffset + 1], bvhNodes[nodeOffset + 2]);
        vec3 nodeMax = vec3(bvhNodes[nodeOffset + 3], bvhNodes[nodeOffset + 4], bvhNodes[nodeOffset + 5]);
        
        AABBIntersectResult aabbResult = RayAABBIntersect(ray.origin, rayInvDir, nodeMin, nodeMax);
        if (!aabbResult.hit)
            continue;
        
        float closestT = aabbResult.tMin < 0.0 ? 0.0 : aabbResult.tMin;
        if (closestT > hit.t)
            continue;
        
        int leftChild = int(bvhNodes[nodeOffset + 6]);
        int rightChild = int(bvhNodes[nodeOffset + 7]);
        int triIndex = int(bvhNodes[nodeOffset + 8]);
        
        bool isLeaf = (leftChild < 0 && rightChild < 0);
        
        if (isLeaf && triIndex >= 0 && triIndex < triangleCount)
        {
            int triOffset = triIndex * 21;
            if (triOffset + 20 >= triangles.length())
                continue;
                
            vec3 v0 = vec3(triangles[triOffset + 0], triangles[triOffset + 1], triangles[triOffset + 2]);
            vec3 v1 = vec3(triangles[triOffset + 3], triangles[triOffset + 4], triangles[triOffset + 5]);
            vec3 v2 = vec3(triangles[triOffset + 6], triangles[triOffset + 7], triangles[triOffset + 8]);
            
            TriangleIntersectResult triResult = RayTriangleIntersect(ray.origin, ray.direction, v0, v1, v2);
            if (triResult.hit)
            {
                if (triResult.t > 0.0001 && triResult.t < hit.t)
                {
                    hit.hit = true;
                    hit.t = triResult.t;
                    hit.position = ray.origin + ray.direction * triResult.t;
                    
                    vec3 n0 = vec3(triangles[triOffset + 9], triangles[triOffset + 10], triangles[triOffset + 11]);
                    vec3 n1 = vec3(triangles[triOffset + 12], triangles[triOffset + 13], triangles[triOffset + 14]);
                    vec3 n2 = vec3(triangles[triOffset + 15], triangles[triOffset + 16], triangles[triOffset + 17]);
                    
                    float w = 1.0 - triResult.u - triResult.v;
                    vec3 baryNormal = n0 * w + n1 * triResult.u + n2 * triResult.v;
                    hit.normal = normalize(baryNormal);
                    
                    hit.albedo = vec3(triangles[triOffset + 18], triangles[triOffset + 19], triangles[triOffset + 20]);
                }
            }
        }
        else
        {
            if (rightChild >= 0 && rightChild < bvhNodeCount)
                stack[stackPtr++] = rightChild;
            if (leftChild >= 0 && leftChild < bvhNodeCount)
                stack[stackPtr++] = leftChild;
        }
    }
    
    return hit;
}

bool TraceShadowRay(vec3 origin, vec3 direction, float maxDist)
{
    if (bvhNodeCount == 0 || triangleCount == 0)
        return true;
    
    float distToOrigin = length(origin - basicDataUBO.ViewPos);
    float adaptiveBias = max(0.001, distToOrigin * 0.0001);
    float minDist = adaptiveBias;
    float maxDistCheck = maxDist;
    
    Ray ray;
    ray.origin = origin;
    ray.direction = normalize(direction);
    
    vec3 rayInvDir = vec3(
        abs(ray.direction.x) > 0.0001 ? 1.0 / ray.direction.x : (ray.direction.x >= 0.0 ? 1e30 : -1e30),
        abs(ray.direction.y) > 0.0001 ? 1.0 / ray.direction.y : (ray.direction.y >= 0.0 ? 1e30 : -1e30),
        abs(ray.direction.z) > 0.0001 ? 1.0 / ray.direction.z : (ray.direction.z >= 0.0 ? 1e30 : -1e30)
    );
    
    int stack[32];
    int stackPtr = 0;
    stack[stackPtr++] = 0;
    
    while (stackPtr > 0 && stackPtr < 32)
    {
        int nodeIndex = stack[--stackPtr];
        if (nodeIndex < 0 || nodeIndex >= bvhNodeCount)
            continue;
            
        int nodeOffset = nodeIndex * 9;
        if (nodeOffset + 8 >= bvhNodes.length())
            continue;
        
        vec3 nodeMin = vec3(bvhNodes[nodeOffset + 0], bvhNodes[nodeOffset + 1], bvhNodes[nodeOffset + 2]);
        vec3 nodeMax = vec3(bvhNodes[nodeOffset + 3], bvhNodes[nodeOffset + 4], bvhNodes[nodeOffset + 5]);
        
        AABBIntersectResult aabbResult = RayAABBIntersect(ray.origin, rayInvDir, nodeMin, nodeMax);
        if (!aabbResult.hit)
            continue;
        
        float tMin = max(aabbResult.tMin, minDist);
        float tMax = min(aabbResult.tMax, maxDistCheck);
        
        if (tMin > tMax)
            continue;
        
        int leftChild = int(bvhNodes[nodeOffset + 6]);
        int rightChild = int(bvhNodes[nodeOffset + 7]);
        int triIndex = int(bvhNodes[nodeOffset + 8]);
        
        bool isLeaf = (leftChild < 0 && rightChild < 0);
        
        if (isLeaf && triIndex >= 0 && triIndex < triangleCount)
        {
            int triOffset = triIndex * 21;
            if (triOffset + 20 < triangles.length())
            {
                vec3 v0 = vec3(triangles[triOffset + 0], triangles[triOffset + 1], triangles[triOffset + 2]);
                vec3 v1 = vec3(triangles[triOffset + 3], triangles[triOffset + 4], triangles[triOffset + 5]);
                vec3 v2 = vec3(triangles[triOffset + 6], triangles[triOffset + 7], triangles[triOffset + 8]);
                
                TriangleIntersectResult triResult = RayTriangleIntersect(ray.origin, ray.direction, v0, v1, v2);
                if (triResult.hit && triResult.t > minDist && triResult.t < maxDistCheck)
                {
                    return false;
                }
            }
        }
        else
        {
            if (rightChild >= 0 && rightChild < bvhNodeCount)
                stack[stackPtr++] = rightChild;
            if (leftChild >= 0 && leftChild < bvhNodeCount)
                stack[stackPtr++] = leftChild;
        }
    }
    
    return true;
}

vec3 GetSkyColor(vec3 direction)
{
    float horizonFactor = max(direction.y, 0.0);
    vec3 skyColor = mix(vec3(0.1, 0.1, 0.15), vec3(0.5, 0.7, 1.0) * 1.2, horizonFactor);
    return skyColor * 0.6;
}

vec3 SampleDirectLight(vec3 position, vec3 normal, vec3 lightDir, vec3 lightColor)
{
    if (length(lightColor) < 0.001)
        return vec3(0.0);
    
    vec3 nld = normalize(-lightDir);
    float NdotL = max(dot(normal, nld), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);
    
    float distToOrigin = length(position - basicDataUBO.ViewPos);
    float adaptiveBias = max(0.001, distToOrigin * 0.0001);
    vec3 shadowRayOrigin = position + normal * adaptiveBias;
    float lightDistance = 10000.0;
    
    bool visible = TraceShadowRay(shadowRayOrigin, nld, lightDistance);
    if (!visible)
        return vec3(0.0);
    
    return lightColor * NdotL / TWO_OVER_PI;
}

vec3 Radiance(Ray ray, vec3 lightDir, out float directLightAmount)
{
    vec3 outColor = vec3(0.0);
    vec3 color = vec3(1.0);
    directLightAmount = 0.0;
    
    vec3 inrd;
    HitInfo hit;
    
    for (int i = 0; i < rayDepth; i++)
    {
        if (i == 0)
        {
            hit = TraceRay(ray);
            inrd = ray.direction;
        }
        else
        {
            inrd = ray.direction;
            hit = TraceRay(ray);
        }
        
        vec3 normal = hit.normal;
        float epsilon = 0.1;
        vec3 position = hit.position + normal * epsilon;
        
        if (!hit.hit)
        {
            outColor += color * GetSkyColor(inrd);
            break;
        }
        
        vec3 albedo = hit.albedo;
        vec3 albedoDemodulated = albedo / TWO_OVER_PI;
        
        vec3 lightContrib = vec3(0.0);
        if (enableDirectLight != 0)
        {
            if (enableShadows != 0)
            {
                lightContrib = SampleDirectLight(position, normal, lightDir, basicDataUBO.LightColor) * color * albedoDemodulated;
            }
            else
            {
                vec3 nld = normalize(-lightDir);
                float NdotL = max(dot(normal, nld), 0.0);
                if (NdotL > 0.0)
                {
                    lightContrib = basicDataUBO.LightColor * NdotL * color * albedoDemodulated / TWO_OVER_PI;
                }
            }
            outColor += lightContrib;
            
            if (i == 0)
            {
                directLightAmount = length(lightContrib);
            }
        }
        
        if (i < rayDepth - 1 && enableReflections != 0 && i == 0)
        {
            if (shouldUpdateReflections == 0)
            {
                break;
            }
            
            vec3 indirectLight = vec3(0.0);
            
            vec3 rd = CosineSampleHemisphere(normal);
            float pdf = max(dot(normal, rd), 0.0) / PI;
            
            if (pdf > 1e-7)
            {
                float NdotRd = max(abs(dot(normal, rd)), 1e-7);
                vec3 throughput = albedoDemodulated * NdotRd / pdf;
                
                if (enableDirectLight != 0)
                {
                    vec3 nextPosition = position + rd * 0.1;
                    HitInfo nextHit = TraceRay(Ray(nextPosition, normalize(rd)));
                    
                    if (nextHit.hit)
                    {
                        vec3 nextNormal = nextHit.normal;
                        vec3 nextPos = nextHit.position + nextNormal * 0.1;
                        
                        vec3 lightContrib = vec3(0.0);
                        if (enableShadows != 0)
                        {
                            lightContrib = SampleDirectLight(nextPos, nextNormal, lightDir, basicDataUBO.LightColor);
                        }
                        else
                        {
                            vec3 nld = normalize(-lightDir);
                            float NdotL = max(dot(nextNormal, nld), 0.0);
                            if (NdotL > 0.0)
                            {
                                lightContrib = basicDataUBO.LightColor * NdotL / TWO_OVER_PI;
                            }
                        }
                        
                        indirectLight = lightContrib * nextHit.albedo / TWO_OVER_PI;
                    }
                    else
                    {
                        indirectLight = GetSkyColor(rd);
                    }
                }
                
                outColor += color * throughput * indirectLight;
            }
            
            break;
        }
        else if (i < rayDepth - 1)
        {
            break;
        }
        else
        {
            break;
        }
        
        float maxColor = max(max(color.x, color.y), color.z);
        if (maxColor < 0.01)
            break;
        
        float terminationProb = 1.0 - min(maxColor, 0.95);
        if (GetRandomFloat01() < terminationProb)
            break;
        
        color /= (1.0 - terminationProb);
    }
    
    return outColor;
}

vec3 ACESToneMapping(vec3 color)
{
    const float A = 2.51;
    const float B = 0.03;
    const float C = 2.43;
    const float D = 0.59;
    const float E = 0.14;
    return clamp((color * (A * color + B)) / (color * (C * color + D) + E), 0.0, 1.0);
}

void main()
{
    ivec2 imgResultSize = imageSize(ImgResult);
    ivec2 imgCoord = ivec2(gl_GlobalInvocationID.xy);
    
    if (imgCoord.x >= imgResultSize.x || imgCoord.y >= imgResultSize.y)
        return;
    
    rndSeed = gl_GlobalInvocationID.x * 1973 + gl_GlobalInvocationID.y * 9277 + thisRendererFrame * 2699 | 1;
    
    vec2 uv = (imgCoord + vec2(0.5)) / imgResultSize;
    float depth = texture(DepthTexture, uv).r;
    vec3 originalColor = texture(ColorTexture, uv).rgb;
    
    if (depth >= 1.0 - EPSILON)
    {
        vec3 lastFrameColor = imageLoad(ImgResult, imgCoord).rgb;
        float blendFactor = 0.15;
        vec3 finalColor = mix(lastFrameColor, originalColor, blendFactor);
        imageStore(ImgResult, imgCoord, vec4(finalColor, 1.0));
        return;
    }
    
    vec3 position = ReconstructPosition(uv, depth);
    
    Ray primaryRay;
    primaryRay.origin = basicDataUBO.ViewPos;
    primaryRay.direction = normalize(position - basicDataUBO.ViewPos);
    
    vec3 lightDir = normalize(basicDataUBO.LightDirection);
    
    int actualSPP = max(1, min(SPP, 8));
    vec3 irradiance = vec3(0.0);
    float avgDirectLight = 0.0;
    
    bool shouldUpdateReflections = (sceneHash != lastSceneHash) || thisRendererFrame < 2;
    int totalSPP = actualSPP;
    if (enableReflections != 0 && !shouldUpdateReflections)
    {
        totalSPP = max(1, actualSPP / 2);
    }
    
    for (int i = 0; i < totalSPP; i++)
    {
        vec2 jitter = vec2(GetRandomFloat01(), GetRandomFloat01()) - 0.5;
        vec2 uvOffset = (imgCoord + vec2(0.5) + jitter) / imgResultSize;
        float depth = texture(DepthTexture, uvOffset).r;
        
        if (depth >= 1.0 - EPSILON)
        {
            continue;
        }
        
        vec3 position = ReconstructPosition(uvOffset, depth);
        Ray ray;
        ray.origin = basicDataUBO.ViewPos;
        vec3 rayDir = normalize(position - basicDataUBO.ViewPos);
        
        vec2 randomOffset = vec2(GetRandomFloat01() - 0.5, GetRandomFloat01() - 0.5) * 0.002;
        vec3 offset = cross(rayDir, vec3(0.0, 1.0, 0.0));
        if (length(offset) < 0.1)
            offset = cross(rayDir, vec3(1.0, 0.0, 0.0));
        offset = normalize(offset);
        vec3 bitangent = normalize(cross(rayDir, offset));
        ray.direction = normalize(rayDir + offset * randomOffset.x + bitangent * randomOffset.y);
        
        float directLight;
        irradiance += Radiance(ray, lightDir, directLight);
        avgDirectLight += directLight;
    }
    
    if (totalSPP != actualSPP && totalSPP > 0)
    {
        irradiance *= float(actualSPP) / float(totalSPP);
        avgDirectLight *= float(actualSPP) / float(totalSPP);
    }
    
    if (totalSPP > 0)
    {
        irradiance /= float(totalSPP);
        avgDirectLight /= float(totalSPP);
    }
    
    float shadowDarkness = 1.0 - smoothstep(0.0, 0.15, avgDirectLight);
    shadowDarkness = mix(1.0, 0.0, shadowDarkness);
    irradiance *= shadowDarkness;
    
    vec3 denoisedIrradiance = irradiance;
    
    if (thisRendererFrame >= 1)
    {
        vec3 spatialFiltered = vec3(0.0);
        float totalWeight = 0.0;
        float centerDepth = depth;
        vec3 centerPos = position;
        float centerLuminance = dot(irradiance, vec3(0.299, 0.587, 0.114));
        
        float luminanceVariance = 0.0;
        int sampleCount = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                ivec2 sampleCoord = imgCoord + ivec2(dx, dy);
                if (sampleCoord.x < 0 || sampleCoord.x >= imgResultSize.x || 
                    sampleCoord.y < 0 || sampleCoord.y >= imgResultSize.y)
                    continue;
                
                vec2 sampleUV = (vec2(sampleCoord) + vec2(0.5)) / imgResultSize;
                float sampleDepth = texture(DepthTexture, sampleUV).r;
                
                if (sampleDepth >= 1.0 - EPSILON)
                    continue;
                
                vec3 sampleColor = imageLoad(ImgResult, sampleCoord).rgb;
                float sampleLuminance = dot(sampleColor, vec3(0.299, 0.587, 0.114));
                float diff = abs(sampleLuminance - centerLuminance);
                luminanceVariance += diff;
                sampleCount++;
            }
        }
        
        if (sampleCount > 0)
        {
            luminanceVariance /= float(sampleCount);
        }
        
        bool isReflection = luminanceVariance > 0.15;
        int radius = isReflection ? 2 : 1;
        float blendFactor = isReflection ? 0.5 : 0.35;
        float luminanceWeightScale = isReflection ? 0.5 : 0.8;
        
        float kernel[3] = float[3](1.0, 0.666, 0.333);
        
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                ivec2 sampleCoord = imgCoord + ivec2(dx, dy);
                if (sampleCoord.x < 0 || sampleCoord.x >= imgResultSize.x || 
                    sampleCoord.y < 0 || sampleCoord.y >= imgResultSize.y)
                    continue;
                
                vec2 sampleUV = (vec2(sampleCoord) + vec2(0.5)) / imgResultSize;
                float sampleDepth = texture(DepthTexture, sampleUV).r;
                
                if (sampleDepth >= 1.0 - EPSILON)
                    continue;
                
                vec3 sampleColor = imageLoad(ImgResult, sampleCoord).rgb;
                
                if (dx == 0 && dy == 0)
                {
                    sampleColor = irradiance;
                }
                
                vec3 samplePos = ReconstructPosition(sampleUV, sampleDepth);
                float sampleLuminance = dot(sampleColor, vec3(0.299, 0.587, 0.114));
                
                float depthDiff = abs(sampleDepth - centerDepth);
                float posDist = length(samplePos - centerPos);
                int spatialDist = abs(dx) + abs(dy);
                float luminanceDiff = abs(sampleLuminance - centerLuminance);
                
                float depthWeight = exp(-depthDiff * 80.0);
                float posWeight = exp(-posDist * 1.5);
                float spatialKernel = spatialDist < 3 ? kernel[spatialDist] : 0.0;
                float luminanceWeight = exp(-luminanceDiff * luminanceWeightScale);
                
                float weight = depthWeight * posWeight * spatialKernel * luminanceWeight;
                spatialFiltered += sampleColor * weight;
                totalWeight += weight;
            }
        }
        
        if (totalWeight > 0.001)
        {
            spatialFiltered /= totalWeight;
            denoisedIrradiance = mix(irradiance, spatialFiltered, blendFactor);
        }
    }
    
    vec3 lastFrameColor = imageLoad(ImgResult, imgCoord).rgb;
    bool geometryChanged = (sceneHash != lastSceneHash);
    
    vec3 finalColor;
    if (length(lastFrameColor) < 0.001 || geometryChanged || thisRendererFrame < 1)
    {
        finalColor = denoisedIrradiance;
    }
    else
    {
        vec3 colorDiff = abs(denoisedIrradiance - lastFrameColor);
        float luminanceDiff = dot(colorDiff, vec3(0.299, 0.587, 0.114));
        
        float adaptiveWeight;
        if (luminanceDiff > 0.08)
        {
            adaptiveWeight = 1.0;
        }
        else
        {
            float frameWeight = 1.0 / float(thisRendererFrame + 1);
            adaptiveWeight = min(frameWeight * 2.0, 0.25);
        }
        
        finalColor = mix(lastFrameColor, denoisedIrradiance, adaptiveWeight);
    }
    
    finalColor = ACESToneMapping(finalColor);
    finalColor = pow(finalColor, vec3(1.0 / 2.2));
    
    imageStore(ImgResult, imgCoord, vec4(finalColor, 1.0));
}
";
        
        _computeShader = new Shader(computeShaderSource);
    }
    
    public void ResetRenderer()
    {
        _frameCount = 0;
    }

    public override void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height)
    {
        if (!Enabled || _computeShader == null || !_settings.Enabled || _camera == null)
            return;

        if (_currentScene != null)
        {
            UpdateScene(_currentScene);
        }

        if (_width != width || _height != height)
        {
            Resize(width, height);
        }

        UpdateBasicDataUBO();
        
        _computeShader.Use();
        _computeShader.SetInt("rayDepth", _settings.RayDepth);
        _computeShader.SetInt("SPP", _settings.SamplesPerPixel);
        _computeShader.SetInt("thisRendererFrame", _frameCount);
        _computeShader.SetVector2("screenSize", new Vector2(_width, _height));
        _computeShader.SetInt("triangleCount", _triangleCount);
        _computeShader.SetInt("bvhNodeCount", _bvhNodeCount);
        _computeShader.SetInt("sceneHash", _sceneHash);
        _computeShader.SetInt("lastSceneHash", _lastSceneHash);
        _computeShader.SetInt("enableDirectLight", _settings.EnableDirectLight ? 1 : 0);
        _computeShader.SetInt("enableShadows", _settings.EnableShadows ? 1 : 0);
        _computeShader.SetInt("enableReflections", _settings.EnableReflections ? 1 : 0);
        
        bool shouldUpdateReflections = (_sceneHash != _lastSceneHash) || _frameCount < 2;
        _computeShader.SetInt("shouldUpdateReflections", shouldUpdateReflections ? 1 : 0);
        
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, _basicDataUBO);
        
        if (_triangleSSBO != 0)
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _triangleSSBO);
        if (_bvhSSBO != 0)
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, _bvhSSBO);
        
        GL.BindImageTexture(0, _resultTexture, 0, false, 0, TextureAccess.ReadWrite, (SizedInternalFormat)PixelInternalFormat.Rgba32f);
        
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        int depthLocation = _computeShader.GetUniformLocation("DepthTexture");
        if (depthLocation >= 0)
        {
            GL.Uniform1(depthLocation, 0);
        }

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
        int colorLocation = _computeShader.GetUniformLocation("ColorTexture");
        if (colorLocation >= 0)
        {
            GL.Uniform1(colorLocation, 1);
        }
        
        int groupsX = (_width + LOCAL_SIZE_X - 1) / LOCAL_SIZE_X;
        int groupsY = (_height + LOCAL_SIZE_Y - 1) / LOCAL_SIZE_Y;
        GL.DispatchCompute(groupsX, groupsY, 1);

        GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit);

        _frameCount++;
    }

    public void Resize(int width, int height)
    {
        if (_width == width && _height == height)
            return;

        _width = width;
        _height = height;

        GL.BindTexture(TextureTarget.Texture2D, _resultTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, _width, _height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        
        _frameCount = 0;
    }


    protected override void DisposeEffect(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteTexture(_resultTexture);
            GL.DeleteBuffer(_basicDataUBO);
            GL.DeleteBuffer(_triangleSSBO);
            GL.DeleteBuffer(_bvhSSBO);
            _computeShader?.Dispose();
        }
    }

    private struct Triangle
    {
        public Vector3 V0, V1, V2;
        public Vector3 N0, N1, N2;
        public Vector3 Albedo;
    }

    private struct BVHNode
    {
        public Vector3 Min;
        public Vector3 Max;
        public int LeftChild;
        public int RightChild;
        public int TriangleIndex;
    }
}
