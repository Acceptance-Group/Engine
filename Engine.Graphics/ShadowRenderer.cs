using System;
using System.Collections.Generic;
using Engine.Core;
using Engine.Math;
using Engine.Renderer;
using Engine.Graphics;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Graphics;

public class ShadowRenderer : Disposable
{
    private readonly List<ShadowMap> _shadowMaps = new List<ShadowMap>();
    private readonly ShadowSettings _settings;
    private Shader? _shadowShader;
    private Matrix4 _lightViewProjection;
    private int _currentShadowMapResolution;
    
    private Matrix4[] _cascadeLightViewProjections = new Matrix4[4];
    private float[] _cascadeDepths = new float[4];

    public ShadowRenderer(ShadowSettings settings)
    {
        _settings = settings;
        CreateShadowShader();
    }

    private void CreateShadowShader()
    {
        string vertexShader = @"
#version 330 core
layout (location = 0) in vec3 aPosition;

uniform mat4 uLightSpaceMatrix;
uniform mat4 uModel;

void main()
{
    gl_Position = uLightSpaceMatrix * uModel * vec4(aPosition, 1.0);
}";

        string fragmentShader = @"
#version 330 core
void main()
{
}";

        _shadowShader = new Shader(vertexShader, fragmentShader);
    }

    public void RenderShadowMap(DirectionalLight light, Camera camera, Scene scene)
    {
        if (!_settings.Enabled || !light.CastShadows || _shadowShader == null)
            return;

        if (_settings.UseCascadedShadowMaps)
        {
            RenderCascadedShadowMaps(light, camera, scene);
        }
        else
        {
            RenderSingleShadowMap(light, camera, scene);
        }
    }

    private void RenderSingleShadowMap(DirectionalLight light, Camera camera, Scene scene)
    {
        if (_shadowMaps.Count == 0 || _currentShadowMapResolution != _settings.ShadowMapResolution)
        {
            if (_shadowMaps.Count > 0)
            {
                _shadowMaps[0].Dispose();
                _shadowMaps.Clear();
            }

            _shadowMaps.Add(new ShadowMap(_settings.ShadowMapResolution));
            _currentShadowMapResolution = _settings.ShadowMapResolution;
        }

        var shadowMap = _shadowMaps[0];
        _lightViewProjection = CalculateStableLightViewProjection(light, camera, camera.NearPlane, _settings.ShadowDistance);

        shadowMap.Bind();
        
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);
        GL.CullFace(CullFaceMode.Front);

        _shadowShader!.Use();
        _shadowShader.SetMatrix4("uLightSpaceMatrix", _lightViewProjection);

        RenderSceneToShadowMap(scene);

        GL.CullFace(CullFaceMode.Back);
        shadowMap.Unbind();
    }

    private void RenderCascadedShadowMaps(DirectionalLight light, Camera camera, Scene scene)
    {
        int cascadeCount = System.Math.Min(_settings.CascadeCount, 4);
        
        while (_shadowMaps.Count < cascadeCount || _currentShadowMapResolution != _settings.ShadowMapResolution)
        {
            if (_shadowMaps.Count > 0)
            {
                foreach (var shadowMap in _shadowMaps)
                {
                    shadowMap.Dispose();
                }
                _shadowMaps.Clear();
            }

            for (int i = 0; i < cascadeCount; i++)
            {
                _shadowMaps.Add(new ShadowMap(_settings.ShadowMapResolution));
            }
            _currentShadowMapResolution = _settings.ShadowMapResolution;
        }

        float[] cascadeSplits = CalculateCascadeSplits(camera.NearPlane, _settings.ShadowDistance, cascadeCount);

        for (int i = 0; i < cascadeCount; i++)
        {
            float nearSplit = cascadeSplits[i];
            float farSplit = cascadeSplits[i + 1];
            
            _cascadeDepths[i] = farSplit;
            _cascadeLightViewProjections[i] = CalculateStableLightViewProjection(light, camera, nearSplit, farSplit);

            var shadowMap = _shadowMaps[i];
            shadowMap.Bind();
            
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.CullFace(CullFaceMode.Front);

            _shadowShader!.Use();
            _shadowShader.SetMatrix4("uLightSpaceMatrix", _cascadeLightViewProjections[i]);

            RenderSceneToShadowMap(scene);

            GL.CullFace(CullFaceMode.Back);
            shadowMap.Unbind();
        }

        _lightViewProjection = _cascadeLightViewProjections[0];
    }

    private float[] CalculateCascadeSplits(float nearPlane, float farPlane, int cascadeCount)
    {
        float[] splits = new float[cascadeCount + 1];
        splits[0] = nearPlane;
        splits[cascadeCount] = farPlane;

        float lambda = 0.95f;
        
        for (int i = 1; i < cascadeCount; i++)
        {
            float ratio = (float)i / cascadeCount;
            float logSplit = nearPlane * (float)System.Math.Pow(farPlane / nearPlane, ratio);
            float uniformSplit = nearPlane + (farPlane - nearPlane) * ratio;
            splits[i] = (float)(lambda * logSplit + (1.0f - lambda) * uniformSplit);
        }

        return splits;
    }

    private void RenderSceneToShadowMap(Scene scene)
    {
        var renderers = scene.FindObjectsOfType<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.Enabled && renderer.GameObject?.Active == true && renderer.Transform != null)
            {
                _shadowShader!.SetMatrix4("uModel", renderer.Transform.WorldMatrix);
                if (renderer.Mesh != null)
                {
                    renderer.Mesh.Draw();
                }
            }
        }
    }

    private Matrix4 CalculateStableLightViewProjection(DirectionalLight light, Camera camera, float nearSplit, float farSplit)
    {
        Vector3 lightDir = light.Direction.Normalized();
        Vector3 up = MathF.Abs(Vector3.Dot(lightDir, Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
        
        float fov = camera.FOV;
        float aspect = camera.AspectRatio;
        float tanHalfFov = MathF.Tan(fov * 0.5f);
        
        Vector3[] frustumCorners = new Vector3[8];
        
        float nearHeight = tanHalfFov * nearSplit;
        float nearWidth = nearHeight * aspect;
        float farHeight = tanHalfFov * farSplit;
        float farWidth = farHeight * aspect;
        
        Vector3 nearCenter = camera.Position + camera.Forward * nearSplit;
        Vector3 farCenter = camera.Position + camera.Forward * farSplit;
        
        frustumCorners[0] = nearCenter + camera.Right * nearWidth + camera.Up * nearHeight;
        frustumCorners[1] = nearCenter + camera.Right * nearWidth - camera.Up * nearHeight;
        frustumCorners[2] = nearCenter - camera.Right * nearWidth + camera.Up * nearHeight;
        frustumCorners[3] = nearCenter - camera.Right * nearWidth - camera.Up * nearHeight;
        frustumCorners[4] = farCenter + camera.Right * farWidth + camera.Up * farHeight;
        frustumCorners[5] = farCenter + camera.Right * farWidth - camera.Up * farHeight;
        frustumCorners[6] = farCenter - camera.Right * farWidth + camera.Up * farHeight;
        frustumCorners[7] = farCenter - camera.Right * farWidth - camera.Up * farHeight;
        
        Vector3 frustumCenter = Vector3.Zero;
        for (int i = 0; i < 8; i++)
        {
            frustumCenter += frustumCorners[i];
        }
        frustumCenter /= 8.0f;
        
        Vector3 lightPos = frustumCenter;
        Vector3 target = frustumCenter + lightDir;
        
        Matrix4 lightView = Matrix4.CreateLookAt(lightPos, target, up);
        
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        
        for (int i = 0; i < 8; i++)
        {
            Vector4 cornerInLightSpace = lightView * new Vector4(frustumCorners[i], 1.0f);
            if (cornerInLightSpace.X < minX) minX = cornerInLightSpace.X;
            if (cornerInLightSpace.X > maxX) maxX = cornerInLightSpace.X;
            if (cornerInLightSpace.Y < minY) minY = cornerInLightSpace.Y;
            if (cornerInLightSpace.Y > maxY) maxY = cornerInLightSpace.Y;
            if (cornerInLightSpace.Z < minZ) minZ = cornerInLightSpace.Z;
            if (cornerInLightSpace.Z > maxZ) maxZ = cornerInLightSpace.Z;
        }
        
        float orthoWidth = maxX - minX;
        float orthoHeight = maxY - minY;
        float orthoSize = System.Math.Max(orthoWidth, orthoHeight);
        
        if (orthoSize < 0.1f)
            orthoSize = 100.0f;
        
        float padding = System.Math.Max(orthoSize * 0.2f, 10.0f);
        orthoSize += padding * 2.0f;
        
        float texelSize = orthoSize / _settings.ShadowMapResolution;
        
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        
        minX = (float)System.Math.Floor((centerX - orthoSize * 0.5f) / texelSize) * texelSize;
        minY = (float)System.Math.Floor((centerY - orthoSize * 0.5f) / texelSize) * texelSize;
        maxX = (float)System.Math.Ceiling((centerX + orthoSize * 0.5f) / texelSize) * texelSize;
        maxY = (float)System.Math.Ceiling((centerY + orthoSize * 0.5f) / texelSize) * texelSize;
        
        orthoWidth = maxX - minX;
        orthoHeight = maxY - minY;
        orthoSize = System.Math.Max(orthoWidth, orthoHeight);
        
        float zRange = maxZ - minZ;
        float zPadding = System.Math.Max(zRange * 0.1f, 50.0f);
        
        float orthoNear = minZ - zPadding;
        float orthoFar = maxZ + zPadding;
        
        if (orthoNear >= orthoFar)
        {
            float zCenter = (minZ + maxZ) * 0.5f;
            orthoNear = zCenter - 100.0f;
            orthoFar = zCenter + 100.0f;
        }
        
        Vector4 snappedCenter4 = lightView.Inverse() * new Vector4((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f, 1.0f);
        Vector3 snappedCenter = new Vector3(snappedCenter4.X, snappedCenter4.Y, snappedCenter4.Z);
        
        lightView = Matrix4.CreateLookAt(snappedCenter, snappedCenter + lightDir, up);
        
        Matrix4 lightProjection = Matrix4.CreateOrthographic(orthoSize, orthoSize, orthoNear, orthoFar);

        return lightProjection * lightView;
    }

    public void BindShadowMap(Shader shader, int textureUnit = 1)
    {
        if (_shadowMaps.Count == 0 || !_settings.Enabled)
        {
            shader.SetInt("uUseShadows", 0);
            return;
        }

        if (_settings.UseCascadedShadowMaps)
        {
            BindCascadedShadowMaps(shader, textureUnit);
        }
        else
        {
            BindSingleShadowMap(shader, textureUnit);
        }
    }

    private void BindSingleShadowMap(Shader shader, int textureUnit)
    {
        _shadowMaps[0].BindTexture(textureUnit);
        shader.SetInt("uShadowMap", textureUnit);
        shader.SetMatrix4("uLightSpaceMatrix", _lightViewProjection);
        shader.SetMatrix4("uLightSpaceMatrix0", _lightViewProjection);
        shader.SetMatrix4("uLightSpaceMatrix1", _lightViewProjection);
        shader.SetMatrix4("uLightSpaceMatrix2", _lightViewProjection);
        shader.SetMatrix4("uLightSpaceMatrix3", _lightViewProjection);
        shader.SetFloat("uShadowBias", _settings.DepthBias);
        shader.SetFloat("uNormalBias", _settings.NormalBias);
        shader.SetFloat("uShadowOpacity", _settings.ShadowOpacity);
        shader.SetInt("uSoftShadows", _settings.SoftShadows ? 1 : 0);
        shader.SetInt("uShadowQuality", (int)_settings.Quality);
        shader.SetInt("uUseShadows", 1);
        shader.SetInt("uUseCascadedShadows", 0);
    }

    private void BindCascadedShadowMaps(Shader shader, int startTextureUnit)
    {
        int cascadeCount = System.Math.Min(_settings.CascadeCount, 4);
        
        for (int i = 0; i < cascadeCount; i++)
        {
            _shadowMaps[i].BindTexture(startTextureUnit + i);
            shader.SetInt($"uShadowMap{i}", startTextureUnit + i);
            shader.SetMatrix4($"uLightSpaceMatrix{i}", _cascadeLightViewProjections[i]);
        }
        
        shader.SetInt("uCascadeCount", cascadeCount);
        shader.SetFloat("uShadowBias", _settings.DepthBias);
        shader.SetFloat("uNormalBias", _settings.NormalBias);
        shader.SetFloat("uShadowOpacity", _settings.ShadowOpacity);
        shader.SetFloat("uCascadeBlendArea", _settings.CascadeBlendArea);
        shader.SetInt("uSoftShadows", _settings.SoftShadows ? 1 : 0);
        shader.SetInt("uShadowQuality", (int)_settings.Quality);
        
        for (int i = 0; i < cascadeCount; i++)
        {
            shader.SetFloat($"uCascadeDepth{i}", _cascadeDepths[i]);
        }
        
        shader.SetInt("uUseShadows", 1);
        shader.SetInt("uUseCascadedShadows", 1);
    }

    public Matrix4 LightViewProjection => _lightViewProjection;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var shadowMap in _shadowMaps)
            {
                shadowMap.Dispose();
            }
            _shadowMaps.Clear();
            _shadowShader?.Dispose();
        }
    }
}
