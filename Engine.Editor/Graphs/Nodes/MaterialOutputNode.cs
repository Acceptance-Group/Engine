using System.Numerics;
using ImGuiNET;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace Engine.Editor.Graphs.Nodes;

public class MaterialOutputNode : GraphNode
{
    public MaterialOutputNode(int id, NVector2 position) 
        : base(id, "Material Output", null, position, new NVector2(260f, 320f), new NVector4(0.18f, 0.18f, 0.18f, 1f),
            inputs: new[]
            {
                ("Base Color", NodeValueKind.Color),
                ("Metallic", NodeValueKind.Scalar),
                ("Specular", NodeValueKind.Scalar),
                ("Roughness", NodeValueKind.Scalar),
                ("Normal", NodeValueKind.Normal),
                ("Ambient Occlusion", NodeValueKind.Scalar),
                ("Emission", NodeValueKind.Emission)
            },
            outputs: Array.Empty<(string, NodeValueKind)>())
    {
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        return null;
    }

    public override void DrawInspector()
    {
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "No editable properties");
    }
}

