using System;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;
using Engine.Math;
using Engine.Graphics;

namespace Engine.Editor;

public enum RenderingMode
{
    Default,
    Wireframe,
    ShadedWireframe
}

public enum TransformGizmoMode
{
    Position,
    Rotation,
    Scale
}

public class ViewportWindow
{
    private readonly EditorApplication _editor;
    private uint _framebuffer;
    private uint _texture;
    private uint _depthTexture;
    private uint _msaaFramebuffer;
    private uint _msaaColorRenderbuffer;
    private uint _msaaDepthRenderbuffer;
    private int _width = 1920;
    private int _height = 1080;
    private int _msaaSamples = 0;
    private bool _isHovered = false;
    private RenderingMode _renderingMode = RenderingMode.Default;
    private TransformGizmoMode _gizmoMode = TransformGizmoMode.Position;
    private GizmoRenderer? _gizmoRenderer;
    private bool _gizmoInitialized = false;
    private bool _isDraggingGizmo = false;
    private int _selectedGizmoAxis = -1;
    private System.Numerics.Vector2 _dragStartPosition;
    private Vector3 _dragStartMouseRay;
    private Vector3 _dragStartRayOrigin;
    private Vector3 _dragStartRayDirection;
    private Vector3 _objectStartWorldPosition;
    private Vector3 _objectStartPosition;
    private Quaternion _objectStartRotation;
    private Vector3 _objectStartScale;
    private float _dragStartOffset = 0.0f;
    private const float ArrowLength = 0.8f;
    private const float RotationArcRadius = 0.7f;

    public bool IsHovered => _isHovered;
    public RenderingMode CurrentRenderingMode => _renderingMode;
    public TransformGizmoMode CurrentGizmoMode => _gizmoMode;

    public ViewportWindow(EditorApplication editor)
    {
        _editor = editor;
        CreateFramebuffer();
        _gizmoRenderer = new GizmoRenderer();
    }

    private void CreateFramebuffer()
    {
        int samples = GetMSAASamples();
        
        if (samples > 0)
        {
            int maxSamples = GL.GetInteger(GetPName.MaxSamples);
            if (samples > maxSamples)
            {
                samples = maxSamples;
            }
            
            bool msaaSuccess = false;
            for (int testSamples = samples; testSamples > 0 && !msaaSuccess; testSamples--)
            {
                _msaaFramebuffer = (uint)GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _msaaFramebuffer);

                _msaaColorRenderbuffer = (uint)GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msaaColorRenderbuffer);
                GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, testSamples, RenderbufferStorage.Rgba8, _width, _height);
                
                ErrorCode error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                    GL.DeleteFramebuffer(_msaaFramebuffer);
                    GL.DeleteRenderbuffer(_msaaColorRenderbuffer);
                    _msaaFramebuffer = 0;
                    _msaaColorRenderbuffer = 0;
                    continue;
                }

                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _msaaColorRenderbuffer);

                _msaaDepthRenderbuffer = (uint)GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msaaDepthRenderbuffer);
                GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, testSamples, RenderbufferStorage.Depth24Stencil8, _width, _height);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _msaaDepthRenderbuffer);

                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

                var msaaStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (msaaStatus == FramebufferErrorCode.FramebufferComplete)
                {
                    samples = testSamples;
                    msaaSuccess = true;
                }
                else
                {
                    GL.DeleteFramebuffer(_msaaFramebuffer);
                    GL.DeleteRenderbuffer(_msaaColorRenderbuffer);
                    GL.DeleteRenderbuffer(_msaaDepthRenderbuffer);
                    _msaaFramebuffer = 0;
                    _msaaColorRenderbuffer = 0;
                    _msaaDepthRenderbuffer = 0;
                }
            }
            
            if (!msaaSuccess)
            {
                Logger.Warning($"MSAA framebuffer creation failed for all sample counts, falling back to non-MSAA");
                samples = 0;
            }
        }

        _framebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

        _texture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, 0);

        _depthTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, _width, _height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, _depthTexture, 0);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            Logger.Error($"Framebuffer is not complete! Status: {status}");
            throw new Exception($"Framebuffer is not complete! Status: {status}");
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _msaaSamples = samples;
    }
    
    private int GetMSAASamples()
    {
        if (_editor.AntiAliasingSettings == null || !_editor.AntiAliasingSettings.Enabled)
            return 0;
            
        return _editor.AntiAliasingSettings.Mode switch
        {
            AntiAliasingMode.MSAA2x => 2,
            AntiAliasingMode.MSAA4x => 4,
            AntiAliasingMode.MSAA8x => 8,
            _ => 0
        };
    }

    public void Render()
    {
        if (ImGui.Begin("Viewport"))
        {
            _isHovered = ImGui.IsWindowHovered();
            
            var viewportSize = ImGui.GetContentRegionAvail();
            if (viewportSize.X > 0 && viewportSize.Y > 0)
            {
                int newWidth = (int)viewportSize.X;
                int newHeight = (int)viewportSize.Y;

                if (newWidth != _width || newHeight != _height)
                {
                    _width = newWidth;
                    _height = newHeight;
                    ResizeFramebuffer();
                    _editor.Camera.AspectRatio = (float)_width / _height;
                }

                int samples = GetMSAASamples();
                uint renderFramebuffer = (samples > 0 && _msaaFramebuffer > 0) ? _msaaFramebuffer : _framebuffer;
                
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, renderFramebuffer);
                
                if (Engine.Core.System.DevMode)
                {
                    var fbStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (fbStatus != FramebufferErrorCode.FramebufferComplete)
                    {
                        Logger.Error($"Framebuffer not complete before render! Status: {fbStatus}");
                    }
                }
                
                GL.Viewport(0, 0, _width, _height);
                GL.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                if (_editor.ShadowRenderer != null && _editor.DirectionalLight != null && _editor.CurrentScene != null && _editor.ShadowSettings.Enabled && _editor.DirectionalLight.CastShadows)
                {
                    _editor.ShadowRenderer.RenderShadowMap(_editor.DirectionalLight, _editor.Camera, _editor.CurrentScene);
                }
                
                if (samples > 0 && _msaaFramebuffer > 0)
                {
                    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msaaFramebuffer);
                    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _framebuffer);
                    if (Engine.Core.System.IsMacOS())
                    {
                        GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
                    }
                    else
                    {
                        GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
                    }
                }
                
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
                GL.Viewport(0, 0, _width, _height);

                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthFunction.Less);
                GL.Disable(EnableCap.Blend);
                
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(CullFaceMode.Back);

                if (_editor.Skybox != null)
                {
                    _editor.Skybox.Render(_editor.Camera.ViewMatrix, _editor.Camera.ProjectionMatrix);
                }

                GL.Clear(ClearBufferMask.DepthBufferBit);

                var scene = _editor.CurrentScene;
                if (scene != null && _editor.DefaultShader != null)
                {
                    _editor.DefaultShader.Use();
                    
                    _editor.DefaultShader.SetMatrix4("uView", _editor.Camera.ViewMatrix);
                    
                    if (_editor.ShadowRenderer != null && _editor.ShadowSettings.Enabled && _editor.DirectionalLight != null && _editor.DirectionalLight.CastShadows)
                    {
                        _editor.ShadowRenderer.BindShadowMap(_editor.DefaultShader);
                    }
                    else
                    {
                        _editor.DefaultShader.SetInt("uUseShadows", 0);
                    }

                    Span<int> prevPolygonMode = stackalloc int[2];
                    unsafe
                    {
                        fixed (int* iptr = &prevPolygonMode[0])
                        {
                            GL.GetInteger(GetPName.PolygonMode, iptr);
                        }
                    }

                        int glVersion = GL.GetInteger(GetPName.MajorVersion) * 100 + GL.GetInteger(GetPName.MinorVersion) * 10;
                        bool compatibilityProfile = (GL.GetInteger((GetPName)All.ContextProfileMask) & (int)All.ContextCompatibilityProfileBit) != 0;
                        
                    var renderers = scene.FindObjectsOfType<MeshRenderer>();
                    
                    if (_renderingMode == RenderingMode.ShadedWireframe)
                    {
                        if (glVersion <= 310 || compatibilityProfile)
                        {
                            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
                            GL.PolygonMode(MaterialFace.Back, PolygonMode.Fill);
                        }
                        else
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    }

                    foreach (var renderer in renderers)
                    {
                        if (renderer.Enabled && renderer.GameObject?.Active == true)
                        {
                            renderer.Render(_editor.Camera.ViewProjectionMatrix);
                        }
                    }

                        if (glVersion <= 310 || compatibilityProfile)
                        {
                            GL.PolygonMode(MaterialFace.Front, PolygonMode.Line);
                            GL.PolygonMode(MaterialFace.Back, PolygonMode.Line);
                        }
                        else
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        }

                        foreach (var renderer in renderers)
                        {
                            if (renderer.Enabled && renderer.GameObject?.Active == true)
                            {
                                renderer.Render(_editor.Camera.ViewProjectionMatrix);
                            }
                        }
                    }
                    else if (_renderingMode == RenderingMode.Wireframe)
                    {
                        if (glVersion <= 310 || compatibilityProfile)
                        {
                            GL.PolygonMode(MaterialFace.Front, PolygonMode.Line);
                            GL.PolygonMode(MaterialFace.Back, PolygonMode.Line);
                        }
                        else
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        }

                        foreach (var renderer in renderers)
                        {
                            if (renderer.Enabled && renderer.GameObject?.Active == true)
                            {
                                renderer.Render(_editor.Camera.ViewProjectionMatrix);
                            }
                        }
                    }
                    else
                    {
                        if (glVersion <= 310 || compatibilityProfile)
                        {
                            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
                            GL.PolygonMode(MaterialFace.Back, PolygonMode.Fill);
                        }
                        else
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                        }

                        foreach (var renderer in renderers)
                        {
                            if (renderer.Enabled && renderer.GameObject?.Active == true)
                            {
                                renderer.Render(_editor.Camera.ViewProjectionMatrix);
                            }
                        }
                    }
                        
                        if (glVersion <= 310 || compatibilityProfile)
                        {
                            GL.PolygonMode(MaterialFace.Front, (PolygonMode)prevPolygonMode[0]);
                            GL.PolygonMode(MaterialFace.Back, (PolygonMode)prevPolygonMode[1]);
                        }
                        else
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, (PolygonMode)prevPolygonMode[0]);
                        }
                    
                    GL.DepthFunc(DepthFunction.Less);
                }

                if (!_gizmoInitialized && _gizmoRenderer != null)
                {
                    _gizmoRenderer.Initialize();
                    _gizmoInitialized = true;
                }

                if (_editor.SelectedObject != null && _gizmoRenderer != null && _gizmoInitialized)
                {
                    var transform = _editor.SelectedObject.Transform;
                    if (transform != null)
                    {
                        int selectedAxis = _isDraggingGizmo ? _selectedGizmoAxis : -1;
                        switch (_gizmoMode)
                        {
                            case TransformGizmoMode.Position:
                                _gizmoRenderer.RenderPositionGizmo(transform, _editor.Camera, _editor.Camera.ViewProjectionMatrix, selectedAxis);
                                break;
                            case TransformGizmoMode.Rotation:
                                _gizmoRenderer.RenderRotationGizmo(transform, _editor.Camera, _editor.Camera.ViewProjectionMatrix, selectedAxis);
                                break;
                            case TransformGizmoMode.Scale:
                                _gizmoRenderer.RenderScaleGizmo(transform, _editor.Camera, _editor.Camera.ViewProjectionMatrix, selectedAxis);
                                break;
                        }
                    }
                }

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                
                uint finalTexture = _texture;
                
                if (_editor.AntiAliasingSettings != null && _editor.AntiAliasingSettings.Enabled)
                {
                    if (_editor.AntiAliasingSettings.Mode == AntiAliasingMode.FXAA && _editor.FXAA != null)
                    {
                        _editor.FXAA.Resize(_width, _height);
                        _editor.FXAA.Render(_texture);
                        finalTexture = _editor.FXAA.Texture;
                    }
                    else if (_editor.AntiAliasingSettings.Mode == AntiAliasingMode.SMAA && _editor.SMAA != null)
                    {
                        _editor.SMAA.Resize(_width, _height);
                        _editor.SMAA.Render(_texture);
                        finalTexture = _editor.SMAA.Texture;
                    }
                }
                
                if (_editor.PostProcessingSettings != null)
                {
                    if (_editor.MotionBlur != null)
                    {
                        _editor.MotionBlur.SetDepthTexture(_depthTexture);
                    }
                    if (_editor.SSAO != null)
                    {
                        _editor.SSAO.SetDepthTexture(_depthTexture);
                        _editor.SSAO.UpdateCameraData(_editor.Camera.ProjectionMatrix);
                    }
                    if (_editor.MotionBlur != null && _editor.PostProcessingSettings.MotionBlurEnabled)
                    {
                        _editor.MotionBlur.SetActive(true);
                        _editor.MotionBlur.UpdateCameraData(_editor.Camera.ViewProjectionMatrix, 1.0f / 60.0f);
                    }
                    else if (_editor.MotionBlur != null)
                    {
                        _editor.MotionBlur.SetActive(false);
                    }
                    
                    uint currentTexture = finalTexture;

                    if (_editor.PostProcessingSettings.SSAOEnabled && _editor.SSAO != null)
                    {
                        _editor.SSAO.Resize(_width, _height);
                        _editor.SSAO.Apply(currentTexture, 0, _width, _height);
                        currentTexture = _editor.SSAO.Texture;
                    }

                    if (_editor.PostProcessingSettings.BloomEnabled && _editor.Bloom != null)
                    {
                        _editor.Bloom.Resize(_width, _height);
                        _editor.Bloom.Apply(currentTexture, 0, _width, _height);
                        currentTexture = _editor.Bloom.Texture;
                    }
                    
                    if (_editor.PostProcessingSettings.VignetteEnabled && _editor.Vignette != null)
                    {
                        _editor.Vignette.Resize(_width, _height);
                        _editor.Vignette.Apply(currentTexture, 0, _width, _height);
                        currentTexture = _editor.Vignette.Texture;
                    }
                    
                    if (_editor.PostProcessingSettings.MotionBlurEnabled && _editor.MotionBlur != null)
                    {
                        _editor.MotionBlur.Resize(_width, _height);
                        _editor.MotionBlur.Apply(currentTexture, 0, _width, _height);
                        currentTexture = _editor.MotionBlur.Texture;
                    }
                    
                    finalTexture = currentTexture;
                }
                
                int viewportWidth, viewportHeight;
                if (Engine.Core.System.IsMacOS() && _editor.Window != null)
                {
                    viewportWidth = _editor.Window.FramebufferSize.X;
                    viewportHeight = _editor.Window.FramebufferSize.Y;
                }
                else
                {
                    viewportWidth = _editor.Window != null ? _editor.Window.Size.X : 1920;
                    viewportHeight = _editor.Window != null ? _editor.Window.Size.Y : 1080;
                }
                GL.Viewport(0, 0, viewportWidth, viewportHeight);

                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                GL.BindTexture(TextureTarget.Texture2D, finalTexture);
                if (Engine.Core.System.DevMode)
                {
                    ErrorCode error = GL.GetError();
                    if (error != ErrorCode.NoError)
                    {
                        Logger.Error($"Viewport: OpenGL error before ImGui.Image: {error}");
                    }
                }

                ImGui.Image((IntPtr)finalTexture, viewportSize, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
                
                if (Engine.Core.System.DevMode)
                {
                    ErrorCode error = GL.GetError();
                    if (error != ErrorCode.NoError)
                    {
                        Logger.Error($"Viewport: OpenGL error after ImGui.Image: {error}");
                    }
                }
                
                HandleObjectPicking();
                RenderOverlay();
            }
        }
        ImGui.End();
    }

    private Vector3 ScreenToWorldRay(System.Numerics.Vector2 mousePos, System.Numerics.Vector2 imageMin, System.Numerics.Vector2 imageMax, out Vector3 rayOrigin, out Vector3 rayDirection)
    {
        float normalizedX = (mousePos.X - imageMin.X) / (imageMax.X - imageMin.X);
        float normalizedY = 1.0f - (mousePos.Y - imageMin.Y) / (imageMax.Y - imageMin.Y);

        float ndcX = normalizedX * 2.0f - 1.0f;
        float ndcY = normalizedY * 2.0f - 1.0f;

        Engine.Math.Vector4 nearPoint = new Engine.Math.Vector4(ndcX, ndcY, -1.0f, 1.0f);
        Engine.Math.Vector4 farPoint = new Engine.Math.Vector4(ndcX, ndcY, 1.0f, 1.0f);

        Matrix4 invViewProj = _editor.Camera.ViewProjectionMatrix.Inverse();

        Engine.Math.Vector4 nearWorld = invViewProj * nearPoint;
        Engine.Math.Vector4 farWorld = invViewProj * farPoint;

        if (MathF.Abs(nearWorld.W) > 0.0001f)
            nearWorld = new Engine.Math.Vector4(nearWorld.X / nearWorld.W, nearWorld.Y / nearWorld.W, nearWorld.Z / nearWorld.W, 1.0f);
        if (MathF.Abs(farWorld.W) > 0.0001f)
            farWorld = new Engine.Math.Vector4(farWorld.X / farWorld.W, farWorld.Y / farWorld.W, farWorld.Z / farWorld.W, 1.0f);

        rayOrigin = new Vector3(nearWorld.X, nearWorld.Y, nearWorld.Z);
        rayDirection = (new Vector3(farWorld.X, farWorld.Y, farWorld.Z) - rayOrigin).Normalized();
        return rayOrigin;
    }

    private void HandleObjectPicking()
    {
        if (!_isHovered)
            return;

        var mousePos = ImGui.GetMousePos();
        var imageMin = ImGui.GetItemRectMin();
        var imageMax = ImGui.GetItemRectMax();

        if (mousePos.X < imageMin.X || mousePos.X > imageMax.X || mousePos.Y < imageMin.Y || mousePos.Y > imageMax.Y)
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _isDraggingGizmo = false;
                _selectedGizmoAxis = -1;
            }
            return;
        }

        ScreenToWorldRay(mousePos, imageMin, imageMax, out Vector3 rayOrigin, out Vector3 rayDirection);

        bool isMouseDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        bool isMouseClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        bool isMouseReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left);

        if (_editor.SelectedObject != null && _gizmoRenderer != null && _gizmoInitialized)
        {
            var transform = _editor.SelectedObject.Transform;
            if (transform != null)
            {
                if (isMouseClicked)
                {
                    if (_gizmoRenderer.RayIntersectsGizmo(rayOrigin, rayDirection, transform, _gizmoMode, _editor.Camera, out int axis))
                    {
                        _isDraggingGizmo = true;
                        _selectedGizmoAxis = axis;
                        _dragStartPosition = mousePos;
                        _dragStartRayOrigin = rayOrigin;
                        _dragStartRayDirection = rayDirection;
                        _objectStartWorldPosition = transform.WorldPosition;
                        _objectStartPosition = transform.Position;
                        _objectStartRotation = transform.Rotation;
                        _objectStartScale = transform.Scale;
                        
                        Vector3 axisDir = axis == 0 ? Vector3.Right : (axis == 1 ? Vector3.Up : Vector3.Backward);
                        if (_gizmoMode == TransformGizmoMode.Position)
                        {
                            Vector3 cameraForward = _editor.Camera.Forward;
                            Vector3 planeNormal = Vector3.Cross(axisDir, cameraForward).Normalized();
                            if (planeNormal.LengthSquared < 0.01f)
                                planeNormal = Vector3.Cross(axisDir, _editor.Camera.Up).Normalized();
                            
                            float planeD = -Vector3.Dot(planeNormal, _objectStartWorldPosition);
                            float denom = Vector3.Dot(planeNormal, rayDirection);
                            if (MathF.Abs(denom) > 0.0001f)
                            {
                                float t = -(Vector3.Dot(planeNormal, rayOrigin) + planeD) / denom;
                                Vector3 startIntersection = rayOrigin + rayDirection * t;
                                _dragStartOffset = Vector3.Dot(startIntersection - _objectStartWorldPosition, axisDir);
                            }
                        }
                        else if (_gizmoMode == TransformGizmoMode.Scale)
                        {
                            _dragStartOffset = 0.0f;
                        }
                        else if (_gizmoMode == TransformGizmoMode.Rotation)
                        {
                            _dragStartOffset = 0.0f;
                        }
                        
                        return;
                    }
                }

                if (_isDraggingGizmo && isMouseDown)
                {
                    HandleGizmoDrag(transform, mousePos, rayOrigin, rayDirection);
                    return;
                }

                if (isMouseReleased)
                {
                    _isDraggingGizmo = false;
                    _selectedGizmoAxis = -1;
                }
            }
        }

        if (isMouseClicked && !_isDraggingGizmo)
        {
            GameObject? closestObject = null;
            float closestDistance = float.MaxValue;

            var scene = _editor.CurrentScene;
            if (scene != null)
            {
                var renderers = scene.FindObjectsOfType<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    if (!renderer.Enabled || renderer.GameObject?.Active != true || renderer.Mesh == null || renderer.Transform == null)
                        continue;

                    Vector3 objectPos = renderer.Transform.WorldPosition;
                    Vector3 toObject = objectPos - rayOrigin;
                    float projectionLength = Vector3.Dot(toObject, rayDirection);
                    
                    if (projectionLength < 0)
                        continue;

                    Vector3 closestPoint = rayOrigin + rayDirection * projectionLength;
                    float distance = Vector3.Distance(closestPoint, objectPos);

                    if (distance < 0.5f && projectionLength < closestDistance)
                    {
                        closestDistance = projectionLength;
                        closestObject = renderer.GameObject;
                    }
                }
            }

            _editor.SelectedObject = closestObject;
        }
    }

    private Vector3 WorldToScreen(Vector3 worldPos, Matrix4 viewProjection, System.Numerics.Vector2 viewportSize)
    {
        Engine.Math.Vector4 clipPos = viewProjection * new Engine.Math.Vector4(worldPos.X, worldPos.Y, worldPos.Z, 1.0f);
        if (MathF.Abs(clipPos.W) < 0.0001f)
            return Vector3.Zero;
        
        float ndcX = clipPos.X / clipPos.W;
        float ndcY = clipPos.Y / clipPos.W;
        
        float screenX = (ndcX + 1.0f) * 0.5f * viewportSize.X;
        float screenY = (1.0f - ndcY) * 0.5f * viewportSize.Y;
        
        return new Vector3(screenX, screenY, clipPos.Z / clipPos.W);
    }

    private void HandleGizmoDrag(Transform transform, System.Numerics.Vector2 mousePos, Vector3 rayOrigin, Vector3 rayDirection)
    {
        if (_selectedGizmoAxis < 0)
            return;

        Vector3 objectWorldPos = _objectStartWorldPosition;
        Vector3 axisDirection = _selectedGizmoAxis == 0 ? Vector3.Right : (_selectedGizmoAxis == 1 ? Vector3.Up : Vector3.Backward);

        if (_gizmoMode == TransformGizmoMode.Position)
        {
            var imageMin = ImGui.GetItemRectMin();
            var imageMax = ImGui.GetItemRectMax();
            System.Numerics.Vector2 viewportSize = new System.Numerics.Vector2(imageMax.X - imageMin.X, imageMax.Y - imageMin.Y);
            
            Matrix4 viewProjection = _editor.Camera.ViewProjectionMatrix;
            Vector3 axisStartScreen = WorldToScreen(objectWorldPos, viewProjection, viewportSize);
            Vector3 axisEndScreen = WorldToScreen(objectWorldPos + axisDirection * ArrowLength, viewProjection, viewportSize);
            
            Vector3 axisScreenDir = (axisEndScreen - axisStartScreen);
            float axisScreenLength = axisScreenDir.Length;
            if (axisScreenLength < 0.0001f)
                return;
            
            axisScreenDir = axisScreenDir / axisScreenLength;
            
            Vector3 mouseDelta = new Vector3(mousePos.X - _dragStartPosition.X, mousePos.Y - _dragStartPosition.Y, 0);
            float screenMovement = Vector3.Dot(mouseDelta, new Vector3(axisScreenDir.X, axisScreenDir.Y, 0));
            
            float worldMovement = (screenMovement / axisScreenLength) * ArrowLength;

            if (transform.Parent == null)
            {
                Vector3 newPosition = _objectStartPosition;
                if (_selectedGizmoAxis == 0)
                    newPosition.X += worldMovement;
                else if (_selectedGizmoAxis == 1)
                    newPosition.Y += worldMovement;
                else
                    newPosition.Z += worldMovement;
                transform.Position = newPosition;
            }
            else
            {
                Vector3 worldMovementVec = axisDirection * worldMovement;
                Matrix4 parentInverse = transform.Parent.WorldMatrix.Inverse();
                Engine.Math.Vector4 localMovement4 = parentInverse * new Engine.Math.Vector4(worldMovementVec.X, worldMovementVec.Y, worldMovementVec.Z, 0);
                Vector3 localMovement = new Vector3(localMovement4.X, localMovement4.Y, localMovement4.Z);
                transform.Position = _objectStartPosition + localMovement;
            }
        }
        else if (_gizmoMode == TransformGizmoMode.Scale)
        {
            var imageMin = ImGui.GetItemRectMin();
            var imageMax = ImGui.GetItemRectMax();
            System.Numerics.Vector2 viewportSize = new System.Numerics.Vector2(imageMax.X - imageMin.X, imageMax.Y - imageMin.Y);
            
            Matrix4 viewProjection = _editor.Camera.ViewProjectionMatrix;
            Vector3 axisStartScreen = WorldToScreen(objectWorldPos, viewProjection, viewportSize);
            Vector3 axisEndScreen = WorldToScreen(objectWorldPos + axisDirection * ArrowLength, viewProjection, viewportSize);
            
            Vector3 axisScreenDir = (axisEndScreen - axisStartScreen);
            float axisScreenLength = axisScreenDir.Length;
            if (axisScreenLength < 0.0001f)
                return;
            
            axisScreenDir = axisScreenDir / axisScreenLength;
            
            Vector3 mouseDelta = new Vector3(mousePos.X - _dragStartPosition.X, mousePos.Y - _dragStartPosition.Y, 0);
            float screenMovement = Vector3.Dot(mouseDelta, new Vector3(axisScreenDir.X, axisScreenDir.Y, 0));
            
            float scaleFactor = 1.0f + (screenMovement / axisScreenLength);

            Vector3 newScale = _objectStartScale;
            if (_selectedGizmoAxis == 0)
                newScale.X = MathF.Max(0.01f, _objectStartScale.X * scaleFactor);
            else if (_selectedGizmoAxis == 1)
                newScale.Y = MathF.Max(0.01f, _objectStartScale.Y * scaleFactor);
            else
                newScale.Z = MathF.Max(0.01f, _objectStartScale.Z * scaleFactor);

            transform.Scale = newScale;
        }
        else if (_gizmoMode == TransformGizmoMode.Rotation)
        {
            var imageMin = ImGui.GetItemRectMin();
            var imageMax = ImGui.GetItemRectMax();
            System.Numerics.Vector2 viewportSize = new System.Numerics.Vector2(imageMax.X - imageMin.X, imageMax.Y - imageMin.Y);
            
            Matrix4 viewProjection = _editor.Camera.ViewProjectionMatrix;
            Vector3 objectScreen = WorldToScreen(objectWorldPos, viewProjection, viewportSize);
            
            Vector3 cameraPos = _editor.Camera.Position;
            Vector3 toObject = (_objectStartWorldPosition - cameraPos).Normalized();
            Vector3 right = Vector3.Cross(axisDirection, toObject).Normalized();
            if (right.LengthSquared < 0.01f)
                right = Vector3.Cross(axisDirection, _editor.Camera.Up).Normalized();
            Vector3 up = Vector3.Cross(right, axisDirection).Normalized();
            
            Vector3 rightScreen = WorldToScreen(objectWorldPos + right * RotationArcRadius, viewProjection, viewportSize);
            Vector3 upScreen = WorldToScreen(objectWorldPos + up * RotationArcRadius, viewProjection, viewportSize);
            
            Vector3 rightScreenDir = (rightScreen - objectScreen);
            Vector3 upScreenDir = (upScreen - objectScreen);
            
            float rightScreenLen = rightScreenDir.Length;
            float upScreenLen = upScreenDir.Length;
            if (rightScreenLen < 0.0001f || upScreenLen < 0.0001f)
                return;
            
            rightScreenDir = rightScreenDir / rightScreenLen;
            upScreenDir = upScreenDir / upScreenLen;
            
            Vector3 startMouseDir = new Vector3(_dragStartPosition.X - objectScreen.X, _dragStartPosition.Y - objectScreen.Y, 0);
            Vector3 currentMouseDir = new Vector3(mousePos.X - objectScreen.X, mousePos.Y - objectScreen.Y, 0);
            
            float startMouseLen = startMouseDir.Length;
            float currentMouseLen = currentMouseDir.Length;
            if (startMouseLen < 0.0001f || currentMouseLen < 0.0001f)
                return;
            
            startMouseDir = startMouseDir / startMouseLen;
            currentMouseDir = currentMouseDir / currentMouseLen;
            
            float startAngle = MathF.Atan2(Vector3.Dot(startMouseDir, new Vector3(upScreenDir.X, upScreenDir.Y, 0)), 
                                          Vector3.Dot(startMouseDir, new Vector3(rightScreenDir.X, rightScreenDir.Y, 0)));
            float currentAngle = MathF.Atan2(Vector3.Dot(currentMouseDir, new Vector3(upScreenDir.X, upScreenDir.Y, 0)), 
                                             Vector3.Dot(currentMouseDir, new Vector3(rightScreenDir.X, rightScreenDir.Y, 0)));
            float angleDelta = currentAngle - startAngle;

            Quaternion rotationDelta = Quaternion.FromAxisAngle(axisDirection, -angleDelta);
            transform.Rotation = (_objectStartRotation * rotationDelta).Normalized();
        }
    }

    private void RenderOverlay()
    {
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var imageMin = ImGui.GetItemRectMin();
        var imageMax = ImGui.GetItemRectMax();
        
        float buttonSize = 28.0f;
        float buttonSpacing = 4.0f;
        float padding = 12.0f;
        
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(buttonSpacing, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);
        
        var leftPos = new System.Numerics.Vector2(imageMin.X + padding, imageMin.Y + padding);
        ImGui.SetCursorPos(new System.Numerics.Vector2(leftPos.X - windowPos.X, leftPos.Y - windowPos.Y));
        
        bool isPosition = _gizmoMode == TransformGizmoMode.Position;
        bool isRotation = _gizmoMode == TransformGizmoMode.Rotation;
        bool isScale = _gizmoMode == TransformGizmoMode.Scale;
        
        if (isPosition)
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 1.0f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f));
        if (ImGui.Button("P", new System.Numerics.Vector2(buttonSize, buttonSize)))
            _gizmoMode = TransformGizmoMode.Position;
        ImGui.PopStyleColor(3);
        
        ImGui.SameLine();
        if (isRotation)
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 1.0f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f));
        if (ImGui.Button("R", new System.Numerics.Vector2(buttonSize, buttonSize)))
            _gizmoMode = TransformGizmoMode.Rotation;
        ImGui.PopStyleColor(3);
        
        ImGui.SameLine();
        if (isScale)
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 1.0f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f));
        if (ImGui.Button("S", new System.Numerics.Vector2(buttonSize, buttonSize)))
            _gizmoMode = TransformGizmoMode.Scale;
        ImGui.PopStyleColor(3);
        
        ImGui.PopStyleVar(3);
        
        float textPadding = 12.0f;
        float defaultWidth = ImGui.CalcTextSize("Default").X + textPadding * 2;
        float wireframeWidth = ImGui.CalcTextSize("Wireframe").X + textPadding * 2;
        float shadedWidth = ImGui.CalcTextSize("Shaded").X + textPadding * 2;
        float totalRightWidth = defaultWidth + wireframeWidth + shadedWidth + buttonSpacing * 2;
        
        var rightPos = new System.Numerics.Vector2(imageMax.X - totalRightWidth - padding, imageMin.Y + padding);
        ImGui.SetCursorPos(new System.Numerics.Vector2(rightPos.X - windowPos.X, rightPos.Y - windowPos.Y));
        
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(textPadding, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(buttonSpacing, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);
        
        bool isDefault = _renderingMode == RenderingMode.Default;
        bool isWireframe = _renderingMode == RenderingMode.Wireframe;
        bool isShadedWireframe = _renderingMode == RenderingMode.ShadedWireframe;
        
        if (isDefault)
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 1.0f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f));
        if (ImGui.Button("Default", new System.Numerics.Vector2(0, buttonSize)))
            _renderingMode = RenderingMode.Default;
        ImGui.PopStyleColor(3);
        
        ImGui.SameLine();
        if (isWireframe)
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 1.0f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f));
        if (ImGui.Button("Wireframe", new System.Numerics.Vector2(0, buttonSize)))
            _renderingMode = RenderingMode.Wireframe;
        ImGui.PopStyleColor(3);
        
        ImGui.SameLine();
        if (isShadedWireframe)
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 1.0f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 0.9f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.0f));
        if (ImGui.Button("Shaded", new System.Numerics.Vector2(0, buttonSize)))
            _renderingMode = RenderingMode.ShadedWireframe;
        ImGui.PopStyleColor(3);
        
        ImGui.PopStyleVar(3);
    }

    private void ResizeFramebuffer()
    {
        if (_msaaFramebuffer > 0)
        {
            GL.DeleteFramebuffer(_msaaFramebuffer);
            GL.DeleteRenderbuffer(_msaaColorRenderbuffer);
            GL.DeleteRenderbuffer(_msaaDepthRenderbuffer);
            _msaaFramebuffer = 0;
            _msaaColorRenderbuffer = 0;
            _msaaDepthRenderbuffer = 0;
        }
        
        GL.DeleteTexture(_texture);
        GL.DeleteTexture(_depthTexture);
        GL.DeleteFramebuffer(_framebuffer);
        _framebuffer = 0;
        _texture = 0;
        _depthTexture = 0;
        CreateFramebuffer();
    }
}
