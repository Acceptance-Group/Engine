using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using EngineVec2 = Engine.Math.Vector2;
using EngineVec4 = Engine.Math.Vector4;
using EngineVec3 = Engine.Math.Vector3;
using EngineMatrix4 = Engine.Math.Matrix4;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;
using Engine.Graphics;
using Engine.Renderer;
using Engine.Editor.Graphs;
using Engine.Editor.Graphs.Nodes;

namespace Engine.Editor;

public class MaterialEditorWindow
{
    private readonly EditorApplication _editor;
    private readonly MaterialEditorTheme _theme = MaterialEditorTheme.CreateDefault();
    private readonly List<GraphNode> _nodes = new List<GraphNode>();
    private readonly List<NodeConnection> _connections = new List<NodeConnection>();
    private readonly Dictionary<string, string[]> _nodePalette = new Dictionary<string, string[]>
    {
        { "Inputs", new[] { "Texture Sample", "Vector 3", "Vector 4", "Scalar Parameter", "TexCoord" } },
        { "Math", new[] { "Multiply", "Add", "Subtract", "Divide", "Lerp", "Power", "Max", "Min" } },
        { "Math Functions", new[] { "Abs", "Floor", "Ceil", "Frac", "Sin", "Cos" } },
        { "Utility", new[] { "Mask", "Clamp", "Time", "Delta Time", "Panner", "Hue Shift", "Rotator" } },
        { "Conversion", new[] { "To Vector 3", "To Vector 4", "From Vector 3", "From Vector 4", "To Scalar", "To Vector 2" } },
        { "Material", new[] { "Material Output" } }
    };
    private static readonly System.Numerics.Vector4 NodeColorTexCoord = new System.Numerics.Vector4(0.74f, 0.18f, 0.12f, 1f);
    private static readonly System.Numerics.Vector4 NodeColorTexture = new System.Numerics.Vector4(0.1f, 0.42f, 0.66f, 1f);
    private static readonly System.Numerics.Vector4 NodeColorMultiply = new System.Numerics.Vector4(0.24f, 0.48f, 0.23f, 1f);
    private static readonly System.Numerics.Vector4 NodeColorLerp = new System.Numerics.Vector4(0.18f, 0.35f, 0.45f, 1f);
    private static readonly System.Numerics.Vector4 NodeColorUtility = new System.Numerics.Vector4(0.27f, 0.37f, 0.6f, 1f);
    private static readonly System.Numerics.Vector4 NodeColorOutput = new System.Numerics.Vector4(0.18f, 0.18f, 0.18f, 1f);
    private static readonly System.Numerics.Vector4 NodeColorConstant = new System.Numerics.Vector4(0.3f, 0.7f, 0.4f, 0.95f);
    private const float InspectorWidth = 280f;
    private Vector2 _inspectorMin = Vector2.Zero;
    private Vector2 _inspectorMax = Vector2.Zero;

    private NVector2 _canvasOffset = new NVector2(120f, 120f);
    private float _canvasZoom = 1.0f;
    private int _nextNodeId = 1;
    private int? _selectedNodeId;
    private int? _draggingNodeId;
    private NVector2 _draggingNodeOffset;
    private (int nodeId, PinDirection direction, int pinIndex)? _hoveredPin;
    private (int nodeId, PinDirection direction, int pinIndex)? _activeLink;
    private (int nodeId, PinDirection direction, int pinIndex)? _contextPin;
    private NVector2 _activeLinkMousePos;
    private int? _selectedConnectionIndex;
    private int? _hoveredConnectionIndex;
    private int? _contextConnectionIndex;
    
    private uint _previewFramebuffer;
    private uint _previewTexture;
    private uint _previewDepthBuffer;
    private int _previewWidth = 240;
    private int _previewHeight = 200;
    private Mesh? _previewSphere;
    private Camera? _previewCamera;
    private float _previewRotation = 0f;
    private FXAA? _previewFXAA;
    private Engine.Renderer.Texture? _previewDiffuseTexture;
    private string? _cachedPreviewTexturePath;

    public bool Visible { get; set; }

    public MaterialEditorWindow(EditorApplication editor)
    {
        _editor = editor;
        InitializeDemoGraph();
        InitializePreview();
    }
    
    private void InitializePreview()
    {
        _previewSphere = Primitives.CreateSphere(1.0f, 32);
        _previewCamera = new Camera();
        _previewCamera.Position = new EngineVec3(0f, 0f, 3f);
        _previewCamera.LookAt(EngineVec3.Zero, EngineVec3.Up);
        _previewCamera.AspectRatio = (float)_previewWidth / _previewHeight;
        _previewCamera.FOV = 45f * System.MathF.PI / 180f;
        
        CreatePreviewFramebuffer();
        _previewFXAA = new FXAA(_previewWidth, _previewHeight);
    }
    
    private void CreatePreviewFramebuffer()
    {
        _previewFramebuffer = (uint)GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previewFramebuffer);
        
        _previewTexture = (uint)GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _previewTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _previewWidth, _previewHeight, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _previewTexture, 0);
        
        _previewDepthBuffer = (uint)GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _previewDepthBuffer);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, _previewWidth, _previewHeight);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _previewDepthBuffer);
        
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
    }

    public void Render()
    {
        if (!Visible)
            return;

        var io = ImGui.GetIO();
        TimeNode.UpdateTime(io.DeltaTime);
        DeltaTimeNode.UpdateDeltaTime(io.DeltaTime);

        bool visible = Visible;
        ImGui.SetNextWindowSize(new Vector2(1100f, 640f), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Material Editor", ref visible, ImGuiWindowFlags.NoCollapse))
        {
            Visible = visible;
            DrawToolbar();
            DrawBody();
            HandleKeyboardInput();
        }

        ImGui.End();
    }

    private void HandleKeyboardInput()
    {
        var io = ImGui.GetIO();
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsAnyItemActive())
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Delete))
            {
                if (_selectedConnectionIndex.HasValue)
                {
                    int idx = _selectedConnectionIndex.Value;
                    if (idx >= 0 && idx < _connections.Count)
                    {
                        _connections.RemoveAt(idx);
                    }
                    _selectedConnectionIndex = null;
                    _contextConnectionIndex = null;
                }
                else if (_selectedNodeId.HasValue)
                {
                    DeleteSelectedNode();
                }
            }
        }
    }

    private void DeleteSelectedNode()
    {
        if (!_selectedNodeId.HasValue)
            return;

        int nodeId = _selectedNodeId.Value;
        _connections.RemoveAll(c => c.OutputNodeId == nodeId || c.InputNodeId == nodeId);
        _nodes.RemoveAll(n => n.Id == nodeId);
        _selectedNodeId = null;
        _draggingNodeId = null;
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("New Graph"))
        {
            InitializeDemoGraph();
            }

            ImGui.SameLine();
        ImGui.BeginDisabled();
        ImGui.Button("Import Material");
        ImGui.SameLine();
        ImGui.Button("Export Material");
        ImGui.EndDisabled();

        ImGui.Separator();
    }

    private void DrawBody()
    {
        float sidebarWidth = 260f;

        ImGui.BeginChild("MaterialSidebar", new Vector2(sidebarWidth, 0f), ImGuiChildFlags.Border, ImGuiWindowFlags.None);
        DrawSidebar();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("MaterialGraphArea", new Vector2(-InspectorWidth, 0f), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawGraphArea();
        ImGui.EndChild();

            ImGui.SameLine();

        ImGui.BeginChild("MaterialInspector", new Vector2(InspectorWidth, 0f), ImGuiChildFlags.Border, ImGuiWindowFlags.None);
        DrawInspector();
        ImGui.EndChild();
    }

    private void DrawSidebar()
    {
        Vector2 availableSize = ImGui.GetContentRegionAvail();
        float previewHeaderHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().ItemSpacing.Y * 2f + ImGui.GetStyle().FramePadding.Y * 2f;
        float previewContentHeight = 200f;
        float previewHeight = previewHeaderHeight + previewContentHeight + ImGui.GetStyle().ItemSpacing.Y;

        ImGui.BeginChild("SidebarContent", new Vector2(0f, availableSize.Y - previewHeight), ImGuiChildFlags.None, ImGuiWindowFlags.None);

        ImGui.Text("Node Palette");
            ImGui.Separator();

        foreach (var entry in _nodePalette)
        {
            if (ImGui.TreeNode(entry.Key))
            {
                foreach (var label in entry.Value)
                {
                    if (ImGui.Selectable(label, false, ImGuiSelectableFlags.DontClosePopups))
                    {
                        var io = ImGui.GetIO();
                        Vector2 canvasSize = ImGui.GetContentRegionAvail();
                        Vector2 centerPos = new Vector2(canvasSize.X * 0.5f, canvasSize.Y * 0.5f);
                        Vector2 graphPos = (centerPos - _canvasOffset) / _canvasZoom;
                        CreateNodeFromType(label, graphPos);
                    }

                    if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                    {
                        unsafe
                        {
                            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(label);
                            fixed (byte* ptr = bytes)
                            {
                                ImGui.SetDragDropPayload("NODE_TYPE", (nint)ptr, (uint)bytes.Length, ImGuiCond.Once);
                            }
                        }
                        ImGui.Text(label);
                        ImGui.EndDragDropSource();
                    }
                }
                ImGui.TreePop();
            }
        }
        
        ImGui.EndChild();

        ImGui.Text("Material Preview");
        ImGui.Separator();
        ImGui.BeginChild("MaterialPreviewContainer", new Vector2(0f, previewContentHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawMaterialPreview();
        ImGui.EndChild();
    }

    private void DrawMaterialPreview()
    {
        Vector2 availableSize = ImGui.GetContentRegionAvail();
        float aspectRatio = 1.2f;
        float previewWidth = availableSize.X;
        float previewHeight = System.MathF.Min(availableSize.Y, previewWidth / aspectRatio);
        previewWidth = previewHeight * aspectRatio;
        
        Vector2 previewSize = new Vector2(previewWidth, previewHeight);
        Vector2 cursor = ImGui.GetCursorScreenPos();
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        
        drawList.AddRectFilled(cursor, cursor + previewSize, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.16f, 1f)), 6f);
        
        float checkerSize = 16f;
        for (int y = 0; y < (int)(previewSize.Y / checkerSize); y++)
        {
            for (int x = 0; x < (int)(previewSize.X / checkerSize); x++)
            {
                bool isLight = (x + y) % 2 == 0;
                Vector2 checkPos = cursor + new Vector2(x * checkerSize, y * checkerSize);
                drawList.AddRectFilled(checkPos, checkPos + new Vector2(checkerSize, checkerSize), 
                    ImGui.GetColorU32(isLight ? new Vector4(0.15f, 0.15f, 0.18f, 1f) : new Vector4(0.1f, 0.1f, 0.12f, 1f)));
            }
        }
        
        MaterialData? materialData = EvaluateMaterial();
        
        if (_previewSphere != null && _previewCamera != null && _editor.DefaultShader != null)
        {
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previewFramebuffer);
            GL.Viewport(0, 0, _previewWidth, _previewHeight);
            GL.ClearColor(0.12f, 0.12f, 0.16f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            
            EngineMatrix4 rotation = EngineMatrix4.CreateRotationY(_previewRotation);
            EngineMatrix4 model = rotation;
            EngineMatrix4 view = _previewCamera.ViewMatrix;
            EngineMatrix4 projection = _previewCamera.ProjectionMatrix;
            EngineMatrix4 mvp = projection * view * model;
            
            _editor.DefaultShader.Use();
            _editor.DefaultShader.SetMatrix4("uModel", model);
            _editor.DefaultShader.SetMatrix4("uView", view);
            _editor.DefaultShader.SetMatrix4("uMVP", mvp);
            
            EngineVec2 previewUVScale = materialData?.UVScale ?? EngineVec2.One;
            EngineVec2 previewUVOffset = materialData?.UVOffset ?? EngineVec2.Zero;
            _editor.DefaultShader.SetVector2("uUVScale", previewUVScale);
            _editor.DefaultShader.SetVector2("uUVOffset", previewUVOffset);
            
            bool useTexture = false;
            if (materialData != null && !string.IsNullOrEmpty(materialData.DiffuseMapPath))
            {
                if (_cachedPreviewTexturePath != materialData.DiffuseMapPath)
                {
                    _previewDiffuseTexture?.Dispose();
                    _previewDiffuseTexture = null;
                    
                    try
                    {
                        if (File.Exists(materialData.DiffuseMapPath))
                        {
                            var textureData = TextureFileLoader.Load(materialData.DiffuseMapPath);
                            byte[] rgbaBytes = new byte[textureData.Width * textureData.Height * 4];
                            for (int i = 0; i < textureData.Pixels.Length; i++)
                            {
                                float r = System.Math.Clamp(textureData.Pixels[i].X, 0f, 1f);
                                float g = System.Math.Clamp(textureData.Pixels[i].Y, 0f, 1f);
                                float b = System.Math.Clamp(textureData.Pixels[i].Z, 0f, 1f);
                                float a = System.Math.Clamp(textureData.Pixels[i].W, 0f, 1f);
                                rgbaBytes[i * 4 + 0] = (byte)(r * 255f);
                                rgbaBytes[i * 4 + 1] = (byte)(g * 255f);
                                rgbaBytes[i * 4 + 2] = (byte)(b * 255f);
                                rgbaBytes[i * 4 + 3] = (byte)(a * 255f);
                            }
                            unsafe
                            {
                                fixed (byte* ptr = rgbaBytes)
                                {
                                    _previewDiffuseTexture = new Engine.Renderer.Texture(
                                        textureData.Width,
                                        textureData.Height,
                                        (IntPtr)ptr
                                    );
                                }
                            }
                            _cachedPreviewTexturePath = materialData.DiffuseMapPath;
                            useTexture = true;
                        }
                    }
                    catch
                    {
                        useTexture = false;
                    }
                }
                else if (_previewDiffuseTexture != null)
                {
                    useTexture = true;
                }
            }
            
            if (useTexture && _previewDiffuseTexture != null)
            {
                _editor.DefaultShader.SetInt("uUseTexture", 1);
                _previewDiffuseTexture.Bind(0);
                _editor.DefaultShader.SetInt("uTexture", 0);
            }
            else
            {
            _editor.DefaultShader.SetInt("uUseTexture", 0);
            }
            
            if (materialData != null)
            {
                EngineVec4 color = materialData.DiffuseColor;
                _editor.DefaultShader.SetVector4("uColor", color);
                _editor.DefaultShader.SetFloat("uMetallic", materialData.Metallic);
                _editor.DefaultShader.SetFloat("uSpecular", materialData.Specular);
                _editor.DefaultShader.SetFloat("uRoughness", materialData.Roughness);
            }
            else
            {
                _editor.DefaultShader.SetVector4("uColor", new EngineVec4(0.8f, 0.6f, 0.4f, 1.0f));
                _editor.DefaultShader.SetFloat("uMetallic", 0.0f);
                _editor.DefaultShader.SetFloat("uSpecular", 0.5f);
                _editor.DefaultShader.SetFloat("uRoughness", 0.5f);
            }
            
            _previewSphere.Draw();
            
            if (useTexture && _previewDiffuseTexture != null)
            {
                _previewDiffuseTexture.Unbind();
            }
            
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            
            if (_editor.DefaultShader != null)
            {
                _editor.DefaultShader.SetInt("uUseTexture", 0);
                _editor.DefaultShader.SetVector2("uUVScale", EngineVec2.One);
                _editor.DefaultShader.SetVector2("uUVOffset", EngineVec2.Zero);
                _editor.DefaultShader.SetFloat("uMetallic", 0.0f);
                _editor.DefaultShader.SetFloat("uSpecular", 0.5f);
                _editor.DefaultShader.SetFloat("uRoughness", 0.5f);
            }

            uint finalTexture = _previewTexture;
            if (_previewFXAA == null)
            {
                _previewFXAA = new FXAA(_previewWidth, _previewHeight);
            }
            else
            {
                _previewFXAA.Resize(_previewWidth, _previewHeight);
            }

            _previewFXAA.Render(_previewTexture);
            finalTexture = _previewFXAA.Texture;
            
            ImGui.SetCursorScreenPos(cursor);
            ImGui.Image((IntPtr)finalTexture, previewSize, new NVector2(0, 1), new NVector2(1, 0));
        }
        else
        {
            drawList.AddText(cursor + new NVector2(10f, 10f), ImGui.GetColorU32(new NVector4(1f, 1f, 1f, 0.9f)), "No Material");
        }
        
        ImGui.Dummy(previewSize);
    }

    private void DrawInspector()
    {
        _inspectorMin = ImGui.GetWindowPos();
        _inspectorMax = _inspectorMin + ImGui.GetWindowSize();
        
        if (!_selectedNodeId.HasValue)
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No node selected");
            ImGui.Spacing();
            ImGui.TextWrapped("Select a node to edit its properties");
            return;
        }

        GraphNode? node = GetNode(_selectedNodeId.Value);
        if (node == null)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.3f, 0.3f, 1f), "Node not found");
            return;
        }

        ImGui.Text(node.Title);
        if (!string.IsNullOrEmpty(node.Subtitle))
        {
            ImGui.TextDisabled(node.Subtitle);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        node.DrawInspector(_connections);
    }

    private void DrawGraphArea()
    {
        Vector2 canvasPos = ImGui.GetCursorScreenPos();
        Vector2 canvasSize = ImGui.GetContentRegionAvail();
        if (canvasSize.X < 1f) canvasSize.X = 1f;
        if (canvasSize.Y < 1f) canvasSize.Y = 1f;

        ImGui.InvisibleButton("MaterialGraphCanvas", canvasSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
        bool canvasHovered = ImGui.IsItemHovered();

        HandleCanvasInput(canvasPos, canvasHovered);

        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        Vector2 canvasEnd = canvasPos + canvasSize;
        drawList.AddRectFilled(canvasPos, canvasEnd, ImGui.GetColorU32(_theme.CanvasBackground));
        drawList.AddRect(canvasPos, canvasEnd, ImGui.GetColorU32(_theme.CanvasBorder));

        if (ImGui.BeginDragDropTarget())
        {
            unsafe
            {
                var payload = ImGui.AcceptDragDropPayload("NODE_TYPE");
                if (payload.NativePtr != null && payload.IsDelivery())
                {
                    byte* data = (byte*)payload.Data;
                    string nodeType = System.Text.Encoding.UTF8.GetString(data, (int)payload.DataSize);
                    var io = ImGui.GetIO();
                    Vector2 graphPos = (io.MousePos - canvasPos - _canvasOffset) / _canvasZoom;
                    CreateNodeFromType(nodeType, graphPos);
                }
            }
            ImGui.EndDragDropTarget();
        }

        GraphDrawCache cache = BuildGraphDrawCache(canvasPos);
        bool changed = false;
        changed |= HandlePinInteractions(cache);
        changed |= HandleNodeDragging(cache, canvasPos);
        HandleNodeContextMenu(cache);
        if (changed)
        {
            cache = BuildGraphDrawCache(canvasPos);
        }

        DrawCanvasGrid(drawList, canvasPos, canvasSize);
        UpdateConnectionHover(cache.PinCenters);
        DrawConnections(drawList, cache.PinCenters);
        DrawNodes(drawList, cache);
        DrawActiveLinkPreview(drawList, cache);

        if (canvasHovered && _draggingNodeId == null && !_activeLink.HasValue)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private bool HandleNodeContextMenu(GraphDrawCache cache)
    {
        bool changed = false;
        var io = ImGui.GetIO();
        
        if (IsMouseOverInspector())
        {
            return false;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && _contextConnectionIndex == null)
        {
            if (_hoveredPin.HasValue)
            {
                _contextPin = _hoveredPin.Value;
                ImGui.OpenPopup($"PinContextMenu_{_hoveredPin.Value.nodeId}_{_hoveredPin.Value.pinIndex}");
            }
            else
            {
                _contextPin = null;
            for (int i = cache.NodeStates.Count - 1; i >= 0; --i)
            {
                var state = cache.NodeStates[i];
                if (ContainsPoint(state.Min, state.Max, io.MousePos))
                {
                    _selectedNodeId = state.Node.Id;
                    break;
                }
                }
            }
        }

        if (_contextPin.HasValue)
        {
            var (nodeId, direction, pinIndex) = _contextPin.Value;
            GraphNode? node = GetNode(nodeId);
            if (node != null && direction == PinDirection.Output && pinIndex < node.OutputPins.Count)
            {
                var pin = node.OutputPins[pinIndex];
                string popupId = $"PinContextMenu_{nodeId}_{pinIndex}";
                if (ImGui.BeginPopupContextItem(popupId))
                {
                    var typeDef = NodeValueTypeSystem.GetDefinition(pin.Kind);
                    if (typeDef != null && typeDef.CanSplit)
                    {
                        if (!pin.IsSplitPin)
                        {
                            if (ImGui.MenuItem("Split"))
                            {
                                SplitPin(node, pinIndex);
                                changed = true;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                        else
                        {
                            if (ImGui.MenuItem("Promote to Vector"))
                            {
                                PromotePin(node, pinIndex);
                                changed = true;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }
                    ImGui.EndPopup();
                }
            }
        }
        else if (_selectedNodeId.HasValue && ImGui.BeginPopupContextWindow("NodeContextMenu"))
        {
            if (ImGui.MenuItem("Delete"))
            {
                DeleteSelectedNode();
                changed = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        
        return changed;
    }
    
    private void SplitPin(GraphNode node, int pinIndex)
    {
        if (pinIndex >= node.OutputPins.Count)
            return;
            
        var originalPin = node.OutputPins[pinIndex];
        if (originalPin.IsSplitPin)
            return;
        
        var typeDef = NodeValueTypeSystem.GetDefinition(originalPin.Kind);
        if (typeDef == null || !typeDef.CanSplit)
            return;
        
        int insertIndex = pinIndex;
        node.OutputPins.RemoveAt(insertIndex);
        
        string[] componentNames = typeDef.ComponentNames;
        for (int i = 0; i < componentNames.Length; i++)
        {
            node.OutputPins.Insert(insertIndex + i, new NodePin(componentNames[i], NodeValueKind.Scalar, true, i));
        }
        
        _connections.RemoveAll(c => c.OutputNodeId == node.Id && c.OutputIndex == pinIndex);
    }
    
    private void PromotePin(GraphNode node, int pinIndex)
    {
        if (pinIndex >= node.OutputPins.Count)
            return;
            
        var pin = node.OutputPins[pinIndex];
        if (!pin.IsSplitPin)
            return;
        
        int startIndex = pinIndex;
        while (startIndex > 0 && node.OutputPins[startIndex - 1].IsSplitPin)
        {
            startIndex--;
        }
        
        int endIndex = pinIndex;
        while (endIndex < node.OutputPins.Count - 1 && node.OutputPins[endIndex + 1].IsSplitPin)
        {
            endIndex++;
        }
        
        int count = endIndex - startIndex + 1;
        
        NodeValueKind originalKind = NodeValueKind.Color;
        string originalLabel = "Color";
        
        foreach (var kind in Enum.GetValues<NodeValueKind>())
        {
            var typeDef = NodeValueTypeSystem.GetDefinition(kind);
            if (typeDef != null && typeDef.CanSplit && typeDef.ComponentCount == count)
            {
                bool matches = true;
                for (int i = 0; i < count; i++)
                {
                    if (startIndex + i >= node.OutputPins.Count || 
                        node.OutputPins[startIndex + i].Label != typeDef.ComponentNames[i])
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                {
                    originalKind = kind;
                    originalLabel = kind == NodeValueKind.Color ? "RGBA" : 
                                   kind == NodeValueKind.UV ? "UV" :
                                   kind == NodeValueKind.Vector ? "Vector" :
                                   kind == NodeValueKind.Normal ? "Normal" :
                                   kind == NodeValueKind.Emission ? "Emission" : "Color";
                    break;
                }
            }
        }
        
        string newLabel = originalLabel;
        NodeValueKind newKind = originalKind;
        
        List<NodeConnection> connectionsToUpdate = new List<NodeConnection>();
        for (int i = _connections.Count - 1; i >= 0; i--)
        {
            var conn = _connections[i];
            if (conn.OutputNodeId == node.Id)
            {
                if (conn.OutputIndex >= startIndex && conn.OutputIndex <= endIndex)
                {
                    connectionsToUpdate.Add(conn);
                    _connections.RemoveAt(i);
                }
                else if (conn.OutputIndex > endIndex)
                {
                    _connections[i] = new NodeConnection(conn.OutputNodeId, conn.OutputIndex - count + 1, conn.InputNodeId, conn.InputIndex, conn.Color);
                }
            }
        }
        
        for (int i = endIndex; i >= startIndex; i--)
        {
            node.OutputPins.RemoveAt(i);
        }
        
        node.OutputPins.Insert(startIndex, new NodePin(newLabel, newKind));
        
        foreach (var conn in connectionsToUpdate)
        {
            _connections.Add(new NodeConnection(node.Id, startIndex, conn.InputNodeId, conn.InputIndex, conn.Color));
        }
    }

    private void HandleCanvasInput(Vector2 canvasOrigin, bool hovered)
    {
        var io = ImGui.GetIO();

        if (hovered && ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
        {
            _canvasOffset += io.MouseDelta;
        }

        if (hovered && System.MathF.Abs(io.MouseWheel) > float.Epsilon)
        {
            float previousZoom = _canvasZoom;
            _canvasZoom = System.Math.Clamp(_canvasZoom + io.MouseWheel * 0.1f, 0.5f, 1.8f);

            Vector2 mouseCanvasSpace = (io.MousePos - canvasOrigin - _canvasOffset) / previousZoom;
            _canvasOffset = io.MousePos - canvasOrigin - mouseCanvasSpace * _canvasZoom;
        }
    }

    private void DrawCanvasGrid(ImDrawListPtr drawList, Vector2 canvasPos, Vector2 canvasSize)
    {
        float baseTileSize = 64f;
        float tileSize = baseTileSize * _canvasZoom;
        while (tileSize < 24f)
        {
            baseTileSize *= 2f;
            tileSize = baseTileSize * _canvasZoom;
        }
        while (tileSize > 140f)
        {
            baseTileSize *= 0.5f;
            tileSize = baseTileSize * _canvasZoom;
        }

        float tileOffsetX = Mod(_canvasOffset.X, tileSize) - tileSize;
        float tileOffsetY = Mod(_canvasOffset.Y, tileSize) - tileSize;

        int initialTileX = (int)System.MathF.Floor((_canvasOffset.X + tileOffsetX) / tileSize);
        int initialTileY = (int)System.MathF.Floor((_canvasOffset.Y + tileOffsetY) / tileSize);
        int tileXIndex = initialTileX;
        uint tileColor = ImGui.GetColorU32(_theme.GridTileDark);
        for (float x = tileOffsetX; x < canvasSize.X + tileSize; x += tileSize, tileXIndex++)
        {
            int tileYIndex = initialTileY;
            for (float y = tileOffsetY; y < canvasSize.Y + tileSize; y += tileSize, tileYIndex++)
            {
                Vector2 min = canvasPos + new Vector2(x, y);
                Vector2 max = min + new Vector2(tileSize, tileSize);
                drawList.AddRectFilled(min, max, tileColor);
            }
        }

        float majorStep = tileSize;
        float minorStep = tileSize * 0.25f;
        if (minorStep < 8f) minorStep = 8f;

        uint minorColor = ImGui.GetColorU32(_theme.GridMinor);
        uint majorColor = ImGui.GetColorU32(_theme.GridMajor);

        float minorOffsetX = Mod(_canvasOffset.X, minorStep) - minorStep;
        float minorOffsetY = Mod(_canvasOffset.Y, minorStep) - minorStep;
        float majorOffsetX = Mod(_canvasOffset.X, majorStep) - majorStep;
        float majorOffsetY = Mod(_canvasOffset.Y, majorStep) - majorStep;

        for (float x = minorOffsetX; x < canvasSize.X + minorStep; x += minorStep)
        {
            drawList.AddLine(
                new Vector2(canvasPos.X + x, canvasPos.Y),
                new Vector2(canvasPos.X + x, canvasPos.Y + canvasSize.Y),
                minorColor);
        }

        for (float y = minorOffsetY; y < canvasSize.Y + minorStep; y += minorStep)
        {
            drawList.AddLine(
                new Vector2(canvasPos.X, canvasPos.Y + y),
                new Vector2(canvasPos.X + canvasSize.X, canvasPos.Y + y),
                minorColor);
        }

        for (float x = majorOffsetX; x < canvasSize.X + majorStep; x += majorStep)
        {
            drawList.AddLine(
                new Vector2(canvasPos.X + x, canvasPos.Y),
                new Vector2(canvasPos.X + x, canvasPos.Y + canvasSize.Y),
                majorColor);
        }

        for (float y = majorOffsetY; y < canvasSize.Y + majorStep; y += majorStep)
        {
            drawList.AddLine(
                new Vector2(canvasPos.X, canvasPos.Y + y),
                new Vector2(canvasPos.X + canvasSize.X, canvasPos.Y + y),
                majorColor);
        }
    }

    private GraphDrawCache BuildGraphDrawCache(Vector2 canvasOrigin)
    {
        var cache = new GraphDrawCache(_nodes.Count, 12f * _canvasZoom);
        foreach (var node in _nodes)
        {
            Vector2 nodePos = canvasOrigin + _canvasOffset + node.Position * _canvasZoom;
            Vector2 nodeSize = node.Size * _canvasZoom;
            float minNodeWidth = 220f * _canvasZoom;
            if (nodeSize.X < minNodeWidth)
            {
                nodeSize.X = minNodeWidth;
            }

            float dynamicColumnRatio = nodeSize.X < 360f * _canvasZoom ? 0.53f : 0.58f;

            Vector2 nodeMin = nodePos;
            Vector2 nodeMax = nodePos + nodeSize;

            Vector2 titleSize = ImGui.CalcTextSize(node.Title ?? string.Empty);
            Vector2 subtitleSize = node.Subtitle != null ? ImGui.CalcTextSize(node.Subtitle) : Vector2.Zero;
            float headerNeeded = 6f * _canvasZoom + titleSize.Y + (subtitleSize.Y > 0 ? 4f * _canvasZoom + subtitleSize.Y : 0f);
            float headerHeight = System.MathF.Max(30f * _canvasZoom, headerNeeded);
            float bodyTop = nodeMin.Y + headerHeight + 2f * _canvasZoom;
            float bodyBottom = nodeMax.Y - 4.5f * _canvasZoom;
            float bodyHeight = System.MathF.Max(bodyBottom - bodyTop, 12f * _canvasZoom);

            float pinOffset = 8f * _canvasZoom;
            float pinRowHeight = 24f * _canvasZoom;
            float pinRowSpacing = 4f * _canvasZoom;
            float pinTopPadding = 2f * _canvasZoom;
            float pinBottomPadding = 8f * _canvasZoom;
            int maxPins = System.Math.Max(node.InputPins.Count, node.OutputPins.Count);
            float requiredBody = maxPins > 0
                ? pinTopPadding + maxPins * pinRowHeight + (maxPins - 1) * pinRowSpacing + pinBottomPadding
                : pinTopPadding + pinBottomPadding;

            bodyHeight = System.MathF.Max(requiredBody, 0f);
            bodyBottom = bodyTop + bodyHeight;
            nodeSize.Y = bodyBottom - nodeMin.Y + 4.5f * _canvasZoom;
            nodeMax = nodeMin + nodeSize;

            bool hasInputs = node.InputPins.Count > 0;
            bool hasOutputs = node.OutputPins.Count > 0;
            float columnSplit;
            float tightMargin = 6f * _canvasZoom;
            if (hasInputs && hasOutputs)
            {
                columnSplit = nodeMin.X + nodeSize.X * dynamicColumnRatio;
            }
            else if (hasInputs)
            {
                columnSplit = nodeMax.X - tightMargin;
            }
            else
            {
                columnSplit = nodeMin.X + tightMargin;
            }

            node.Size = nodeSize / _canvasZoom;

            var state = new NodeDrawState(node, nodeMin, nodeMax, headerHeight, bodyTop, bodyHeight, pinOffset, columnSplit, pinRowHeight, pinRowSpacing);
            cache.NodeStates.Add(state);

            float startY = bodyTop + pinTopPadding + pinRowHeight * 0.5f;

            for (int i = 0; i < node.InputPins.Count; i++)
            {
                float pinY = startY + i * (pinRowHeight + pinRowSpacing);
                Vector2 pinCenter = new Vector2(nodeMin.X - state.PinOffset * 0.5f, pinY);
                state.InputPinCenters.Add(pinCenter);
                cache.PinCenters[(node.Id, PinDirection.Input, i)] = pinCenter;
            }

            for (int i = 0; i < node.OutputPins.Count; i++)
            {
                float pinY = startY + i * (pinRowHeight + pinRowSpacing);
                Vector2 pinCenter = new Vector2(nodeMax.X + state.PinOffset * 0.5f, pinY);
                state.OutputPinCenters.Add(pinCenter);
                cache.PinCenters[(node.Id, PinDirection.Output, i)] = pinCenter;
            }
        }
        return cache;
    }

    private bool HandleNodeDragging(GraphDrawCache cache, Vector2 canvasOrigin)
    {
        var io = ImGui.GetIO();
        bool changed = false;

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !_activeLink.HasValue && !_hoveredPin.HasValue)
        {
            if (IsMouseOverInspector())
            {
                return false;
            }

            bool clickedNode = false;
            for (int i = cache.NodeStates.Count - 1; i >= 0; --i)
            {
                var state = cache.NodeStates[i];
                if (ContainsPoint(state.Min, state.Max, io.MousePos))
                {
                    clickedNode = true;
                    _selectedNodeId = state.Node.Id;
                    _draggingNodeId = state.Node.Id;
                    _draggingNodeOffset = io.MousePos - state.Min;
                    MoveNodeToTop(state.Node.Id);
                    changed = true;
                    break;
                }
            }

            if (!clickedNode)
            {
                _selectedNodeId = null;
            }
        }

        if (_draggingNodeId.HasValue && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var node = GetNode(_draggingNodeId.Value);
            if (node != null)
            {
                Vector2 newMinScreen = io.MousePos - _draggingNodeOffset;
                Vector2 graphPosition = (newMinScreen - canvasOrigin - _canvasOffset) / _canvasZoom;
                node.Position = graphPosition;
                changed = true;
            }
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _draggingNodeId = null;
        }

        return changed;
    }

    private bool HandlePinInteractions(GraphDrawCache cache)
    {
        var io = ImGui.GetIO();
        bool changed = false;
        _hoveredPin = null;

        if (IsMouseOverInspector())
        {
            return false;
        }

        float detectionRadius = cache.PinHitRadius;
        float closest = detectionRadius;
        foreach (var pin in cache.PinCenters)
        {
            float distance = Vector2.Distance(io.MousePos, pin.Value);
            if (distance <= closest)
            {
                _hoveredPin = pin.Key;
                closest = distance;
            }
        }

        if (_hoveredPin.HasValue && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var pin = _hoveredPin.Value;
            if (pin.direction == PinDirection.Output)
            {
                _activeLink = pin;
                _activeLinkMousePos = io.MousePos;
            }
            else if (_activeLink.HasValue && _activeLink.Value.direction == PinDirection.Output)
            {
                changed |= TryCreateConnection(_activeLink.Value, pin);
                _activeLink = null;
            }
        }

        if (_activeLink.HasValue)
        {
            _activeLinkMousePos = io.MousePos;

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                if (_hoveredPin.HasValue && _hoveredPin.Value.direction == PinDirection.Input)
                {
                    changed |= TryCreateConnection(_activeLink.Value, _hoveredPin.Value);
                }
                else
                {
                    float maxConnectionDistance = cache.PinHitRadius * 1.5f;
                    float closestDistance = maxConnectionDistance;
                    (int nodeId, PinDirection direction, int pinIndex)? closestInputPin = null;
                    
                    foreach (var pin in cache.PinCenters)
                    {
                        if (pin.Key.direction == PinDirection.Input)
                        {
                            float distance = Vector2.Distance(io.MousePos, pin.Value);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestInputPin = pin.Key;
                            }
                        }
                    }
                    
                    if (closestInputPin.HasValue)
                    {
                        changed |= TryCreateConnection(_activeLink.Value, closestInputPin.Value);
                    }
                }
                _activeLink = null;
            }
        }

        return changed;
    }

    private void DrawNodes(ImDrawListPtr drawList, GraphDrawCache cache)
    {
        var io = ImGui.GetIO();
        foreach (var state in cache.NodeStates)
        {
            Vector2 min = state.Min;
            Vector2 max = state.Max;
            float rounding = 10f * _canvasZoom;
            Vector2 shadowOffset = new Vector2(8f, 10f) * _canvasZoom * 0.25f;

            uint shadowColor = ImGui.GetColorU32(_theme.NodeShadow);
            drawList.AddRectFilled(min + shadowOffset, max + shadowOffset, shadowColor, rounding + 4f);

            Vector4 headerColor = state.Node.HeaderColor;
            Vector4 bodyColor = _theme.NodeBodyBottom;

            drawList.AddRectFilled(min, max, ImGui.GetColorU32(bodyColor), rounding);
            drawList.AddRectFilled(min, new Vector2(max.X, min.Y + state.HeaderHeight), ImGui.GetColorU32(headerColor), rounding, ImDrawFlags.RoundCornersTop);

            bool isSelected = _selectedNodeId == state.Node.Id;
            bool hovered = ContainsPoint(min, max, io.MousePos);
            uint borderColor = ImGui.GetColorU32(isSelected ? _theme.NodeBorderSelected : hovered ? _theme.NodeBorderHovered : _theme.NodeBorder);
            float borderThickness = isSelected ? 2.4f * _canvasZoom : 1.2f * _canvasZoom;
            drawList.AddRect(min, max, borderColor, rounding, ImDrawFlags.None, borderThickness);

            float fontScale = System.MathF.Round(_canvasZoom * 20f) / 20f;
            ImGui.SetWindowFontScale(fontScale);
            drawList.AddText(min + new Vector2(14f * _canvasZoom, 8f * _canvasZoom), ImGui.GetColorU32(_theme.NodeTitle), state.Node.Title);

            if (!string.IsNullOrEmpty(state.Node.Subtitle))
            {
                drawList.AddText(min + new Vector2(14f * _canvasZoom, state.HeaderHeight - 16f * _canvasZoom), ImGui.GetColorU32(_theme.NodeSubtitle), state.Node.Subtitle);
            }
            ImGui.SetWindowFontScale(1.0f);

            DrawNodePins(drawList, state);
        }
    }

    private void DrawNodePins(ImDrawListPtr drawList, NodeDrawState state)
    {
        float pinRadius = 6f * _canvasZoom;
        float rowHalf = state.PinRowHeight * 0.5f;
        float leftPadding = 12f * _canvasZoom;
        float rightPadding = 12f * _canvasZoom;
        float stubThickness = 2f * _canvasZoom;

        for (int i = 0; i < state.Node.InputPins.Count; i++)
        {
            if (i >= state.InputPinCenters.Count)
                break;
                
            Vector2 center = state.InputPinCenters[i];
            NodeValueKind kind = state.Node.InputPins[i].Kind;
            bool hovered = _hoveredPin.HasValue && _hoveredPin.Value == (state.Node.Id, PinDirection.Input, i);
            float radius = hovered ? pinRadius * 1.25f : pinRadius;
            uint color = ImGui.GetColorU32(ResolvePinColor(kind));

            Vector2 rowMin = new Vector2(state.Min.X + leftPadding, center.Y - rowHalf);
            Vector2 rowMax = new Vector2(state.ColumnSplit - rightPadding, center.Y + rowHalf);
            if (rowMax.X <= rowMin.X)
            {
                rowMax.X = rowMin.X + 12f * _canvasZoom;
            }
            drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(_theme.PinRowBackground), 5f * _canvasZoom);
            drawList.AddRect(rowMin, rowMax, ImGui.GetColorU32(_theme.PinRowBorder), 5f * _canvasZoom, ImDrawFlags.None, 1f * _canvasZoom);

            Vector2 stubMin = new Vector2(center.X, center.Y - stubThickness);
            Vector2 stubMax = new Vector2(rowMin.X, center.Y + stubThickness);
            drawList.AddRectFilled(stubMin, stubMax, color);

            drawList.AddCircleFilled(center, radius, color);
            if (hovered)
            {
                drawList.AddCircle(center, radius + 2f, ImGui.GetColorU32(_theme.PinHoverOutline), 16, 1.3f);
            }

            float fontScale = System.MathF.Round(_canvasZoom * 10f) / 10f;
            ImGui.SetWindowFontScale(fontScale);
            Vector2 textSize = ImGui.CalcTextSize(state.Node.InputPins[i].Label);
            Vector2 textPos = new Vector2(rowMin.X + 8f * _canvasZoom, center.Y - textSize.Y * 0.5f);
            drawList.AddText(textPos, ImGui.GetColorU32(_theme.PinLabelText), state.Node.InputPins[i].Label);
            ImGui.SetWindowFontScale(1.0f);
        }

        for (int i = 0; i < state.Node.OutputPins.Count; i++)
        {
            if (i >= state.OutputPinCenters.Count)
                break;
                
            Vector2 center = state.OutputPinCenters[i];
            var pin = state.Node.OutputPins[i];
            NodeValueKind kind = pin.Kind;
            bool hovered = _hoveredPin.HasValue && _hoveredPin.Value == (state.Node.Id, PinDirection.Output, i);
            float radius = hovered ? pinRadius * 1.25f : pinRadius;
            uint color = ImGui.GetColorU32(ResolvePinColor(kind));

            float splitPinOffset = pin.IsSplitPin ? 8f * _canvasZoom : 0f;

            Vector2 rowMin = new Vector2(state.ColumnSplit + rightPadding + splitPinOffset, center.Y - rowHalf);
            Vector2 rowMax = new Vector2(state.Max.X - leftPadding, center.Y + rowHalf);
            if (rowMax.X <= rowMin.X)
            {
                rowMax.X = rowMin.X + 12f * _canvasZoom;
            }
            drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(_theme.PinRowBackground), 5f * _canvasZoom);
            drawList.AddRect(rowMin, rowMax, ImGui.GetColorU32(_theme.PinRowBorder), 5f * _canvasZoom, ImDrawFlags.None, 1f * _canvasZoom);

            Vector2 stubMin = new Vector2(rowMax.X, center.Y - stubThickness);
            Vector2 stubMax = new Vector2(center.X, center.Y + stubThickness);
            drawList.AddRectFilled(stubMin, stubMax, color);

            drawList.AddCircleFilled(center, radius, color);
            if (hovered)
            {
                drawList.AddCircle(center, radius + 2f, ImGui.GetColorU32(_theme.PinHoverOutline), 16, 1.3f);
            }

            float fontScale = System.MathF.Round(_canvasZoom * 10f) / 10f;
            ImGui.SetWindowFontScale(fontScale);
            Vector2 labelSize = ImGui.CalcTextSize(pin.Label);
            Vector2 textPos = new Vector2(rowMax.X - labelSize.X - 8f * _canvasZoom, center.Y - labelSize.Y * 0.5f);
            drawList.AddText(textPos, ImGui.GetColorU32(_theme.PinLabelText), pin.Label);
            ImGui.SetWindowFontScale(1.0f);
            
            string pinId = $"Pin_{state.Node.Id}_Output_{i}";
            ImGui.SetCursorScreenPos(center - new Vector2(radius, radius));
            ImGui.InvisibleButton(pinId, new Vector2(radius * 2, radius * 2));
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                _contextPin = (state.Node.Id, PinDirection.Output, i);
                ImGui.OpenPopup($"PinContextMenu_{state.Node.Id}_{i}");
            }
        }
    }

    private void UpdateConnectionHover(Dictionary<(int nodeId, PinDirection direction, int index), Vector2> pinCenters)
    {
        var io = ImGui.GetIO();
        _hoveredConnectionIndex = null;

        if (IsMouseOverInspector())
        {
            return;
        }

        float closestDist = 20f * _canvasZoom;
        for (int i = 0; i < _connections.Count; i++)
        {
            var conn = _connections[i];
            if (!pinCenters.TryGetValue((conn.OutputNodeId, PinDirection.Output, conn.OutputIndex), out var start))
                continue;
            if (!pinCenters.TryGetValue((conn.InputNodeId, PinDirection.Input, conn.InputIndex), out var end))
                continue;

            float dist = DistanceToBezier(io.MousePos, start, end);
            if (dist < closestDist)
            {
                closestDist = dist;
                _hoveredConnectionIndex = i;
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredConnectionIndex.HasValue && !_activeLink.HasValue && !_hoveredPin.HasValue)
        {
            _selectedConnectionIndex = _hoveredConnectionIndex;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && _hoveredConnectionIndex.HasValue)
        {
            _contextConnectionIndex = _hoveredConnectionIndex;
            _selectedConnectionIndex = _hoveredConnectionIndex;
        }
    }

    private float DistanceToBezier(Vector2 point, Vector2 start, Vector2 end)
    {
        float distance = System.MathF.Max(System.MathF.Abs(end.X - start.X), 40f);
        Vector2 controlOffset = new Vector2(distance * 0.5f, 0f);
        Vector2 control1 = start + controlOffset;
        Vector2 control2 = end - controlOffset;

        float minDist = float.MaxValue;
        for (int i = 0; i <= 32; i++)
        {
            float t = i / 32f;
            Vector2 bezierPoint = BezierPoint(start, control1, control2, end, t);
            float dist = Vector2.Distance(point, bezierPoint);
            if (dist < minDist)
                minDist = dist;
        }
        return minDist;
    }

    private Vector2 BezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
    }

    private void DrawConnections(ImDrawListPtr drawList, Dictionary<(int nodeId, PinDirection direction, int index), Vector2> pinCenters)
    {
        for (int i = 0; i < _connections.Count; i++)
        {
            var connection = _connections[i];
            if (!pinCenters.TryGetValue((connection.OutputNodeId, PinDirection.Output, connection.OutputIndex), out var start))
                continue;
            if (!pinCenters.TryGetValue((connection.InputNodeId, PinDirection.Input, connection.InputIndex), out var end))
                continue;

            float distance = System.MathF.Max(System.MathF.Abs(end.X - start.X), 40f);
            Vector2 controlOffset = new Vector2(distance * 0.5f, 0f);
            Vector2 control1 = start + controlOffset;
            Vector2 control2 = end - controlOffset;

            bool isSelected = _selectedConnectionIndex == i;
            bool isHovered = _hoveredConnectionIndex == i;
            Vector4 connectionColor = (isSelected || isHovered) && _theme.OverrideLinkColor
                ? _theme.LinkSelectedColor
                : _theme.OverrideLinkColor ? _theme.LinkColor : connection.Color;
            Vector4 glowColorVec = (isSelected || isHovered) && _theme.OverrideLinkColor
                ? _theme.LinkSelectedGlowColor
                : _theme.OverrideLinkColor ? _theme.LinkGlowColor : new Vector4(connectionColor.X, connectionColor.Y, connectionColor.Z, connectionColor.W * 0.35f);
            uint glowColor = ImGui.GetColorU32(glowColorVec);
            uint baseColor = ImGui.GetColorU32(connectionColor);

            drawList.AddBezierCubic(start, control1, control2, end, glowColor, 5.5f * _canvasZoom);
            drawList.AddBezierCubic(start, control1, control2, end, baseColor, 3f * _canvasZoom);
            drawList.AddCircleFilled(end, 4f * _canvasZoom, baseColor);
        }

        if (_contextConnectionIndex.HasValue && ImGui.BeginPopupContextWindow("ConnectionContextMenu"))
        {
            if (ImGui.MenuItem("Delete"))
            {
                int idx = _contextConnectionIndex.Value;
                if (idx >= 0 && idx < _connections.Count)
                {
                    _connections.RemoveAt(idx);
                }
                _contextConnectionIndex = null;
                _selectedConnectionIndex = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawActiveLinkPreview(ImDrawListPtr drawList, GraphDrawCache cache)
    {
        if (!_activeLink.HasValue)
            return;

        if (!cache.PinCenters.TryGetValue((_activeLink.Value.nodeId, _activeLink.Value.direction, _activeLink.Value.pinIndex), out var start))
            return;

        GraphNode? node = GetNode(_activeLink.Value.nodeId);
        Vector4 color = node != null && _activeLink.Value.pinIndex < node.OutputPins.Count
            ? ResolvePinColor(node.OutputPins[_activeLink.Value.pinIndex].Kind)
            : new Vector4(0.95f, 0.8f, 0.4f, 0.9f);

        Vector2 end = _hoveredPin.HasValue && _hoveredPin.Value.direction == PinDirection.Input
            ? cache.PinCenters[(_hoveredPin.Value.nodeId, _hoveredPin.Value.direction, _hoveredPin.Value.pinIndex)]
            : _activeLinkMousePos;

        float distance = System.MathF.Max(System.MathF.Abs(end.X - start.X), 40f);
        Vector2 controlOffset = new Vector2(distance * 0.5f, 0f);
        Vector2 control1 = start + controlOffset;
        Vector2 control2 = end - controlOffset;

        Vector4 lineColor = _theme.OverrideLinkColor ? _theme.LinkColor : color;
        Vector4 glowColorVec = _theme.OverrideLinkColor ? _theme.LinkGlowColor : new Vector4(lineColor.X, lineColor.Y, lineColor.Z, lineColor.W * 0.35f);

        drawList.AddBezierCubic(start, control1, control2, end, ImGui.GetColorU32(glowColorVec), 5f * _canvasZoom);
        drawList.AddBezierCubic(start, control1, control2, end, ImGui.GetColorU32(lineColor), 2.8f * _canvasZoom);
    }

    private bool TryCreateConnection((int nodeId, PinDirection direction, int pinIndex) outputRef, (int nodeId, PinDirection direction, int pinIndex) inputRef)
    {
        if (outputRef.direction != PinDirection.Output || inputRef.direction != PinDirection.Input)
            return false;

        if (outputRef.nodeId == inputRef.nodeId)
            return false;

        GraphNode? outputNode = GetNode(outputRef.nodeId);
        GraphNode? inputNode = GetNode(inputRef.nodeId);
        if (outputNode == null || inputNode == null)
            return false;

        if (outputRef.pinIndex < 0 || outputRef.pinIndex >= outputNode.OutputPins.Count)
            return false;
        if (inputRef.pinIndex < 0 || inputRef.pinIndex >= inputNode.InputPins.Count)
            return false;

        NodeValueKind outputKind = outputNode.OutputPins[outputRef.pinIndex].Kind;
        NodeValueKind inputKind = inputNode.InputPins[inputRef.pinIndex].Kind;
        if (!AreTypesCompatible(outputKind, inputKind, outputNode, outputRef.pinIndex))
            return false;

        _connections.RemoveAll(c => c.InputNodeId == inputRef.nodeId && c.InputIndex == inputRef.pinIndex);

        Vector4 color = ResolvePinColor(outputKind);
        _connections.Add(new NodeConnection(outputRef.nodeId, outputRef.pinIndex, inputRef.nodeId, inputRef.pinIndex, color));
        return true;
    }

    private GraphNode? GetNode(int id)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].Id == id)
                return _nodes[i];
        }
        return null;
    }

    private void MoveNodeToTop(int nodeId)
    {
        int index = _nodes.FindIndex(n => n.Id == nodeId);
        if (index >= 0 && index != _nodes.Count - 1)
        {
            GraphNode node = _nodes[index];
            _nodes.RemoveAt(index);
            _nodes.Add(node);
        }
    }

    private static bool ContainsPoint(Vector2 min, Vector2 max, Vector2 point)
    {
        return point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
    }

    private bool IsMouseOverInspector()
    {
        var io = ImGui.GetIO();
        return ContainsPoint(_inspectorMin, _inspectorMax, io.MousePos);
    }

    private static bool AreTypesCompatible(NodeValueKind outputKind, NodeValueKind inputKind, GraphNode? outputNode = null, int outputIndex = -1)
    {
        if (outputKind == inputKind)
            return true;

        bool isSplitPin = false;
        if (outputNode != null && outputIndex >= 0 && outputIndex < outputNode.OutputPins.Count)
        {
            var outputPin = outputNode.OutputPins[outputIndex];
            isSplitPin = outputPin.IsSplitPin;
            
            if (isSplitPin && outputPin.Kind == NodeValueKind.Scalar && inputKind == NodeValueKind.Color)
            {
                return false;
            }
        }

        return NodeValueTypeSystem.AreTypesCompatible(outputKind, inputKind);
    }

    private System.Numerics.Vector4 ResolvePinColor(NodeValueKind kind)
    {
        return kind switch
        {
            NodeValueKind.Color => new System.Numerics.Vector4(0.94f, 0.54f, 0.2f, 1f),
            NodeValueKind.Scalar => new System.Numerics.Vector4(0.95f, 0.82f, 0.32f, 1f),
            NodeValueKind.UV => new System.Numerics.Vector4(0.35f, 0.76f, 0.93f, 1f),
            NodeValueKind.Normal => new System.Numerics.Vector4(0.4f, 0.5f, 0.95f, 1f),
            NodeValueKind.Vector => new System.Numerics.Vector4(0.55f, 0.9f, 0.68f, 1f),
            NodeValueKind.Emission => new System.Numerics.Vector4(0.95f, 0.42f, 0.72f, 1f),
            _ => new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1f)
        };
    }

    private void CreateNodeFromType(string nodeType, Vector2 position)
    {
        GraphNode? node = nodeType switch
        {
            "TexCoord" => new TexCoordNode(_nextNodeId++, position),
            "Texture Sample" => new TextureSampleNode(_nextNodeId++, position),
            "Vector 3" => new Vector3Node(_nextNodeId++, position),
            "Vector 4" => new Vector4Node(_nextNodeId++, position),
            "Scalar Parameter" => new ScalarParameterNode(_nextNodeId++, position),
            "To Vector 3" => new ToVector3Node(_nextNodeId++, position),
            "To Vector 4" => new ToVector4Node(_nextNodeId++, position),
            "From Vector 3" => new FromVector3Node(_nextNodeId++, position),
            "From Vector 4" => new FromVector4Node(_nextNodeId++, position),
            "To Scalar" => new ToScalarNode(_nextNodeId++, position),
            "To Vector 2" => new ToVector2Node(_nextNodeId++, position),
            "Multiply" => new MultiplyNode(_nextNodeId++, position),
            "Add" => new AddNode(_nextNodeId++, position),
            "Subtract" => new SubtractNode(_nextNodeId++, position),
            "Divide" => new DivideNode(_nextNodeId++, position),
            "Lerp" => new LerpNode(_nextNodeId++, position),
            "Power" => new PowerNode(_nextNodeId++, position),
            "Max" => new MaxNode(_nextNodeId++, position),
            "Min" => new MinNode(_nextNodeId++, position),
            "Abs" => new AbsNode(_nextNodeId++, position),
            "Floor" => new FloorNode(_nextNodeId++, position),
            "Ceil" => new CeilNode(_nextNodeId++, position),
            "Frac" => new FracNode(_nextNodeId++, position),
            "Sin" => new SinNode(_nextNodeId++, position),
            "Cos" => new CosNode(_nextNodeId++, position),
            "Mask" => new MaskNode(_nextNodeId++, position),
            "Clamp" => new ClampNode(_nextNodeId++, position),
            "Time" => new TimeNode(_nextNodeId++, position),
            "Delta Time" => new DeltaTimeNode(_nextNodeId++, position),
            "Panner" => new PannerNode(_nextNodeId++, position),
            "Hue Shift" => new HueShiftNode(_nextNodeId++, position),
            "Rotator" => new RotatorNode(_nextNodeId++, position),
            "Material Output" => new MaterialOutputNode(_nextNodeId++, position),
            _ => null
        };

        if (node != null)
        {
            _nodes.Add(node);
        }
    }

    private void AddConnection(GraphNode outputNode, int outputIndex, GraphNode inputNode, int inputIndex, NodeValueKind kind)
    {
        if (outputNode == null || inputNode == null)
            return;

        _connections.Add(new NodeConnection(outputNode.Id, outputIndex, inputNode.Id, inputIndex, ResolvePinColor(kind)));
    }

    private void InitializeDemoGraph()
    {
        _nodes.Clear();
        _connections.Clear();
        _nextNodeId = 1;

        var constantColor = new Vector4Node(_nextNodeId++, new Vector2(200f, 200f));
        constantColor.Color = new EngineVec4(0.8f, 0.6f, 0.4f, 1.0f);
        _nodes.Add(constantColor);

        var materialOutput = new MaterialOutputNode(_nextNodeId++, new Vector2(500f, 200f));
        _nodes.Add(materialOutput);

        AddConnection(constantColor, 0, materialOutput, 0, NodeValueKind.Color);
    }

    private static float Mod(float x, float m)
    {
        if (System.MathF.Abs(m) < float.Epsilon)
            return 0f;
        return (x % m + m) % m;
    }

    private enum PinDirection
    {
        Input,
        Output
    }




    private sealed class NodeDrawState
    {
        public NodeDrawState(
            GraphNode node,
            Vector2 min,
            Vector2 max,
            float headerHeight,
            float bodyTop,
            float bodyHeight,
            float pinOffset,
            float columnSplit,
            float pinRowHeight,
            float rowSpacing)
        {
            Node = node;
            Min = min;
            Max = max;
            HeaderHeight = headerHeight;
            BodyTop = bodyTop;
            BodyHeight = bodyHeight;
            PinOffset = pinOffset;
            ColumnSplit = columnSplit;
            PinRowHeight = pinRowHeight;
            RowSpacing = rowSpacing;
        }

        public GraphNode Node { get; }
        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public float HeaderHeight { get; }
        public float BodyTop { get; }
        public float BodyHeight { get; }
        public float PinOffset { get; }
        public float ColumnSplit { get; }
        public float PinRowHeight { get; }
        public float RowSpacing { get; }
        public List<Vector2> InputPinCenters { get; } = new List<Vector2>();
        public List<Vector2> OutputPinCenters { get; } = new List<Vector2>();
    }

    private sealed class GraphDrawCache
    {
        public GraphDrawCache(int capacity, float pinHitRadius)
        {
            NodeStates = new List<NodeDrawState>(capacity);
            PinCenters = new Dictionary<(int, PinDirection, int), Vector2>(capacity * 4);
            PinHitRadius = pinHitRadius;
        }

        public List<NodeDrawState> NodeStates { get; }
        public Dictionary<(int nodeId, PinDirection direction, int index), Vector2> PinCenters { get; }
        public float PinHitRadius { get; }
    }

    private readonly struct MaterialEditorTheme
    {
        public Vector4 CanvasBackground { get; init; }
        public Vector4 CanvasBorder { get; init; }
        public Vector4 GridMinor { get; init; }
        public Vector4 GridMajor { get; init; }
        public Vector4 GridTileLight { get; init; }
        public Vector4 GridTileDark { get; init; }
        public Vector4 NodeShadow { get; init; }
        public Vector4 NodeBodyTop { get; init; }
        public Vector4 NodeBodyBottom { get; init; }
        public Vector4 NodeBorder { get; init; }
        public Vector4 NodeBorderHovered { get; init; }
        public Vector4 NodeBorderSelected { get; init; }
        public Vector4 NodeTitle { get; init; }
        public Vector4 NodeSubtitle { get; init; }
        public Vector4 PinLabelBackground { get; init; }
        public Vector4 PinLabelText { get; init; }
        public Vector4 PinHoverOutline { get; init; }
        public Vector4 PinRowBackground { get; init; }
        public Vector4 PinRowBorder { get; init; }
        public bool OverrideLinkColor { get; init; }
        public Vector4 LinkColor { get; init; }
        public Vector4 LinkGlowColor { get; init; }
        public Vector4 LinkSelectedColor { get; init; }
        public Vector4 LinkSelectedGlowColor { get; init; }

        public static MaterialEditorTheme CreateDefault()
        {
            return new MaterialEditorTheme
            {
                CanvasBackground = new Vector4(0.062f, 0.062f, 0.072f, 1f),
                CanvasBorder = new Vector4(0.18f, 0.18f, 0.2f, 1f),
                GridMinor = new Vector4(0.15f, 0.15f, 0.18f, 0.6f),
                GridMajor = new Vector4(0.26f, 0.26f, 0.3f, 0.85f),
                GridTileLight = new Vector4(0.075f, 0.075f, 0.09f, 1f),
                GridTileDark = new Vector4(0.068f, 0.068f, 0.08f, 1f),
                NodeShadow = new Vector4(0f, 0f, 0f, 0.45f),
                NodeBodyTop = new Vector4(0.18f, 0.18f, 0.24f, 0.96f),
                NodeBodyBottom = new Vector4(0.08f, 0.08f, 0.12f, 0.95f),
                NodeBorder = new Vector4(0.02f, 0.02f, 0.03f, 0.9f),
                NodeBorderHovered = new Vector4(0.4f, 0.55f, 0.85f, 0.9f),
                NodeBorderSelected = new Vector4(0.35f, 0.65f, 1.0f, 1f),
                NodeTitle = new Vector4(0.95f, 0.97f, 1f, 0.98f),
                NodeSubtitle = new Vector4(0.8f, 0.82f, 0.85f, 0.85f),
                PinLabelBackground = new Vector4(0.08f, 0.08f, 0.11f, 0.9f),
                PinLabelText = new Vector4(0.86f, 0.9f, 0.98f, 0.92f),
                PinHoverOutline = new Vector4(0.98f, 0.85f, 0.4f, 0.95f),
                PinRowBackground = new Vector4(0.12f, 0.12f, 0.16f, 0.9f),
                PinRowBorder = new Vector4(0f, 0f, 0f, 0.55f),
                OverrideLinkColor = true,
                LinkColor = new Vector4(0.92f, 0.92f, 0.97f, 1f),
                LinkGlowColor = new Vector4(0.92f, 0.92f, 1f, 0.25f),
                LinkSelectedColor = new Vector4(0.45f, 0.75f, 1.0f, 1f),
                LinkSelectedGlowColor = new Vector4(0.45f, 0.75f, 1.0f, 0.45f)
            };
        }
    }

    private static EngineVec4 LerpColor(EngineVec4 a, EngineVec4 b, float t)
    {
        return a + (b - a) * t;
    }

    private static System.Numerics.Vector4 LerpColor(System.Numerics.Vector4 a, System.Numerics.Vector4 b, float t)
    {
        return a + (b - a) * t;
    }

    private object? EvaluateNodeOutput(GraphNode node, int outputIndex, NodeEvaluationContext context)
    {
        if (context.Cache.TryGetValue((node.Id, outputIndex), out var cached))
        {
            return cached;
        }

        object? result = node.EvaluateOutput(outputIndex, context);

        if (result != null)
        {
            context.Cache[(node.Id, outputIndex)] = result;
        }

        return result;
    }

    private T? GetInputValue<T>(GraphNode node, int inputIndex, NodeEvaluationContext context)
    {
        if (inputIndex < 0 || inputIndex >= node.InputPins.Count)
            return default;

        foreach (var conn in _connections)
        {
            if (conn.InputNodeId == node.Id && conn.InputIndex == inputIndex)
            {
                GraphNode? sourceNode = GetNode(conn.OutputNodeId);
                if (sourceNode != null && conn.OutputIndex < sourceNode.OutputPins.Count)
                {
                    var outputPin = sourceNode.OutputPins[conn.OutputIndex];
                    object? value = EvaluateNodeOutput(sourceNode, conn.OutputIndex, context);
                    
                    if (value != null)
                    {
                        if (outputPin.IsSplitPin && outputPin.ChannelIndex.HasValue)
                        {
                            if (value is T typedValue)
                            {
                                return typedValue;
                            }
                            
                            var componentValue = NodeValueTypeSystem.ExtractComponent(value, outputPin.Kind, outputPin.ChannelIndex.Value);
                            if (componentValue != null)
                            {
                                if (componentValue is T tComponent)
                                    return tComponent;
                                
                                var converted = ConvertValue<T>(componentValue, NodeValueKind.Scalar, InferValueKind<T>());
                                if (converted != null)
                                    return converted;
                            }
                            
                            return default;
                        }
                        
                        if (value is T t)
                        {
                            return t;
                        }
                        
                        if (!outputPin.IsSplitPin)
                        {
                            T? converted = ConvertValue<T>(value, outputPin.Kind, InferValueKind<T>());
                            if (converted != null)
                            {
                                return converted;
                            }
                        }
                    }
                }
            }
        }

        var inputPin = node.InputPins[inputIndex];
        if (inputPin.DefaultValue is T defaultValue)
            return defaultValue;
        
        var convertedDefault = ConvertValue<T>(inputPin.DefaultValue, inputPin.Kind, InferValueKind<T>());
        return convertedDefault;
    }
    
    private object? GetInputValueForChannel(GraphNode node, int inputIndex, NodeEvaluationContext context, int? channelIndex)
    {
        if (inputIndex < 0 || inputIndex >= node.InputPins.Count)
            return null;

        foreach (var conn in _connections)
        {
            if (conn.InputNodeId == node.Id && conn.InputIndex == inputIndex)
            {
                GraphNode? sourceNode = GetNode(conn.OutputNodeId);
                if (sourceNode != null && conn.OutputIndex < sourceNode.OutputPins.Count)
                {
                    var outputPin = sourceNode.OutputPins[conn.OutputIndex];
                    object? value = EvaluateNodeOutput(sourceNode, conn.OutputIndex, context);
                    
                    if (value != null)
                    {
                        if (outputPin.IsSplitPin && outputPin.ChannelIndex.HasValue)
                        {
                            return value;
                        }
                        
                        if (channelIndex.HasValue && value is EngineVec4 color)
                        {
                            return channelIndex.Value switch
                            {
                                0 => color.X,
                                1 => color.Y,
                                2 => color.Z,
                                3 => color.W,
                                _ => value
                            };
                        }
                        
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private static T? ConvertValue<T>(object value, NodeValueKind? fromKind = null, NodeValueKind? toKind = null)
    {
        if (value == null)
            return default;

        NodeValueKind sourceKind = fromKind ?? InferValueKind(value);
        NodeValueKind targetKind = toKind ?? InferValueKind<T>();

        if (sourceKind == targetKind)
        {
            if (value is T t)
                return t;
            return default;
        }

        var converted = NodeValueTypeSystem.ConvertValue(value, sourceKind, targetKind);
        if (converted is T tResult)
            return tResult;

        return default;
    }

    private static object? ConvertValueForType(object value, Type targetType, NodeValueKind? fromKind = null, NodeValueKind? toKind = null)
    {
        if (value == null)
            return null;

        NodeValueKind sourceKind = fromKind ?? InferValueKind(value);
        NodeValueKind targetKind = toKind ?? InferValueKind(targetType);

        if (sourceKind == targetKind)
        {
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;
            return null;
        }

        return NodeValueTypeSystem.ConvertValue(value, sourceKind, targetKind);
    }

    private static NodeValueKind InferValueKind(object value)
    {
        return value switch
        {
            EngineVec4 => NodeValueKind.Color,
            float => NodeValueKind.Scalar,
            EngineVec2 => NodeValueKind.UV,
            _ => NodeValueKind.Color
        };
    }

    private static NodeValueKind InferValueKind<T>()
    {
        return typeof(T) switch
        {
            var t when t == typeof(EngineVec4) => NodeValueKind.Color,
            var t when t == typeof(float) => NodeValueKind.Scalar,
            var t when t == typeof(EngineVec2) => NodeValueKind.UV,
            _ => NodeValueKind.Color
        };
    }

    private static NodeValueKind InferValueKind(Type type)
    {
        if (type == typeof(EngineVec4))
            return NodeValueKind.Color;
        if (type == typeof(float))
            return NodeValueKind.Scalar;
        if (type == typeof(EngineVec2))
            return NodeValueKind.UV;
        return NodeValueKind.Color;
    }

    private TextureSampleNode? FindTextureNodeInChain(int nodeId, int outputIndex)
    {
        return FindTextureNodeInChain(nodeId, outputIndex, new HashSet<(int, int)>());
    }
    
    private TextureSampleNode? FindTextureNodeInChain(int nodeId, int outputIndex, HashSet<(int, int)> visited)
    {
        if (visited.Contains((nodeId, outputIndex)))
            return null;
            
        visited.Add((nodeId, outputIndex));
        
        GraphNode? node = GetNode(nodeId);
        if (node == null)
            return null;
            
        if (node is TextureSampleNode textureNode)
        {
            return textureNode;
        }
        
        if (outputIndex >= 0 && outputIndex < node.OutputPins.Count)
        {
            var pin = node.OutputPins[outputIndex];
            
            if (pin.IsSplitPin && pin.ChannelIndex.HasValue)
            {
                int startIndex = outputIndex;
                while (startIndex > 0 && node.OutputPins[startIndex - 1].IsSplitPin)
                {
                    startIndex--;
                }
                
                if (startIndex < node.OutputPins.Count)
                {
                    foreach (var conn in _connections)
                    {
                        if (conn.OutputNodeId == nodeId && conn.OutputIndex == startIndex)
                        {
                            var result = FindTextureNodeInChain(conn.InputNodeId, conn.InputIndex, visited);
                            if (result != null)
                                return result;
                        }
                    }
                }
            }
        }
        
        foreach (var conn in _connections)
        {
            if (conn.OutputNodeId == nodeId && conn.OutputIndex == outputIndex)
            {
                var result = FindTextureNodeInChain(conn.InputNodeId, conn.InputIndex, visited);
                if (result != null)
                    return result;
            }
        }
        
        return null;
    }

    public MaterialData? EvaluateMaterial()
    {
        var context = new NodeEvaluationContext();
        context.Cache.Clear();
        context.GetInputValueFunc = (nodeId, inputIndex, type) => 
        {
            GraphNode? node = GetNode(nodeId);
            if (node == null) return null;
            
            foreach (var conn in _connections)
            {
                if (conn.InputNodeId == nodeId && conn.InputIndex == inputIndex)
                {
                    GraphNode? sourceNode = GetNode(conn.OutputNodeId);
                    if (sourceNode != null && conn.OutputIndex < sourceNode.OutputPins.Count)
                    {
                        var outputPin = sourceNode.OutputPins[conn.OutputIndex];
                        object? value = EvaluateNodeOutput(sourceNode, conn.OutputIndex, context);
                        
                        if (value != null)
                        {
                            if (outputPin.IsSplitPin && outputPin.ChannelIndex.HasValue)
                            {
                                if (type.IsAssignableFrom(value.GetType()))
                                {
                                    return value;
                                }
                                
                                var componentValue = NodeValueTypeSystem.ExtractComponent(value, outputPin.Kind, outputPin.ChannelIndex.Value);
                                if (componentValue != null)
                                {
                                    if (type.IsAssignableFrom(componentValue.GetType()))
                                        return componentValue;
                                    
                                    var converted = ConvertValueForType(componentValue, type, NodeValueKind.Scalar, InferValueKind(type));
                                    if (converted != null)
                                        return converted;
                                }
                                
                                return null;
                            }
                            
                            if (type.IsAssignableFrom(value.GetType()))
                            {
                                return value;
                            }
                            
                            if (!outputPin.IsSplitPin)
                            {
                                object? converted = ConvertValueForType(value, type, outputPin.Kind, InferValueKind(type));
                                if (converted != null)
                                {
                                    return converted;
                                }
                            }
                        }
                    }
                }
            }
            
            var inputPin = node.InputPins[inputIndex];
            if (inputPin.DefaultValue != null)
            {
                if (type.IsAssignableFrom(inputPin.DefaultValue.GetType()))
                    return inputPin.DefaultValue;
                
                var converted = ConvertValueForType(inputPin.DefaultValue, type, inputPin.Kind, InferValueKind(type));
                if (converted != null)
                    return converted;
            }
            
            return null;
        };

        GraphNode? materialOutput = _nodes.Find(n => n.Title.Contains("Material Output"));
        if (materialOutput == null)
            return null;

        var materialData = new MaterialData();

        if (materialOutput.InputPins.Count > 0)
        {
            var baseConnection = _connections.FirstOrDefault(c => c.InputNodeId == materialOutput.Id && c.InputIndex == 0);
            if (baseConnection != null)
            {
                bool handledByTexture = false;
                TextureSampleNode? textureNode = FindTextureNodeInChain(baseConnection.OutputNodeId, baseConnection.OutputIndex);
                
                if (textureNode != null && !string.IsNullOrEmpty(textureNode.TexturePath))
                {
                    handledByTexture = true;
                    materialData.DiffuseMapPath = textureNode.TexturePath;
                    
                    EngineVec4? multiplier = GetInputValue<EngineVec4>(textureNode, 1, context);
                    if (multiplier.HasValue)
                    {
                        materialData.DiffuseColor = multiplier.Value;
                    }
                    else if (textureNode.InputPins.Count > 1 && textureNode.InputPins[1].DefaultValue is EngineVec4 defaultColor)
                    {
                        materialData.DiffuseColor = defaultColor;
                    }
                    else
                    {
                        materialData.DiffuseColor = new EngineVec4(1f, 1f, 1f, 1f);
                    }
                    
                    var uvConnection = _connections.FirstOrDefault(c => c.InputNodeId == textureNode.Id && c.InputIndex == 0);
                    if (uvConnection != null)
                    {
                        EngineVec2 uvOffset = EngineVec2.Zero;
                        EngineVec2 uvScale = EngineVec2.One;
                        
                        GraphNode? uvNode = GetNode(uvConnection.OutputNodeId);
                        if (uvNode is TexCoordNode texCoordNode)
                        {
                            float? scaleU = GetInputValue<float>(texCoordNode, 2, context);
                            float? scaleV = GetInputValue<float>(texCoordNode, 3, context);
                            float? offsetU = GetInputValue<float>(texCoordNode, 4, context);
                            float? offsetV = GetInputValue<float>(texCoordNode, 5, context);
                            
                            float finalScaleU = scaleU ?? texCoordNode.Scale.X;
                            float finalScaleV = scaleV ?? texCoordNode.Scale.Y;
                            float finalOffsetU = offsetU ?? texCoordNode.Offset.X;
                            float finalOffsetV = offsetV ?? texCoordNode.Offset.Y;
                            
                            if (finalScaleU <= 0f) finalScaleU = 1f;
                            if (finalScaleV <= 0f) finalScaleV = 1f;
                            
                            uvScale = new EngineVec2(finalScaleU, finalScaleV);
                            uvOffset = new EngineVec2(finalOffsetU, finalOffsetV);
                        }
                        else
                        {
                            if (uvNode is PannerNode pannerNode)
                            {
                                float? offsetX = EvaluateNodeOutput(pannerNode, 0, context) as float?;
                                float? offsetY = EvaluateNodeOutput(pannerNode, 1, context) as float?;
                                uvOffset = new EngineVec2(offsetX ?? 0f, offsetY ?? 0f);
                            }
                            else if (uvNode is RotatorNode rotatorNode)
                            {
                                float? offsetX = EvaluateNodeOutput(rotatorNode, 0, context) as float?;
                                float? offsetY = EvaluateNodeOutput(rotatorNode, 1, context) as float?;
                                uvOffset = new EngineVec2(offsetX ?? 0f, offsetY ?? 0f);
                            }
                            else
                            {
                                var currentUVNode = uvNode;
                                while (currentUVNode != null)
                                {
                                    if (currentUVNode is TexCoordNode texCoord)
                                    {
                                        float? scaleU = GetInputValue<float>(texCoord, 2, context);
                                        float? scaleV = GetInputValue<float>(texCoord, 3, context);
                                        float? offsetU = GetInputValue<float>(texCoord, 4, context);
                                        float? offsetV = GetInputValue<float>(texCoord, 5, context);
                                        
                                        float finalScaleU = scaleU ?? texCoord.Scale.X;
                                        float finalScaleV = scaleV ?? texCoord.Scale.Y;
                                        float finalOffsetU = offsetU ?? texCoord.Offset.X;
                                        float finalOffsetV = offsetV ?? texCoord.Offset.Y;
                                        
                                        if (finalScaleU <= 0f) finalScaleU = 1f;
                                        if (finalScaleV <= 0f) finalScaleV = 1f;
                                        
                                        uvScale = new EngineVec2(finalScaleU, finalScaleV);
                                        uvOffset = new EngineVec2(finalOffsetU, finalOffsetV);
                                        break;
                                    }
                                    
                                    var inputConnection = _connections.FirstOrDefault(c => c.InputNodeId == currentUVNode.Id && c.InputIndex == 0);
                                    if (inputConnection != null)
                                    {
                                        currentUVNode = GetNode(inputConnection.OutputNodeId);
                                        
                                        if (currentUVNode is PannerNode panner)
                                        {
                                            float? offsetX = EvaluateNodeOutput(panner, 0, context) as float?;
                                            float? offsetY = EvaluateNodeOutput(panner, 1, context) as float?;
                                            uvOffset = new EngineVec2(offsetX ?? 0f, offsetY ?? 0f);
                                            break;
                                        }
                                        else if (currentUVNode is RotatorNode rotator)
                                        {
                                            float? offsetX = EvaluateNodeOutput(rotator, 0, context) as float?;
                                            float? offsetY = EvaluateNodeOutput(rotator, 1, context) as float?;
                                            uvOffset = new EngineVec2(offsetX ?? 0f, offsetY ?? 0f);
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        
                        materialData.UVScale = uvScale;
                        materialData.UVOffset = uvOffset;
                    }
                    else
                    {
                        materialData.UVScale = EngineVec2.One;
                        materialData.UVOffset = EngineVec2.Zero;
                    }
                }
                
                if (!handledByTexture)
                {
                    EngineVec4? baseColor = GetInputValue<EngineVec4>(materialOutput, 0, context);
                    if (baseColor.HasValue)
                    {
                        materialData.DiffuseColor = baseColor.Value;
                    }
                }
            }
            else
            {
                EngineVec4? baseColor = GetInputValue<EngineVec4>(materialOutput, 0, context);
                if (baseColor.HasValue)
                {
                    materialData.DiffuseColor = baseColor.Value;
                }
            }
        }

        if (materialOutput.InputPins.Count > 1)
        {
            float? metallic = GetInputValue<float>(materialOutput, 1, context);
            if (metallic.HasValue)
            {
                materialData.Metallic = System.Math.Clamp(metallic.Value, 0f, 1f);
            }
        }

        if (materialOutput.InputPins.Count > 2)
        {
            float? specular = GetInputValue<float>(materialOutput, 2, context);
            if (specular.HasValue)
            {
                materialData.Specular = System.Math.Clamp(specular.Value, 0f, 1f);
            }
        }

        if (materialOutput.InputPins.Count > 3)
        {
            float? roughness = GetInputValue<float>(materialOutput, 3, context);
            if (roughness.HasValue)
            {
                materialData.Roughness = System.Math.Clamp(roughness.Value, 0f, 1f);
            }
        }

        return materialData;
    }
}
