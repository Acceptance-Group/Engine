using System;
using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;
using Engine.Math;
using Engine.Graphics;

namespace Engine.Editor;

public class ViewportWindow
{
    private readonly EditorApplication _editor;
    private uint _framebuffer;
    private uint _texture;
    private uint _renderbuffer;
    private uint _msaaFramebuffer;
    private uint _msaaColorRenderbuffer;
    private uint _msaaDepthRenderbuffer;
    private int _width = 1920;
    private int _height = 1080;
    private int _msaaSamples = 0;

    public ViewportWindow(EditorApplication editor)
    {
        _editor = editor;
        CreateFramebuffer();
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

        _renderbuffer = (uint)GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _renderbuffer);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, _width, _height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _renderbuffer);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            Logger.Error($"Framebuffer is not complete! Status: {status}");
            throw new Exception($"Framebuffer is not complete! Status: {status}");
        }

        Logger.Info($"Framebuffer created successfully: {_width}x{_height}, texture ID: {_texture}, MSAA samples: {samples}");

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
                
                var fbStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                if (fbStatus != FramebufferErrorCode.FramebufferComplete)
                {
                    Logger.Error($"Framebuffer not complete before render! Status: {fbStatus}");
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
                    GL.BlitFramebuffer(0, 0, _width, _height, 0, 0, _width, _height, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
                }
                
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
                GL.Viewport(0, 0, _width, _height);

                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthFunction.Less);
                GL.Disable(EnableCap.Blend);
                GL.Disable(EnableCap.CullFace);

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

                    var renderers = scene.FindObjectsOfType<MeshRenderer>();
                    foreach (var renderer in renderers)
                    {
                        if (renderer.Enabled && renderer.GameObject?.Active == true)
                        {
                            renderer.Render(_editor.Camera.ViewProjectionMatrix);
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
                
                int windowWidth = _editor.Window != null ? _editor.Window.Size.X : 1920;
                int windowHeight = _editor.Window != null ? _editor.Window.Size.Y : 1080;
                GL.Viewport(0, 0, windowWidth, windowHeight);

                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

                GL.BindTexture(TextureTarget.Texture2D, finalTexture);
                ErrorCode error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                    Logger.Error($"Viewport: OpenGL error before ImGui.Image: {error}");
                }

                ImGui.Image((IntPtr)finalTexture, viewportSize, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
                
                error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                    Logger.Error($"Viewport: OpenGL error after ImGui.Image: {error}");
                }
            }
        }
        ImGui.End();
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
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        
        GL.DeleteTexture(_texture);
        GL.DeleteRenderbuffer(_renderbuffer);

        _texture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _width, _height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, 0);

        _renderbuffer = (uint)GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _renderbuffer);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, _width, _height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _renderbuffer);

        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
        {
            Logger.Error($"Framebuffer resize failed! Status: {status}");
        }
        else
        {
            Logger.Info($"Framebuffer resized: {_width}x{_height}");
        }
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        CreateFramebuffer();
    }
}
