using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace Engine.Editor;

public class MaterialEditorWindow
{
    private readonly EditorApplication _editor;
    private readonly MaterialEditorTheme _theme = MaterialEditorTheme.CreateDefault();
    private readonly List<GraphNode> _nodes = new List<GraphNode>();
    private readonly List<NodeConnection> _connections = new List<NodeConnection>();
    private readonly Dictionary<string, string[]> _nodePalette = new Dictionary<string, string[]>
    {
        { "Inputs", new[] { "Texture Sample", "Constant3Vector", "Constant4Vector", "Scalar Parameter" } },
        { "Math", new[] { "Multiply", "Add", "Lerp", "Power" } },
        { "Utility", new[] { "Mask", "Clamp", "Time", "Panner" } },
        { "Material", new[] { "Material Attributes", "Material Output" } }
    };
    private static readonly Vector4 NodeColorTexCoord = new Vector4(0.74f, 0.18f, 0.12f, 1f);
    private static readonly Vector4 NodeColorTexture = new Vector4(0.1f, 0.42f, 0.66f, 1f);
    private static readonly Vector4 NodeColorMultiply = new Vector4(0.24f, 0.48f, 0.23f, 1f);
    private static readonly Vector4 NodeColorLerp = new Vector4(0.18f, 0.35f, 0.45f, 1f);
    private static readonly Vector4 NodeColorUtility = new Vector4(0.27f, 0.37f, 0.6f, 1f);
    private static readonly Vector4 NodeColorOutput = new Vector4(0.18f, 0.18f, 0.18f, 1f);

    private Vector2 _canvasOffset = new Vector2(120f, 120f);
    private float _canvasZoom = 1.0f;
    private int _nextNodeId = 1;
    private int? _selectedNodeId;
    private int? _draggingNodeId;
    private Vector2 _draggingNodeOffset;
    private (int nodeId, PinDirection direction, int pinIndex)? _hoveredPin;
    private (int nodeId, PinDirection direction, int pinIndex)? _activeLink;
    private Vector2 _activeLinkMousePos;
    private int? _selectedConnectionIndex;
    private int? _hoveredConnectionIndex;
    private int? _contextConnectionIndex;

    public bool Visible { get; set; }

    public MaterialEditorWindow(EditorApplication editor)
    {
        _editor = editor;
        InitializeDemoGraph();
    }

    public void Render()
    {
        if (!Visible)
            return;

        bool visible = Visible;
        ImGui.SetNextWindowSize(new Vector2(1100f, 640f), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Material Editor", ref visible, ImGuiWindowFlags.NoCollapse))
        {
            Visible = visible;
            DrawToolbar();
            DrawBody();
        }

        ImGui.End();
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

        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.85f, 1f), "Node-based authoring preview");

        ImGui.Separator();
    }

    private void DrawBody()
    {
        float sidebarWidth = 260f;

        ImGui.BeginChild("MaterialSidebar", new Vector2(sidebarWidth, 0f), ImGuiChildFlags.Border, ImGuiWindowFlags.None);
        DrawSidebar();
        ImGui.EndChild();

            ImGui.SameLine();

        ImGui.BeginChild("MaterialGraphArea", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawGraphArea();
        ImGui.EndChild();
    }

    private void DrawSidebar()
    {
        ImGui.Text("Project");
            ImGui.Separator();

        string? projectPath = _editor.ProjectManager.CurrentProjectPath;
        if (string.IsNullOrEmpty(projectPath))
        {
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No project loaded");
        }
        else
        {
            string projectName = Path.GetFileName(projectPath);
            ImGui.Text(projectName);
            ImGui.TextDisabled(projectPath);
        }

        ImGui.Spacing();
        ImGui.Text("Node Palette");
            ImGui.Separator();

        foreach (var entry in _nodePalette)
        {
            if (ImGui.TreeNode(entry.Key))
            {
                foreach (var label in entry.Value)
                {
                    ImGui.BulletText(label);
                }
                ImGui.TreePop();
            }
        }

        ImGui.Spacing();
        ImGui.Text("Preview");
        ImGui.Separator();
        Vector2 previewSize = new Vector2(220f, 160f);
        Vector2 cursor = ImGui.GetCursorScreenPos();
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(cursor, cursor + previewSize, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.16f, 1f)), 6f);
        drawList.AddRect(cursor, cursor + previewSize, ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.35f, 1f)), 6f);
        drawList.AddText(cursor + new Vector2(10f, 10f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), "Material Preview");
        drawList.AddCircleFilled(cursor + new Vector2(110f, 90f), 60f, ImGui.GetColorU32(new Vector4(0.93f, 0.62f, 0.25f, 0.9f)));
        drawList.AddCircleFilled(cursor + new Vector2(130f, 70f), 30f, ImGui.GetColorU32(new Vector4(1f, 0.92f, 0.74f, 0.8f)));
        ImGui.Dummy(previewSize);

        ImGui.Spacing();
        ImGui.Text("Status");
        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.6f, 0.85f, 0.6f, 1f), "Visual prototype mode");
        ImGui.TextWrapped("Interact with the graph canvas to pan/zoom. Node logic will be implemented later.");
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

        GraphDrawCache cache = BuildGraphDrawCache(canvasPos);
        bool changed = false;
        changed |= HandlePinInteractions(cache);
        changed |= HandleNodeDragging(cache, canvasPos);
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
        float tileSize = 64f * _canvasZoom;
        if (tileSize < 24f) tileSize = 24f;

        float tileOffsetX = 0f;
        float tileOffsetY = 0f;

        for (float x = 0f; x < canvasSize.X; x += tileSize)
        {
            for (float y = 0f; y < canvasSize.Y; y += tileSize)
            {
                int tileX = (int)System.MathF.Floor((x + tileOffsetX) / tileSize);
                int tileY = (int)System.MathF.Floor((y + tileOffsetY) / tileSize);
                bool dark = ((tileX + tileY) & 1) == 0;
                uint color = ImGui.GetColorU32(dark ? _theme.GridTileDark : _theme.GridTileLight);
                Vector2 min = canvasPos + new Vector2(x, y);
                Vector2 max = min + new Vector2(tileSize, tileSize);
                drawList.AddRectFilled(min, max, color);
            }
        }

        float majorStep = tileSize;
        float minorStep = tileSize * 0.25f;
        if (minorStep < 8f) minorStep = 8f;

        uint minorColor = ImGui.GetColorU32(_theme.GridMinor);
        uint majorColor = ImGui.GetColorU32(_theme.GridMajor);

        float offsetX = 0f;
        float offsetY = 0f;

        for (float x = 0f; x < canvasSize.X; x += minorStep)
        {
            uint color = System.MathF.Abs(((x + offsetX) % majorStep)) < 0.1f ? majorColor : minorColor;
            drawList.AddLine(new Vector2(canvasPos.X + x, canvasPos.Y), new Vector2(canvasPos.X + x, canvasPos.Y + canvasSize.Y), color);
        }

        for (float y = 0f; y < canvasSize.Y; y += minorStep)
        {
            uint color = System.MathF.Abs(((y + offsetY) % majorStep)) < 0.1f ? majorColor : minorColor;
            drawList.AddLine(new Vector2(canvasPos.X, canvasPos.Y + y), new Vector2(canvasPos.X + canvasSize.X, canvasPos.Y + y), color);
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
            float headerNeeded = 12f * _canvasZoom + titleSize.Y + (subtitleSize.Y > 0 ? 4f * _canvasZoom + subtitleSize.Y : 0f);
            float headerHeight = System.MathF.Max(30f * _canvasZoom, headerNeeded);
            float bodyTop = nodeMin.Y + headerHeight;
            float bodyBottom = nodeMax.Y - 8f * _canvasZoom;
            float bodyHeight = System.MathF.Max(bodyBottom - bodyTop, 12f * _canvasZoom);

            float pinOffset = 8f * _canvasZoom;
            float pinRowHeight = 24f * _canvasZoom;
            float pinRowSpacing = 3f * _canvasZoom;
            float pinTopPadding = 2f * _canvasZoom;
            float pinBottomPadding = 4f * _canvasZoom;
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
                _activeLink = null;
            }
        }
        else if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _activeLink = null;
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
            Vector4 bodyTopColor = LerpColor(_theme.NodeBodyTop, headerColor, 0.35f);
            Vector4 bodyBottomColor = LerpColor(_theme.NodeBodyBottom, headerColor, 0.15f);

            Vector2 bodyMin = new Vector2(min.X, state.BodyTop);
            drawList.AddRectFilled(bodyMin, max, ImGui.GetColorU32(bodyBottomColor), rounding, ImDrawFlags.RoundCornersBottom);

            drawList.AddRectFilled(min, new Vector2(max.X, min.Y + state.HeaderHeight), ImGui.GetColorU32(headerColor), rounding, ImDrawFlags.RoundCornersTop);

            bool isSelected = _selectedNodeId == state.Node.Id;
            bool hovered = ContainsPoint(min, max, io.MousePos);
            uint borderColor = ImGui.GetColorU32(isSelected ? _theme.NodeBorderSelected : hovered ? _theme.NodeBorderHovered : _theme.NodeBorder);
            float borderThickness = isSelected ? 2.4f * _canvasZoom : 1.2f * _canvasZoom;
            drawList.AddRect(min, max, borderColor, rounding, ImDrawFlags.None, borderThickness);

            Vector2 titleSizeLocal = ImGui.CalcTextSize(state.Node.Title ?? string.Empty);
            Vector2 titlePos = min + new Vector2(14f * _canvasZoom, 6f * _canvasZoom);
            drawList.AddText(titlePos, ImGui.GetColorU32(_theme.NodeTitle), state.Node.Title);

            if (!string.IsNullOrEmpty(state.Node.Subtitle))
            {
                Vector2 subtitlePos = new Vector2(titlePos.X, titlePos.Y + titleSizeLocal.Y + 4f * _canvasZoom);
                drawList.AddText(subtitlePos, ImGui.GetColorU32(_theme.NodeSubtitle), state.Node.Subtitle);
            }

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

            Vector2 textSize = ImGui.CalcTextSize(state.Node.InputPins[i].Label);
            Vector2 textPos = new Vector2(rowMin.X + 8f * _canvasZoom, center.Y - textSize.Y * 0.5f);
            drawList.AddText(textPos, ImGui.GetColorU32(_theme.PinLabelText), state.Node.InputPins[i].Label);
        }

        for (int i = 0; i < state.Node.OutputPins.Count; i++)
        {
            Vector2 center = state.OutputPinCenters[i];
            NodeValueKind kind = state.Node.OutputPins[i].Kind;
            bool hovered = _hoveredPin.HasValue && _hoveredPin.Value == (state.Node.Id, PinDirection.Output, i);
            float radius = hovered ? pinRadius * 1.25f : pinRadius;
            uint color = ImGui.GetColorU32(ResolvePinColor(kind));

            Vector2 rowMin = new Vector2(state.ColumnSplit + rightPadding, center.Y - rowHalf);
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

            Vector2 labelSize = ImGui.CalcTextSize(state.Node.OutputPins[i].Label);
            Vector2 textPos = new Vector2(rowMax.X - labelSize.X - 8f * _canvasZoom, center.Y - labelSize.Y * 0.5f);
            drawList.AddText(textPos, ImGui.GetColorU32(_theme.PinLabelText), state.Node.OutputPins[i].Label);
        }
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

            bool isSelected = _selectedConnectionIndex.HasValue && _selectedConnectionIndex.Value == i;
            Vector4 baseThemeColor = isSelected && _theme.OverrideLinkColor ? _theme.LinkSelectedColor : _theme.LinkColor;
            Vector4 connectionColor = _theme.OverrideLinkColor ? baseThemeColor : connection.Color;
            Vector4 glowThemeColor = isSelected && _theme.OverrideLinkColor ? _theme.LinkSelectedGlowColor : _theme.LinkGlowColor;
            Vector4 glowColorVec = _theme.OverrideLinkColor ? glowThemeColor : new Vector4(connectionColor.X, connectionColor.Y, connectionColor.Z, connectionColor.W * 0.35f);
            uint glowColor = ImGui.GetColorU32(glowColorVec);
            uint baseColor = ImGui.GetColorU32(connectionColor);

            drawList.AddBezierCubic(start, control1, control2, end, glowColor, 5.5f * _canvasZoom);
            drawList.AddBezierCubic(start, control1, control2, end, baseColor, 3f * _canvasZoom);
            drawList.AddCircleFilled(end, 4f * _canvasZoom, baseColor);
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

        Vector4 lineColor = _theme.OverrideLinkColor ? _theme.LinkSelectedColor : color;
        Vector4 glowColorVec = _theme.OverrideLinkColor ? _theme.LinkSelectedGlowColor : new Vector4(lineColor.X, lineColor.Y, lineColor.Z, lineColor.W * 0.35f);

        drawList.AddBezierCubic(start, control1, control2, end, ImGui.GetColorU32(glowColorVec), 5f * _canvasZoom);
        drawList.AddBezierCubic(start, control1, control2, end, ImGui.GetColorU32(lineColor), 2.8f * _canvasZoom);
    }

    private void UpdateConnectionHover(Dictionary<(int nodeId, PinDirection direction, int index), Vector2> pinCenters)
    {
        _hoveredConnectionIndex = null;
        var io = ImGui.GetIO();
        Vector2 mouse = io.MousePos;
        float bestDist = 30f * _canvasZoom;

        for (int i = 0; i < _connections.Count; i++)
        {
            var connection = _connections[i];
            if (!pinCenters.TryGetValue((connection.OutputNodeId, PinDirection.Output, connection.OutputIndex), out var start))
                continue;
            if (!pinCenters.TryGetValue((connection.InputNodeId, PinDirection.Input, connection.InputIndex), out var end))
                continue;

            float distance = EstimateDistanceToConnection(mouse, start, end);
            if (distance < bestDist)
            {
                bestDist = distance;
                _hoveredConnectionIndex = i;
            }
        }

        if (_hoveredConnectionIndex.HasValue)
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _selectedConnectionIndex = _hoveredConnectionIndex;
            }
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                _contextConnectionIndex = _hoveredConnectionIndex;
                ImGui.OpenPopup("ConnectionContext");
            }
        }

        if (ImGui.BeginPopup("ConnectionContext"))
        {
            if (ImGui.MenuItem("Delete") && _contextConnectionIndex.HasValue && _contextConnectionIndex.Value >= 0 && _contextConnectionIndex.Value < _connections.Count)
            {
                _connections.RemoveAt(_contextConnectionIndex.Value);
                if (_selectedConnectionIndex.HasValue && _selectedConnectionIndex.Value == _contextConnectionIndex.Value)
                {
                    _selectedConnectionIndex = null;
                }
                _contextConnectionIndex = null;
            }
            ImGui.EndPopup();
        }
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

        _connections.RemoveAll(c => c.InputNodeId == inputRef.nodeId && c.InputIndex == inputRef.pinIndex);

        Vector4 color = ResolvePinColor(outputNode.OutputPins[outputRef.pinIndex].Kind);
        _connections.Add(new NodeConnection(outputRef.nodeId, outputRef.pinIndex, inputRef.nodeId, inputRef.pinIndex, color));
        return true;
    }

    private static float EstimateDistanceToConnection(Vector2 point, Vector2 start, Vector2 end)
    {
        float distance = DistancePointToSegment(point, start, end);
        return distance;
    }

    private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float abLenSq = ab.X * ab.X + ab.Y * ab.Y;
        if (abLenSq <= float.Epsilon)
            return Vector2.Distance(p, a);

        float t = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / abLenSq;
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        Vector2 closest = new Vector2(a.X + ab.X * t, a.Y + ab.Y * t);
        return Vector2.Distance(p, closest);
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

    private Vector4 ResolvePinColor(NodeValueKind kind)
    {
        return kind switch
        {
            NodeValueKind.Color => new Vector4(0.94f, 0.54f, 0.2f, 1f),
            NodeValueKind.Scalar => new Vector4(0.95f, 0.82f, 0.32f, 1f),
            NodeValueKind.UV => new Vector4(0.35f, 0.76f, 0.93f, 1f),
            NodeValueKind.Normal => new Vector4(0.4f, 0.5f, 0.95f, 1f),
            NodeValueKind.Vector => new Vector4(0.55f, 0.9f, 0.68f, 1f),
            NodeValueKind.Emission => new Vector4(0.95f, 0.42f, 0.72f, 1f),
            _ => new Vector4(0.8f, 0.8f, 0.8f, 1f)
        };
    }

    private GraphNode CreateNode(string title, string? subtitle, Vector2 position, Vector2 size, Vector4 headerColor, IEnumerable<(string, NodeValueKind)> inputs, IEnumerable<(string, NodeValueKind)> outputs)
    {
        var node = new GraphNode(_nextNodeId++, title, subtitle, position, size, headerColor, inputs, outputs);
        _nodes.Add(node);
        return node;
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

        var texCoord = CreateNode(
            "TexCoord[0]",
            "UV Source",
            new Vector2(40f, 280f),
            new Vector2(160f, 110f),
            NodeColorTexCoord,
            inputs: Array.Empty<(string, NodeValueKind)>(),
            outputs: new[] { ("UV", NodeValueKind.UV) });

        var macroVariation = CreateNode(
            "Macro Texture Variation",
            "Output Blend",
            new Vector2(230f, 20f),
            new Vector2(240f, 180f),
            NodeColorMultiply,
            inputs: new[] { ("Input A", NodeValueKind.Color), ("Input B", NodeValueKind.Color) },
            outputs: new[] { ("Output 1", NodeValueKind.Color), ("Output 2", NodeValueKind.Color), ("Output 3", NodeValueKind.Color) });

        var woodTexture = CreateNode(
            "Texture Sample",
            "Walnut_BaseColor",
            new Vector2(260f, 200f),
            new Vector2(230f, 190f),
            NodeColorTexture,
            inputs: new[] { ("UVs", NodeValueKind.UV) },
            outputs: new[] { ("RGB", NodeValueKind.Color), ("Alpha", NodeValueKind.Scalar) });

        var roughnessTexture = CreateNode(
            "Texture Sample",
            "Walnut_Roughness",
            new Vector2(260f, 400f),
            new Vector2(230f, 190f),
            NodeColorTexture,
            inputs: new[] { ("UVs", NodeValueKind.UV) },
            outputs: new[] { ("RGB", NodeValueKind.Color), ("Alpha", NodeValueKind.Scalar) });

        var normalTexture = CreateNode(
            "Texture Sample",
            "Walnut_Normal",
            new Vector2(260f, 600f),
            new Vector2(230f, 190f),
            NodeColorTexture,
            inputs: new[] { ("UVs", NodeValueKind.UV) },
            outputs: new[] { ("RGB", NodeValueKind.Vector) });

        var multiplyMacro = CreateNode(
            "Multiply",
            "Macro Blend",
            new Vector2(520f, 80f),
            new Vector2(220f, 150f),
            NodeColorMultiply,
            inputs: new[] { ("A", NodeValueKind.Color), ("B", NodeValueKind.Color) },
            outputs: new[] { ("Result", NodeValueKind.Color) });

        var lerpHigh = CreateNode(
            "Lerp (0.8,1.2)",
            "Surface Variation",
            new Vector2(760f, 60f),
            new Vector2(240f, 170f),
            NodeColorLerp,
            inputs: new[] { ("A", NodeValueKind.Color), ("B", NodeValueKind.Color), ("Alpha", NodeValueKind.Scalar) },
            outputs: new[] { ("Result", NodeValueKind.Color) });

        var multiplySurface = CreateNode(
            "Multiply",
            "Surface Mix",
            new Vector2(1020f, 120f),
            new Vector2(220f, 160f),
            NodeColorMultiply,
            inputs: new[] { ("A", NodeValueKind.Color), ("B", NodeValueKind.Color) },
            outputs: new[] { ("Result", NodeValueKind.Color) });

        var alphaOffset = CreateNode(
            "AlphaOffset",
            "Adjust Roughness",
            new Vector2(560f, 360f),
            new Vector2(220f, 150f),
            NodeColorUtility,
            inputs: new[] { ("Alpha", NodeValueKind.Scalar), ("Offset", NodeValueKind.Scalar) },
            outputs: new[] { ("Result", NodeValueKind.Scalar) });

        var lerpRough = CreateNode(
            "Lerp (0.2)",
            "Roughness Mix",
            new Vector2(810f, 340f),
            new Vector2(230f, 150f),
            NodeColorLerp,
            inputs: new[] { ("A", NodeValueKind.Scalar), ("B", NodeValueKind.Scalar), ("Alpha", NodeValueKind.Scalar) },
            outputs: new[] { ("Result", NodeValueKind.Scalar) });

        var multiplyNormal = CreateNode(
            "Multiply (×2)",
            "Normal Boost",
            new Vector2(640f, 580f),
            new Vector2(220f, 160f),
            NodeColorMultiply,
            inputs: new[] { ("A", NodeValueKind.Vector), ("B", NodeValueKind.Vector) },
            outputs: new[] { ("Result", NodeValueKind.Vector) });

        var materialOutput = CreateNode(
            "M_Wood_Floor_Walnut_Worn",
            "Material Output",
            new Vector2(1160f, 220f),
            new Vector2(260f, 320f),
            NodeColorOutput,
            inputs: new[]
            {
                ("Base Color", NodeValueKind.Color),
                ("Metallic", NodeValueKind.Scalar),
                ("Specular", NodeValueKind.Scalar),
                ("Roughness", NodeValueKind.Scalar),
                ("Normal", NodeValueKind.Normal),
                ("Ambient Occlusion", NodeValueKind.Scalar)
            },
            outputs: Array.Empty<(string, NodeValueKind)>());

        AddConnection(texCoord, 0, woodTexture, 0, NodeValueKind.UV);
        AddConnection(texCoord, 0, roughnessTexture, 0, NodeValueKind.UV);
        AddConnection(texCoord, 0, normalTexture, 0, NodeValueKind.UV);

        AddConnection(woodTexture, 0, multiplyMacro, 0, NodeValueKind.Color);
        AddConnection(macroVariation, 0, multiplyMacro, 1, NodeValueKind.Color);

        AddConnection(multiplyMacro, 0, lerpHigh, 0, NodeValueKind.Color);
        AddConnection(macroVariation, 1, lerpHigh, 1, NodeValueKind.Color);
        AddConnection(macroVariation, 2, lerpHigh, 2, NodeValueKind.Scalar);

        AddConnection(lerpHigh, 0, multiplySurface, 0, NodeValueKind.Color);
        AddConnection(macroVariation, 0, multiplySurface, 1, NodeValueKind.Color);

        AddConnection(multiplySurface, 0, materialOutput, 0, NodeValueKind.Color);

        AddConnection(roughnessTexture, 1, alphaOffset, 0, NodeValueKind.Scalar);
        AddConnection(macroVariation, 1, alphaOffset, 1, NodeValueKind.Scalar);
        AddConnection(alphaOffset, 0, lerpRough, 0, NodeValueKind.Scalar);
        AddConnection(macroVariation, 2, lerpRough, 1, NodeValueKind.Scalar);
        AddConnection(roughnessTexture, 0, lerpRough, 2, NodeValueKind.Scalar);
        AddConnection(lerpRough, 0, materialOutput, 3, NodeValueKind.Scalar);

        AddConnection(normalTexture, 0, multiplyNormal, 0, NodeValueKind.Vector);
        AddConnection(macroVariation, 0, multiplyNormal, 1, NodeValueKind.Vector);
        AddConnection(multiplyNormal, 0, materialOutput, 4, NodeValueKind.Normal);
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

    private enum NodeValueKind
    {
        Color,
        Scalar,
        UV,
        Normal,
        Vector,
        Emission
    }

    private sealed class GraphNode
    {
        public GraphNode(int id, string title, string? subtitle, Vector2 position, Vector2 size, Vector4 headerColor, IEnumerable<(string label, NodeValueKind kind)> inputs, IEnumerable<(string label, NodeValueKind kind)> outputs)
        {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            Position = position;
            Size = size;
            HeaderColor = headerColor;
            InputPins = new List<NodePin>();
            OutputPins = new List<NodePin>();

            if (inputs != null)
            {
                foreach (var (label, kind) in inputs)
                {
                    InputPins.Add(new NodePin(label, kind));
                }
            }

            if (outputs != null)
            {
                foreach (var (label, kind) in outputs)
                {
                    OutputPins.Add(new NodePin(label, kind));
                }
            }
        }

        public int Id { get; }
        public string Title { get; }
        public string? Subtitle { get; }
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public Vector4 HeaderColor { get; }
        public List<NodePin> InputPins { get; }
        public List<NodePin> OutputPins { get; }
    }

    private sealed class NodePin
    {
        public NodePin(string label, NodeValueKind kind)
        {
            Label = label;
            Kind = kind;
        }

        public string Label { get; }
        public NodeValueKind Kind { get; }
    }

    private sealed record NodeConnection(int OutputNodeId, int OutputIndex, int InputNodeId, int InputIndex, Vector4 Color);

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

    private static Vector4 LerpColor(Vector4 a, Vector4 b, float t)
    {
        return a + (b - a) * t;
    }
}