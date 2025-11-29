using System.Numerics;
using EngineVec4 = Engine.Math.Vector4;
using ImGuiNET;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace Engine.Editor.Graphs.Nodes;

public class Vector3Node : GraphNode
{
    public EngineVec4 Color { get; set; } = EngineVec4.One;

    public Vector3Node(int id, NVector2 position) 
        : base(id, "Vector 3", null, position, new NVector2(210f, 140f), new NVector4(0.94f, 0.54f, 0.2f, 1f),
            inputs: Array.Empty<(string, NodeValueKind)>(),
            outputs: new[] { ("Color", NodeValueKind.Color) })
    {
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;
            
        var pin = OutputPins[outputIndex];
        
        if (pin.IsSplitPin && pin.ChannelIndex.HasValue)
        {
            return pin.ChannelIndex.Value switch
            {
                0 => Color.X,
                1 => Color.Y,
                2 => Color.Z,
                _ => null
            };
        }
        
            return new EngineVec4(Color.X, Color.Y, Color.Z, 1.0f);
    }

    public override void DrawInspector()
    {
        ImGui.Text("Color");
        ImGui.Separator();
        
        Vector3 colorVec = new Vector3(Color.X, Color.Y, Color.Z);
        if (ImGui.ColorEdit3("RGB", ref colorVec, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
        {
            Color = new EngineVec4(colorVec.X, colorVec.Y, colorVec.Z, 1.0f);
        }

        ImGui.Spacing();
        ImGui.Text("Components:");
        float r = Color.X;
        float g = Color.Y;
        float b = Color.Z;

        if (ImGui.DragFloat("R", ref r, 0.01f, 0f, 1f))
        {
            Color = new EngineVec4(r, g, b, 1.0f);
        }
        if (ImGui.DragFloat("G", ref g, 0.01f, 0f, 1f))
        {
            Color = new EngineVec4(r, g, b, 1.0f);
        }
        if (ImGui.DragFloat("B", ref b, 0.01f, 0f, 1f))
        {
            Color = new EngineVec4(r, g, b, 1.0f);
        }
    }
}
