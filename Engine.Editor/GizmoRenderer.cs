using System;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;
using Engine.Graphics;
using Engine.Math;
using Engine.Renderer;

namespace Engine.Editor;

public class GizmoRenderer
{
    private Shader? _gizmoShader;
    private Mesh? _arrowMesh;
    private Mesh? _rotationArcMesh;
    private Mesh? _scaleHandleMesh;
    private Mesh? _scaleLineMesh;
    private Mesh? _centerCubeMesh;
    private const float GizmoSize = 1.0f;
    private const float ArrowLength = 0.8f;
    private const float ArrowHeadSize = 0.15f;
    private const float RotationArcRadius = 0.7f;
    private const int RotationArcSegments = 64;
    private const float BaseGizmoScale = 0.45f;

    public void Initialize()
    {
        CreateGizmoShader();
        CreateArrowMesh();
        CreateRotationArcMesh();
        CreateScaleHandleMesh();
        CreateScaleLineMesh();
        CreateCenterCubeMesh();
    }

    private void CreateGizmoShader()
    {
        string vertexShader = @"
#version 330 core
layout (location = 0) in vec3 aPosition;
uniform mat4 uMVP;
uniform vec4 uColor;
out vec4 vColor;
void main()
{
    gl_Position = uMVP * vec4(aPosition, 1.0);
    vColor = uColor;
}
";

        string fragmentShader = @"
#version 330 core
in vec4 vColor;
out vec4 FragColor;
void main()
{
    FragColor = vColor;
}
";

        _gizmoShader = new Shader(vertexShader, fragmentShader);
    }

    private void CreateArrowMesh()
    {
        var vertices = new System.Collections.Generic.List<float>();
        var indices = new System.Collections.Generic.List<uint>();

        float shaftLength = ArrowLength - ArrowHeadSize;
        float shaftRadius = 0.02f;
        int segments = 8;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)i / segments * 2.0f * MathF.PI;
            float angle2 = (float)(i + 1) / segments * 2.0f * MathF.PI;

            float x1 = MathF.Cos(angle1) * shaftRadius;
            float z1 = MathF.Sin(angle1) * shaftRadius;
            float x2 = MathF.Cos(angle2) * shaftRadius;
            float z2 = MathF.Sin(angle2) * shaftRadius;

            uint baseIndex = (uint)vertices.Count / 8;

            vertices.AddRange(new float[] { x1, 0, z1, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x2, 0, z2, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x1, shaftLength, z1, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x2, shaftLength, z2, 0, 1, 0, 0, 0 });

            indices.AddRange(new uint[] {
                baseIndex, baseIndex + 2, baseIndex + 1,
                baseIndex + 1, baseIndex + 2, baseIndex + 3
            });
        }

        float headBaseY = shaftLength;
        float headTopY = ArrowLength;
        float headBaseRadius = 0.05f;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)i / segments * 2.0f * MathF.PI;
            float angle2 = (float)(i + 1) / segments * 2.0f * MathF.PI;

            float x1 = MathF.Cos(angle1) * headBaseRadius;
            float z1 = MathF.Sin(angle1) * headBaseRadius;
            float x2 = MathF.Cos(angle2) * headBaseRadius;
            float z2 = MathF.Sin(angle2) * headBaseRadius;

            uint baseIndex = (uint)vertices.Count / 8;

            vertices.AddRange(new float[] { x1, headBaseY, z1, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x2, headBaseY, z2, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { 0, headTopY, 0, 0, 1, 0, 0, 0 });

            indices.AddRange(new uint[] {
                baseIndex, baseIndex + 1, baseIndex + 2
            });
        }

        _arrowMesh = new Mesh(vertices.ToArray(), indices.ToArray());
    }

    private void CreateRotationArcMesh()
    {
        var vertices = new System.Collections.Generic.List<float>();
        var indices = new System.Collections.Generic.List<uint>();

        float thickness = 0.18f;
        int segments = RotationArcSegments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * MathF.PI * 2.0f;
            float nextAngle = (float)(i + 1) / segments * MathF.PI * 2.0f;

            float x1 = MathF.Cos(angle) * RotationArcRadius;
            float y1 = MathF.Sin(angle) * RotationArcRadius;
            float x2 = MathF.Cos(nextAngle) * RotationArcRadius;
            float y2 = MathF.Sin(nextAngle) * RotationArcRadius;

            Vector3 dir1 = new Vector3(x1, y1, 0).Normalized();
            Vector3 dir2 = new Vector3(x2, y2, 0).Normalized();
            Vector3 perp1 = new Vector3(-dir1.Y, dir1.X, 0) * thickness;
            Vector3 perp2 = new Vector3(-dir2.Y, dir2.X, 0) * thickness;

            uint baseIndex = (uint)vertices.Count / 8;

            vertices.AddRange(new float[] { x1 + perp1.X, y1 + perp1.Y, 0, 0, 0, 1, 0, 0 });
            vertices.AddRange(new float[] { x1 - perp1.X, y1 - perp1.Y, 0, 0, 0, 1, 0, 0 });
            vertices.AddRange(new float[] { x2 + perp2.X, y2 + perp2.Y, 0, 0, 0, 1, 0, 0 });
            vertices.AddRange(new float[] { x2 - perp2.X, y2 - perp2.Y, 0, 0, 0, 1, 0, 0 });

            if (i < segments)
            {
                indices.AddRange(new uint[] {
                    baseIndex, baseIndex + 2, baseIndex + 1,
                    baseIndex + 1, baseIndex + 2, baseIndex + 3
                });
            }
        }

        _rotationArcMesh = new Mesh(vertices.ToArray(), indices.ToArray());
    }

    private void CreateScaleHandleMesh()
    {
        float size = 0.1f;
        float[] vertices = new float[]
        {
            -size, -size, -size, 0, 0, -1, 0, 0,
            size, -size, -size, 0, 0, -1, 0, 0,
            size, size, -size, 0, 0, -1, 0, 0,
            -size, size, -size, 0, 0, -1, 0, 0,
            -size, -size, size, 0, 0, 1, 0, 0,
            size, -size, size, 0, 0, 1, 0, 0,
            size, size, size, 0, 0, 1, 0, 0,
            -size, size, size, 0, 0, 1, 0, 0
        };

        uint[] indices = new uint[]
        {
            0, 1, 2, 2, 3, 0,
            4, 5, 6, 6, 7, 4,
            0, 1, 5, 5, 4, 0,
            2, 3, 7, 7, 6, 2,
            0, 3, 7, 7, 4, 0,
            1, 2, 6, 6, 5, 1
        };

        _scaleHandleMesh = new Mesh(vertices, indices);
    }

    private void CreateScaleLineMesh()
    {
        var vertices = new System.Collections.Generic.List<float>();
        var indices = new System.Collections.Generic.List<uint>();

        float thickness = 0.015f;
        int segments = 8;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)i / segments * 2.0f * MathF.PI;
            float angle2 = (float)(i + 1) / segments * 2.0f * MathF.PI;

            float x1 = MathF.Cos(angle1) * thickness;
            float z1 = MathF.Sin(angle1) * thickness;
            float x2 = MathF.Cos(angle2) * thickness;
            float z2 = MathF.Sin(angle2) * thickness;

            uint baseIndex = (uint)vertices.Count / 8;

            vertices.AddRange(new float[] { x1, 0, z1, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x2, 0, z2, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x1, ArrowLength, z1, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x2, ArrowLength, z2, 0, 1, 0, 0, 0 });

            indices.AddRange(new uint[] {
                baseIndex, baseIndex + 2, baseIndex + 1,
                baseIndex + 1, baseIndex + 2, baseIndex + 3
            });
        }

        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)i / segments * 2.0f * MathF.PI;
            float angle2 = (float)(i + 1) / segments * 2.0f * MathF.PI;

            float x1 = MathF.Cos(angle1) * thickness;
            float z1 = MathF.Sin(angle1) * thickness;
            float x2 = MathF.Cos(angle2) * thickness;
            float z2 = MathF.Sin(angle2) * thickness;

            uint baseIndex = (uint)vertices.Count / 8;

            vertices.AddRange(new float[] { x1, 0, z1, 0, -1, 0, 0, 0 });
            vertices.AddRange(new float[] { x2, 0, z2, 0, -1, 0, 0, 0 });
            vertices.AddRange(new float[] { 0, 0, 0, 0, -1, 0, 0, 0 });

            indices.AddRange(new uint[] {
                baseIndex, baseIndex + 1, baseIndex + 2
            });

            vertices.AddRange(new float[] { x1, ArrowLength, z1, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { x2, ArrowLength, z2, 0, 1, 0, 0, 0 });
            vertices.AddRange(new float[] { 0, ArrowLength, 0, 0, 1, 0, 0, 0 });

            baseIndex = (uint)vertices.Count / 8 - 3;
            indices.AddRange(new uint[] {
                baseIndex, baseIndex + 1, baseIndex + 2
            });
        }

        _scaleLineMesh = new Mesh(vertices.ToArray(), indices.ToArray());
    }

    private void CreateCenterCubeMesh()
    {
        float size = 0.08f;
        float[] vertices = new float[]
        {
            -size, -size, -size, 0, 0, -1, 0, 0,
            size, -size, -size, 0, 0, -1, 0, 0,
            size, size, -size, 0, 0, -1, 0, 0,
            -size, size, -size, 0, 0, -1, 0, 0,
            -size, -size, size, 0, 0, 1, 0, 0,
            size, -size, size, 0, 0, 1, 0, 0,
            size, size, size, 0, 0, 1, 0, 0,
            -size, size, size, 0, 0, 1, 0, 0
        };

        uint[] indices = new uint[]
        {
            0, 1, 2, 2, 3, 0,
            4, 5, 6, 6, 7, 4,
            0, 1, 5, 5, 4, 0,
            2, 3, 7, 7, 6, 2,
            0, 3, 7, 7, 4, 0,
            1, 2, 6, 6, 5, 1
        };

        _centerCubeMesh = new Mesh(vertices, indices);
    }

    private float CalculateGizmoScale(Vector3 position, Camera camera)
    {
        float distance = Vector3.Distance(camera.Position, position);
        float tanHalfFov = MathF.Tan(camera.FOV * 0.5f);
        float scale = distance * tanHalfFov * BaseGizmoScale;
        return MathF.Max(0.01f, scale);
    }

    public void RenderPositionGizmo(Transform transform, Camera camera, Matrix4 viewProjection, int selectedAxis = -1)
    {
        if (_gizmoShader == null || _arrowMesh == null || _centerCubeMesh == null)
            return;

        Vector3 position = transform.WorldPosition;
        float scale = CalculateGizmoScale(position, camera);
        Matrix4 scaleMatrix = Matrix4.CreateScale(scale);
        Matrix4 translation = Matrix4.CreateTranslation(position);

        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(2.0f);

        _gizmoShader.Use();

        float xBrightness = selectedAxis == 0 ? 0.4f : 1.0f;
        float xAlpha = selectedAxis == 0 ? 0.5f : 1.0f;
        Matrix4 xRotation = Matrix4.CreateRotationZ(-MathF.PI * 0.5f);
        Matrix4 xTransform = translation * scaleMatrix * xRotation;
        Matrix4 xMVP = viewProjection * xTransform;
        _gizmoShader.SetMatrix4("uMVP", xMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(1 * xBrightness, 0, 0, xAlpha));
        _arrowMesh.Draw();

        float yBrightness = selectedAxis == 1 ? 0.4f : 1.0f;
        float yAlpha = selectedAxis == 1 ? 0.5f : 1.0f;
        Matrix4 yTransform = translation * scaleMatrix;
        Matrix4 yMVP = viewProjection * yTransform;
        _gizmoShader.SetMatrix4("uMVP", yMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 1 * yBrightness, 0, yAlpha));
        _arrowMesh.Draw();

        float zBrightness = selectedAxis == 2 ? 0.4f : 1.0f;
        float zAlpha = selectedAxis == 2 ? 0.5f : 1.0f;
        Matrix4 zRotation = Matrix4.CreateRotationX(MathF.PI * 0.5f);
        Matrix4 zTransform = translation * scaleMatrix * zRotation;
        Matrix4 zMVP = viewProjection * zTransform;
        _gizmoShader.SetMatrix4("uMVP", zMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 0, 1 * zBrightness, zAlpha));
        _arrowMesh.Draw();

        Matrix4 centerMVP = viewProjection * translation * scaleMatrix;
        _gizmoShader.SetMatrix4("uMVP", centerMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0.8f, 0.8f, 0.8f, 1.0f));
        _centerCubeMesh.Draw();

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
        GL.LineWidth(1.0f);
    }

    public void RenderRotationGizmo(Transform transform, Camera camera, Matrix4 viewProjection, int selectedAxis = -1)
    {
        if (_gizmoShader == null || _rotationArcMesh == null || _centerCubeMesh == null)
            return;

        Vector3 position = transform.WorldPosition;
        float scale = CalculateGizmoScale(position, camera);
        Matrix4 scaleMatrix = Matrix4.CreateScale(scale);
        Matrix4 translation = Matrix4.CreateTranslation(position);

        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _gizmoShader.Use();

        float xBrightness = selectedAxis == 0 ? 0.4f : 1.0f;
        float xAlpha = selectedAxis == 0 ? 0.5f : 1.0f;
        Matrix4 xRotation = Matrix4.CreateRotationY(MathF.PI * 0.5f);
        Matrix4 xTransform = translation * scaleMatrix * xRotation;
        Matrix4 xMVP = viewProjection * xTransform;
        _gizmoShader.SetMatrix4("uMVP", xMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(1 * xBrightness, 0, 0, xAlpha));
        _rotationArcMesh.Draw();

        float yBrightness = selectedAxis == 1 ? 0.4f : 1.0f;
        float yAlpha = selectedAxis == 1 ? 0.5f : 1.0f;
        Matrix4 yRotation = Matrix4.CreateRotationX(MathF.PI * 0.5f);
        Matrix4 yTransform = translation * scaleMatrix * yRotation;
        Matrix4 yMVP = viewProjection * yTransform;
        _gizmoShader.SetMatrix4("uMVP", yMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 1 * yBrightness, 0, yAlpha));
        _rotationArcMesh.Draw();

        float zBrightness = selectedAxis == 2 ? 0.4f : 1.0f;
        float zAlpha = selectedAxis == 2 ? 0.5f : 1.0f;
        Matrix4 zRotation = Matrix4.CreateRotationZ(MathF.PI);
        Matrix4 zTransform = translation * scaleMatrix * zRotation;
        Matrix4 zMVP = viewProjection * zTransform;
        _gizmoShader.SetMatrix4("uMVP", zMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 0, 1 * zBrightness, zAlpha));
        _rotationArcMesh.Draw();

        Matrix4 centerMVP = viewProjection * translation * scaleMatrix;
        _gizmoShader.SetMatrix4("uMVP", centerMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0.8f, 0.8f, 0.8f, 1.0f));
        _centerCubeMesh.Draw();

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
        GL.LineWidth(1.0f);
    }

    public void RenderScaleGizmo(Transform transform, Camera camera, Matrix4 viewProjection, int selectedAxis = -1)
    {
        if (_gizmoShader == null || _scaleHandleMesh == null || _scaleLineMesh == null || _centerCubeMesh == null)
            return;

        Vector3 position = transform.WorldPosition;
        float scale = CalculateGizmoScale(position, camera);
        Matrix4 scaleMatrix = Matrix4.CreateScale(scale);
        Matrix4 translation = Matrix4.CreateTranslation(position);

        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _gizmoShader.Use();

        float xBrightness = selectedAxis == 0 ? 0.4f : 1.0f;
        float xAlpha = selectedAxis == 0 ? 0.5f : 1.0f;
        Matrix4 xRotation = Matrix4.CreateRotationZ(-MathF.PI * 0.5f);
        Matrix4 xLineTransform = translation * scaleMatrix * xRotation;
        Matrix4 xLineMVP = viewProjection * xLineTransform;
        _gizmoShader.SetMatrix4("uMVP", xLineMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(1 * xBrightness, 0, 0, xAlpha));
        _scaleLineMesh.Draw();

        Vector3 xHandlePos = new Vector3(ArrowLength, 0, 0);
        Matrix4 xHandleTransform = translation * scaleMatrix * Matrix4.CreateTranslation(xHandlePos);
        Matrix4 xHandleMVP = viewProjection * xHandleTransform;
        _gizmoShader.SetMatrix4("uMVP", xHandleMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(1 * xBrightness, 0, 0, xAlpha));
        _scaleHandleMesh.Draw();

        float yBrightness = selectedAxis == 1 ? 0.4f : 1.0f;
        float yAlpha = selectedAxis == 1 ? 0.5f : 1.0f;
        Matrix4 yLineTransform = translation * scaleMatrix;
        Matrix4 yLineMVP = viewProjection * yLineTransform;
        _gizmoShader.SetMatrix4("uMVP", yLineMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 1 * yBrightness, 0, yAlpha));
        _scaleLineMesh.Draw();

        Vector3 yHandlePos = new Vector3(0, ArrowLength, 0);
        Matrix4 yHandleTransform = translation * scaleMatrix * Matrix4.CreateTranslation(yHandlePos);
        Matrix4 yHandleMVP = viewProjection * yHandleTransform;
        _gizmoShader.SetMatrix4("uMVP", yHandleMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 1 * yBrightness, 0, yAlpha));
        _scaleHandleMesh.Draw();

        float zBrightness = selectedAxis == 2 ? 0.4f : 1.0f;
        float zAlpha = selectedAxis == 2 ? 0.5f : 1.0f;
        Matrix4 zRotation = Matrix4.CreateRotationX(MathF.PI * 0.5f);
        Matrix4 zLineTransform = translation * scaleMatrix * zRotation;
        Matrix4 zLineMVP = viewProjection * zLineTransform;
        _gizmoShader.SetMatrix4("uMVP", zLineMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 0, 1 * zBrightness, zAlpha));
        _scaleLineMesh.Draw();

        Vector3 zHandlePos = new Vector3(0, ArrowLength, 0);
        Matrix4 zHandleTransform = translation * scaleMatrix * zRotation * Matrix4.CreateTranslation(zHandlePos);
        Matrix4 zHandleMVP = viewProjection * zHandleTransform;
        _gizmoShader.SetMatrix4("uMVP", zHandleMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0, 0, 1 * zBrightness, zAlpha));
        _scaleHandleMesh.Draw();

        Matrix4 centerMVP = viewProjection * translation * scaleMatrix;
        _gizmoShader.SetMatrix4("uMVP", centerMVP);
        _gizmoShader.SetVector4("uColor", new Engine.Math.Vector4(0.8f, 0.8f, 0.8f, 1.0f));
        _centerCubeMesh.Draw();

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
    }

    public void RenderAllGizmo(Transform transform, Camera camera, Matrix4 viewProjection)
    {
        RenderPositionGizmo(transform, camera, viewProjection);
    }

    private bool RayIntersectsCylinder(Vector3 rayOrigin, Vector3 rayDirection, Vector3 cylinderStart, Vector3 cylinderAxis, float radius, float length)
    {
        Vector3 toStart = cylinderStart - rayOrigin;
        Vector3 cross1 = Vector3.Cross(rayDirection, cylinderAxis);
        float cross1LenSq = cross1.LengthSquared;
        
        if (cross1LenSq < 0.0001f)
            return false;
        
        float dist = MathF.Abs(Vector3.Dot(toStart, cross1)) / MathF.Sqrt(cross1LenSq);
        if (dist > radius)
            return false;
        
        Vector3 cross2 = Vector3.Cross(toStart, cylinderAxis);
        float t = Vector3.Dot(cross2, cross1) / cross1LenSq;
        
        if (t < 0)
            return false;
        
        float projection = Vector3.Dot((rayOrigin + rayDirection * t) - cylinderStart, cylinderAxis);
        return projection >= 0 && projection <= length;
    }

    private bool RayIntersectsArc(Vector3 rayOrigin, Vector3 rayDirection, Vector3 center, Vector3 normal, float radius, float thickness)
    {
        float denom = Vector3.Dot(normal, rayDirection);
        if (MathF.Abs(denom) < 0.0001f)
            return false;
        
        float t = Vector3.Dot(normal, center - rayOrigin) / denom;
        if (t < 0)
            return false;
        
        Vector3 intersection = rayOrigin + rayDirection * t;
        Vector3 toIntersection = intersection - center;
        float dist = toIntersection.Length;
        
        return MathF.Abs(dist - radius) < thickness;
    }

    public bool RayIntersectsGizmo(Vector3 rayOrigin, Vector3 rayDirection, Transform transform, TransformGizmoMode mode, Camera camera, out int selectedAxis)
    {
        selectedAxis = -1;
        Vector3 position = transform.WorldPosition;
        float scale = CalculateGizmoScale(position, camera);
        float bestDistance = float.MaxValue;
        int bestAxis = -1;

        if (mode == TransformGizmoMode.Position)
        {
            float arrowRadius = 0.05f * scale;
            float arrowLength = ArrowLength * scale;
            
            Vector3[] axes = { Vector3.Right, Vector3.Up, Vector3.Backward };
            
            for (int i = 0; i < 3; i++)
            {
                Vector3 axisDir = axes[i];
                Vector3 arrowStart = position;
                
                if (RayIntersectsCylinder(rayOrigin, rayDirection, arrowStart, axisDir, arrowRadius, arrowLength))
                {
                    Vector3 toStart = arrowStart - rayOrigin;
                    float t = Vector3.Dot(toStart, rayDirection);
                    if (t >= 0 && t < bestDistance)
                    {
                        bestDistance = t;
                        bestAxis = i;
                    }
                }
            }
            
            selectedAxis = bestAxis;
        }
        else if (mode == TransformGizmoMode.Rotation)
        {
            float arcThickness = 0.12f * scale;
            float arcRadius = RotationArcRadius * scale;
            
            Vector3[] normals = {
                Vector3.Right,
                Vector3.Up,
                Vector3.Forward
            };
            
            for (int i = 0; i < 3; i++)
            {
                Vector3 normal = normals[i];
                
                if (RayIntersectsArc(rayOrigin, rayDirection, position, normal, arcRadius, arcThickness))
                {
                    float denom = Vector3.Dot(normal, rayDirection);
                    if (MathF.Abs(denom) > 0.0001f)
                    {
                        float t = Vector3.Dot(normal, position - rayOrigin) / denom;
                        if (t >= 0 && t < bestDistance)
                        {
                            bestDistance = t;
                            bestAxis = i;
                        }
                    }
                }
            }
            
            selectedAxis = bestAxis;
        }
        else if (mode == TransformGizmoMode.Scale)
        {
            float handleRadius = 0.12f * scale;
            float lineRadius = 0.03f * scale;
            float arrowLength = ArrowLength * scale;
            
            Vector3[] axes = { Vector3.Right, Vector3.Up, Vector3.Backward };
            
            for (int i = 0; i < 3; i++)
            {
                Vector3 axisDir = axes[i];
                Vector3 handlePos = position + axisDir * arrowLength;
                
                float t = float.MaxValue;
                bool hit = false;
                
                if (RayIntersectsCylinder(rayOrigin, rayDirection, position, axisDir, lineRadius, arrowLength))
                {
                    Vector3 toStart = position - rayOrigin;
                    float proj = Vector3.Dot(toStart, rayDirection);
                    if (proj >= 0)
                    {
                        t = proj;
                        hit = true;
                    }
                }
                
                if (RayIntersectsCylinder(rayOrigin, rayDirection, handlePos, axisDir, handleRadius, 0.1f))
                {
                    Vector3 toHandle = handlePos - rayOrigin;
                    float proj = Vector3.Dot(toHandle, rayDirection);
                    if (proj >= 0 && proj < t)
                    {
                        t = proj;
                        hit = true;
                    }
                }
                
                if (hit && t < bestDistance)
                {
                    bestDistance = t;
                    bestAxis = i;
                }
            }
            
            selectedAxis = bestAxis;
        }

        return selectedAxis >= 0;
    }

    public void Dispose()
    {
        _gizmoShader?.Dispose();
        _arrowMesh?.Dispose();
        _rotationArcMesh?.Dispose();
        _scaleHandleMesh?.Dispose();
        _scaleLineMesh?.Dispose();
        _centerCubeMesh?.Dispose();
    }
}

