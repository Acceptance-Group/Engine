using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using EngineVec2 = Engine.Math.Vector2;
using EngineVec4 = Engine.Math.Vector4;

namespace Engine.Editor.Graphs;

public sealed record NodeConnection(int OutputNodeId, int OutputIndex, int InputNodeId, int InputIndex, System.Numerics.Vector4 Color);

public abstract class GraphNode
{
    public int Id { get; }
    public string Title { get; protected set; }
    public string? Subtitle { get; protected set; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
    public Vector4 HeaderColor { get; protected set; }
    public List<NodePin> InputPins { get; }
    public List<NodePin> OutputPins { get; }

    protected GraphNode(int id, string title, string? subtitle, Vector2 position, Vector2 size, Vector4 headerColor, IEnumerable<(string label, NodeValueKind kind)> inputs, IEnumerable<(string label, NodeValueKind kind, bool isSplitPin, int? channelIndex)> outputs)
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
            foreach (var (label, kind, isSplitPin, channelIndex) in outputs)
            {
                OutputPins.Add(new NodePin(label, kind, isSplitPin, channelIndex));
            }
        }
    }

    protected GraphNode(int id, string title, string? subtitle, Vector2 position, Vector2 size, Vector4 headerColor, IEnumerable<(string label, NodeValueKind kind)> inputs, IEnumerable<(string label, NodeValueKind kind)> outputs)
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

    public abstract object? EvaluateOutput(int outputIndex, NodeEvaluationContext context);
    
    public virtual void DrawInspector(List<NodeConnection> connections)
    {
        if (InputPins.Count == 0)
        {
            DrawInspector();
            return;
        }

        for (int i = 0; i < InputPins.Count; i++)
        {
            var pin = InputPins[i];
            bool hasConnection = connections.Any(c => c.InputNodeId == Id && c.InputIndex == i);
            
            if (hasConnection)
            {
                ImGui.BeginDisabled();
                DrawPinValue(pin, i);
                ImGui.EndDisabled();
            }
            else
            {
                DrawPinValue(pin, i);
            }
        }
        
        DrawInspector();
    }
    
    protected virtual void DrawPinValue(NodePin pin, int pinIndex)
    {
        var typeDef = NodeValueTypeSystem.GetDefinition(pin.Kind);
        if (typeDef == null)
            return;

        ImGui.Text(pin.Label);
        
        switch (pin.Kind)
        {
            case NodeValueKind.Scalar:
                float scalarValue = pin.DefaultValue is float f ? f : 0.5f;
                if (ImGui.DragFloat($"##{Id}_{pinIndex}", ref scalarValue, 0.01f))
                {
                    pin.DefaultValue = scalarValue;
                }
                break;
                
            case NodeValueKind.Color:
                EngineVec4 colorValue = pin.DefaultValue is EngineVec4 colorVal ? colorVal : new EngineVec4(0.5f, 0.5f, 0.5f, 1f);
                System.Numerics.Vector4 colorVec = new System.Numerics.Vector4(colorValue.X, colorValue.Y, colorValue.Z, colorValue.W);
                if (ImGui.ColorEdit4($"##{Id}_{pinIndex}", ref colorVec, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
                {
                    pin.DefaultValue = new EngineVec4(colorVec.X, colorVec.Y, colorVec.Z, colorVec.W);
                }
                break;
                
            case NodeValueKind.UV:
                EngineVec2 uvValue = pin.DefaultValue is EngineVec2 uv ? uv : new EngineVec2(0f, 0f);
                float u = uvValue.X;
                float v = uvValue.Y;
                if (ImGui.DragFloat($"U##{Id}_{pinIndex}", ref u, 0.01f))
                {
                    pin.DefaultValue = new EngineVec2(u, v);
                }
                if (ImGui.DragFloat($"V##{Id}_{pinIndex}", ref v, 0.01f))
                {
                    pin.DefaultValue = new EngineVec2(u, v);
                }
                break;
                
            case NodeValueKind.Vector:
            case NodeValueKind.Normal:
            case NodeValueKind.Emission:
                EngineVec4 vectorValue = pin.DefaultValue is EngineVec4 vec ? vec : new EngineVec4(0f, 0f, 0f, 0f);
                float x = vectorValue.X;
                float y = vectorValue.Y;
                float z = vectorValue.Z;
                float w = vectorValue.W;
                if (ImGui.DragFloat($"X##{Id}_{pinIndex}", ref x, 0.01f))
                {
                    pin.DefaultValue = new EngineVec4(x, y, z, w);
                }
                if (ImGui.DragFloat($"Y##{Id}_{pinIndex}", ref y, 0.01f))
                {
                    pin.DefaultValue = new EngineVec4(x, y, z, w);
                }
                if (ImGui.DragFloat($"Z##{Id}_{pinIndex}", ref z, 0.01f))
                {
                    pin.DefaultValue = new EngineVec4(x, y, z, w);
                }
                if (pin.Kind != NodeValueKind.Normal && ImGui.DragFloat($"W##{Id}_{pinIndex}", ref w, 0.01f))
                {
                    pin.DefaultValue = new EngineVec4(x, y, z, w);
                }
                break;
        }
        
        ImGui.Separator();
    }
    
    public abstract void DrawInspector();
}

public class NodePin
{
    public NodePin(string label, NodeValueKind kind, bool isSplitPin = false, int? channelIndex = null)
    {
        Label = label;
        Kind = kind;
        IsSplitPin = isSplitPin;
        ChannelIndex = channelIndex;
        DefaultValue = NodeValueTypeSystem.GetDefaultValue(kind);
    }

    public string Label { get; set; }
    public NodeValueKind Kind { get; set; }
    public bool IsSplitPin { get; set; }
    public int? ChannelIndex { get; set; }
    public object DefaultValue { get; set; }
}

public class NodeEvaluationContext
{
    public Dictionary<(int nodeId, int outputIndex), object> Cache { get; } = new Dictionary<(int, int), object>();
    public Func<int, int, Type, object?> GetInputValueFunc { get; set; } = null!;
    
    public T? GetInputValue<T>(int nodeId, int inputIndex)
    {
        var result = GetInputValueFunc(nodeId, inputIndex, typeof(T));
        if (result is T t)
            return t;
        return default;
    }
}

