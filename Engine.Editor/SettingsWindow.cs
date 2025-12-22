using System;
using ImGuiNET;
using Engine.Graphics;
using Engine.Physics;

namespace Engine.Editor;

public class SettingsWindow
{
    private readonly EditorApplication _editor;
    private bool _showWindow = true;

    public SettingsWindow(EditorApplication editor)
    {
        _editor = editor;
    }

    public void Render()
    {
        if (!_showWindow)
            return;

        if (ImGui.Begin("Settings", ref _showWindow))
        {
            if (ImGui.CollapsingHeader("Rendering"))
            {
                int pipeline = (int)_editor.RenderingPipeline;
                string[] pipelines = { "Default Rendering Pipeline", "Path Traced Rendering Pipeline" };
                ImGui.PushItemWidth(-1f);
                if (ImGui.Combo("##HiddenLabel", ref pipeline, pipelines, pipelines.Length))
                {
                    _editor.RenderingPipeline = (EditorApplication.RenderingPipelineType)pipeline;
                    if (_editor.PathTracingSettings != null)
                    {
                        _editor.PathTracingSettings.Enabled = (pipeline == 1);
                    }
                }
                ImGui.PopItemWidth();
            }

            bool shadowsEnabled = _editor.RenderingPipeline == EditorApplication.RenderingPipelineType.Default;
            if (shadowsEnabled)
            {
                if (ImGui.CollapsingHeader("Shadows"))
            {
                var shadowSettings = _editor.ShadowSettings;
                if (shadowSettings != null)
                {
                    bool enabled = shadowSettings.Enabled;
                    if (ImGui.Checkbox("Enable Shadows", ref enabled))
                    {
                        shadowSettings.Enabled = enabled;
                    }

                    int resolution = shadowSettings.ShadowMapResolution;
                    if (ImGui.SliderInt("Shadow Map Resolution", ref resolution, 512, 8192))
                    {
                        shadowSettings.ShadowMapResolution = resolution;
                    }

                    float depthBias = shadowSettings.DepthBias;
                    if (ImGui.SliderFloat("Depth Bias", ref depthBias, 0.0f, 0.1f))
                    {
                        shadowSettings.DepthBias = depthBias;
                    }

                    float normalBias = shadowSettings.NormalBias;
                    if (ImGui.SliderFloat("Normal Bias", ref normalBias, 0.0f, 0.1f))
                    {
                        shadowSettings.NormalBias = normalBias;
                    }

                    float shadowDistance = shadowSettings.ShadowDistance;
                    if (ImGui.SliderFloat("Shadow Distance", ref shadowDistance, 1.0f, 1000.0f))
                    {
                        shadowSettings.ShadowDistance = shadowDistance;
                    }

                    float shadowOpacity = shadowSettings.ShadowOpacity;
                    if (ImGui.SliderFloat("Shadow Opacity", ref shadowOpacity, 0.0f, 1.0f))
                    {
                        shadowSettings.ShadowOpacity = shadowOpacity;
                    }

                    ImGui.Separator();

                    bool softShadows = shadowSettings.SoftShadows;
                    if (ImGui.Checkbox("Soft Shadows", ref softShadows))
                    {
                        shadowSettings.SoftShadows = softShadows;
                    }

                    if (softShadows)
                    {
                        int quality = (int)shadowSettings.Quality;
                        string[] qualities = { "Low", "Medium", "High", "Ultra", "Cinematic" };
                        if (ImGui.Combo("Shadow Quality", ref quality, qualities, qualities.Length))
                        {
                            shadowSettings.Quality = (ShadowQuality)quality;
                        }
                    }

                    ImGui.Separator();

                    bool useCascaded = shadowSettings.UseCascadedShadowMaps;
                    if (ImGui.Checkbox("Use Cascaded Shadow Maps", ref useCascaded))
                    {
                        shadowSettings.UseCascadedShadowMaps = useCascaded;
                    }

                    if (useCascaded)
                    {
                        int cascadeCount = shadowSettings.CascadeCount;
                        if (ImGui.SliderInt("Cascade Count", ref cascadeCount, 1, 8))
                        {
                            shadowSettings.CascadeCount = cascadeCount;
                        }

                        float blendArea = shadowSettings.CascadeBlendArea;
                        if (ImGui.SliderFloat("Cascade Blend Area", ref blendArea, 0.0f, 0.2f))
                        {
                            shadowSettings.CascadeBlendArea = blendArea;
                        }

                        ImGui.Text("Cascade Splits:");
                        var splits = shadowSettings.CascadeSplits;
                        for (int i = 0; i < cascadeCount && i < splits.Length; i++)
                        {
                            float split = splits[i];
                            string label = $"Split {i}";
                            if (ImGui.SliderFloat(label, ref split, 0.0f, 1.0f))
                            {
                                splits[i] = split;
                            }
                        }
                    }
                }
                }
            }

            if (ImGui.CollapsingHeader("Physics"))
            {
                var physicsWorld = _editor.PhysicsWorld;
                if (physicsWorld != null)
                {
                    var gravity = physicsWorld.Gravity;
                    System.Numerics.Vector3 gravityVec = new System.Numerics.Vector3(gravity.X, gravity.Y, gravity.Z);
                    if (ImGui.DragFloat3("Gravity", ref gravityVec))
                    {
                        physicsWorld.Gravity = new Engine.Math.Vector3(gravityVec.X, gravityVec.Y, gravityVec.Z);
                    }

                    bool showDebug = _editor.ShowPhysicsDebug;
                    if (ImGui.Checkbox("Show Physics Debug", ref showDebug))
                    {
                        _editor.ShowPhysicsDebug = showDebug;
                    }
                }
            }

            if (ImGui.CollapsingHeader("Anti-Aliasing"))
            {
                var aaSettings = _editor.AntiAliasingSettings;
                if (aaSettings != null)
                {
                    int mode = (int)aaSettings.Mode;
                    string[] modes = { "None", "MSAA 2x", "MSAA 4x", "MSAA 8x", "FXAA", "SMAA" };
                    if (ImGui.Combo("Mode", ref mode, modes, modes.Length))
                    {
                        aaSettings.Mode = (AntiAliasingMode)mode;
                    }

                    if (aaSettings.Mode == AntiAliasingMode.FXAA || aaSettings.Mode == AntiAliasingMode.SMAA)
                    {
                        int quality = (int)aaSettings.Quality;
                        string[] qualities = { "Low", "Medium", "High", "Ultra" };
                        if (ImGui.Combo("Quality", ref quality, qualities, qualities.Length))
                        {
                            aaSettings.Quality = (AntiAliasingQuality)quality;
                        }
                    }
                }
            }

            if (ImGui.CollapsingHeader("Display"))
            {
                bool vSync = _editor.VSyncEnabled;
                if (ImGui.Checkbox("V-Sync", ref vSync))
                {
                    _editor.VSyncEnabled = vSync;
                }
            }

            bool pathTracingEnabled = _editor.RenderingPipeline == EditorApplication.RenderingPipelineType.PathTraced;
            if (pathTracingEnabled)
            {
                if (_editor.PathTracingSettings != null)
                {
                    _editor.PathTracingSettings.Enabled = true;
                }
                
                if (ImGui.CollapsingHeader("Path Tracing"))
                {
                    var ptSettings = _editor.PathTracingSettings;
                    if (ptSettings != null)
                    {

                        bool enableDirectLight = ptSettings.EnableDirectLight;
                        if (ImGui.Checkbox("Enable Direct Light", ref enableDirectLight))
                        {
                            ptSettings.EnableDirectLight = enableDirectLight;
                        }

                        bool enableShadows = ptSettings.EnableShadows;
                        if (ImGui.Checkbox("Enable Shadows", ref enableShadows))
                        {
                            ptSettings.EnableShadows = enableShadows;
                        }

                        bool enableReflections = ptSettings.EnableReflections;
                        if (ImGui.Checkbox("Enable Reflections", ref enableReflections))
                        {
                            ptSettings.EnableReflections = enableReflections;
                        }

                        ImGui.Separator();

                        int rayDepth = ptSettings.RayDepth;
                        if (ImGui.SliderInt("Ray Depth", ref rayDepth, 1, 32))
                        {
                            ptSettings.RayDepth = rayDepth;
                        }

                        int spp = ptSettings.SamplesPerPixel;
                        if (ImGui.SliderInt("Samples Per Pixel", ref spp, 1, 16))
                        {
                            ptSettings.SamplesPerPixel = spp;
                        }

                        int maxSamples = ptSettings.MaxSamples;
                        if (ImGui.SliderInt("Max Samples", ref maxSamples, 64, 4096))
                        {
                            ptSettings.MaxSamples = maxSamples;
                        }
                    }
                }
            }

            if (ImGui.CollapsingHeader("Post Processing"))
            {
                var ppSettings = _editor.PostProcessingSettings;
                if (ppSettings != null)
                {
                    bool motionBlur = ppSettings.MotionBlurEnabled;
                    if (ImGui.Checkbox("Motion Blur", ref motionBlur))
                    {
                        ppSettings.MotionBlurEnabled = motionBlur;
                    }

                    if (motionBlur)
                    {
                        float intensity = ppSettings.MotionBlurIntensity;
                        if (ImGui.SliderFloat("Motion Blur Strength", ref intensity, 0.0f, 2.0f))
                        {
                            ppSettings.MotionBlurIntensity = intensity;
                        }

                        float shutter = ppSettings.MotionBlurShutterAngle;
                        if (ImGui.SliderFloat("Shutter Angle", ref shutter, 0.0f, 360.0f))
                        {
                            ppSettings.MotionBlurShutterAngle = shutter;
                        }

                        int samples = ppSettings.MotionBlurSampleCount;
                        if (ImGui.SliderInt("Sample Count", ref samples, 4, 32))
                        {
                            ppSettings.MotionBlurSampleCount = System.Math.Clamp(samples, 4, 32);
                        }

                        float maxDistance = ppSettings.MotionBlurMaxSampleDistance;
                        if (ImGui.SliderFloat("Max Sample Distance", ref maxDistance, 0.05f, 2.0f))
                        {
                            ppSettings.MotionBlurMaxSampleDistance = maxDistance;
                        }
                    }

                    ImGui.Separator();

                    bool bloom = ppSettings.BloomEnabled;
                    if (ImGui.Checkbox("Bloom", ref bloom))
                    {
                        ppSettings.BloomEnabled = bloom;
                    }

                    if (bloom)
                    {
                        float intensity = ppSettings.BloomIntensity;
                        if (ImGui.SliderFloat("Bloom Intensity", ref intensity, 0.0f, 3.0f))
                        {
                            ppSettings.BloomIntensity = intensity;
                        }

                        float threshold = ppSettings.BloomThreshold;
                        if (ImGui.SliderFloat("Bloom Threshold", ref threshold, 0.0f, 2.0f))
                        {
                            ppSettings.BloomThreshold = threshold;
                        }

                        float knee = ppSettings.BloomSoftKnee;
                        if (ImGui.SliderFloat("Soft Knee", ref knee, 0.0f, 1.0f))
                        {
                            ppSettings.BloomSoftKnee = knee;
                        }

                        float diffusion = ppSettings.BloomDiffusion;
                        if (ImGui.SliderFloat("Diffusion", ref diffusion, 1.0f, 10.0f))
                        {
                            ppSettings.BloomDiffusion = diffusion;
                        }

                        float scatter = ppSettings.BloomScatter;
                        if (ImGui.SliderFloat("Scatter", ref scatter, 0.0f, 1.5f))
                        {
                            ppSettings.BloomScatter = scatter;
                        }

                        bool highQuality = ppSettings.BloomHighQuality;
                        if (ImGui.Checkbox("High Quality Filtering", ref highQuality))
                        {
                            ppSettings.BloomHighQuality = highQuality;
                        }
                    }

                    ImGui.Separator();

                    bool vignette = ppSettings.VignetteEnabled;
                    if (ImGui.Checkbox("Vignette", ref vignette))
                    {
                        ppSettings.VignetteEnabled = vignette;
                    }

                    if (vignette)
                    {
                        float intensity = ppSettings.VignetteIntensity;
                        if (ImGui.SliderFloat("Vignette Intensity", ref intensity, 0.0f, 1.0f))
                        {
                            ppSettings.VignetteIntensity = intensity;
                        }

                        float radius = ppSettings.VignetteRadius;
                        if (ImGui.SliderFloat("Vignette Radius", ref radius, 0.0f, 1.0f))
                        {
                            ppSettings.VignetteRadius = radius;
                        }

                        float smoothness = ppSettings.VignetteSmoothness;
                        if (ImGui.SliderFloat("Vignette Smoothness", ref smoothness, 0.0f, 1.0f))
                        {
                            ppSettings.VignetteSmoothness = smoothness;
                        }
                    }

                    ImGui.Separator();

                    bool ssao = ppSettings.SSAOEnabled;
                    if (ImGui.Checkbox("SSAO", ref ssao))
                    {
                        ppSettings.SSAOEnabled = ssao;
                    }

                    if (ssao)
                    {
                        float ssaoRadius = ppSettings.SSAORadius;
                        if (ImGui.SliderFloat("SSAO Radius", ref ssaoRadius, 0.5f, 16.0f))
                        {
                            ppSettings.SSAORadius = ssaoRadius;
                        }

                        float ssaoBias = ppSettings.SSAOBias;
                        if (ImGui.SliderFloat("SSAO Bias", ref ssaoBias, 0.001f, 0.05f))
                        {
                            ppSettings.SSAOBias = ssaoBias;
                        }

                        float ssaoPower = ppSettings.SSAOPower;
                        if (ImGui.SliderFloat("SSAO Power", ref ssaoPower, 0.5f, 3.0f))
                        {
                            ppSettings.SSAOPower = ssaoPower;
                        }
                    }
                }
            }
        }
        ImGui.End();
    }

    public bool IsVisible
    {
        get => _showWindow;
        set => _showWindow = value;
    }
}

