using System.Numerics;
using EngineVec2 = Engine.Math.Vector2;
using EngineVec3 = Engine.Math.Vector3;
using EngineVec4 = Engine.Math.Vector4;
using ImGuiNET;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace Engine.Editor.Graphs.Nodes;

public class ToVector3Node : GraphNode
{
    public ToVector3Node(int id, NVector2 position) 
        : base(id, "To Vector 3", null, position, new NVector2(180f, 140f), new NVector4(0.94f, 0.54f, 0.2f, 1f),
            inputs: new[] { ("X", NodeValueKind.Scalar), ("Y", NodeValueKind.Scalar), ("Z", NodeValueKind.Scalar) },
            outputs: new[] { ("RGB", NodeValueKind.Color) })
    {
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;
            
            float? x = context.GetInputValue<float>(Id, 0);
            float? y = context.GetInputValue<float>(Id, 1);
            float? z = context.GetInputValue<float>(Id, 2);
        EngineVec4 result = new EngineVec4(x ?? 0f, y ?? 0f, z ?? 0f, 1.0f);
        
        var pin = OutputPins[outputIndex];
        
        if (pin.IsSplitPin && pin.ChannelIndex.HasValue)
        {
            return pin.ChannelIndex.Value switch
            {
                0 => result.X,
                1 => result.Y,
                2 => result.Z,
                _ => null
            };
        }
        
        return result;
    }

    public override void DrawInspector()
    {
        ImGui.TextColored(new NVector4(0.7f, 0.7f, 0.7f, 1f), "Combines 3 scalars into RGB color.");
    }
}

public class ToVector4Node : GraphNode
{
    public ToVector4Node(int id, NVector2 position) 
        : base(id, "To Vector 4", null, position, new NVector2(180f, 170f), new NVector4(0.94f, 0.54f, 0.2f, 1f),
            inputs: new[] { ("X", NodeValueKind.Scalar), ("Y", NodeValueKind.Scalar), ("Z", NodeValueKind.Scalar), ("W", NodeValueKind.Scalar) },
            outputs: new[] { ("RGBA", NodeValueKind.Color) })
    {
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;
            
            float? x = context.GetInputValue<float>(Id, 0);
            float? y = context.GetInputValue<float>(Id, 1);
            float? z = context.GetInputValue<float>(Id, 2);
            float? w = context.GetInputValue<float>(Id, 3);
        EngineVec4 result = new EngineVec4(x ?? 0f, y ?? 0f, z ?? 0f, w ?? 0f);
        
        var pin = OutputPins[outputIndex];
        
        if (pin.IsSplitPin && pin.ChannelIndex.HasValue)
        {
            return pin.ChannelIndex.Value switch
            {
                0 => result.X,
                1 => result.Y,
                2 => result.Z,
                3 => result.W,
                _ => null
            };
        }
        
        return result;
    }

    public override void DrawInspector()
    {
        ImGui.TextColored(new NVector4(0.7f, 0.7f, 0.7f, 1f), "Combines 4 scalars into RGBA color.");
    }
}

public class FromVector3Node : GraphNode
{
    public FromVector3Node(int id, NVector2 position) 
        : base(id, "From Vector 3", null, position, new NVector2(180f, 140f), new NVector4(0.94f, 0.54f, 0.2f, 1f),
            inputs: new[] { ("RGB", NodeValueKind.Color) },
            outputs: new[] { ("X", NodeValueKind.Scalar), ("Y", NodeValueKind.Scalar), ("Z", NodeValueKind.Scalar) })
    {
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;

        EngineVec4? input = context.GetInputValue<EngineVec4>(Id, 0);
        if (input.HasValue)
        {
            return outputIndex switch
            {
                0 => input.Value.X,
                1 => input.Value.Y,
                2 => input.Value.Z,
                _ => null
            };
        }
        return outputIndex switch
        {
            0 => 0f,
            1 => 0f,
            2 => 0f,
            _ => null
        };
    }

    public override void DrawInspector()
    {
        ImGui.TextColored(new NVector4(0.7f, 0.7f, 0.7f, 1f), "Splits RGB color into X, Y, Z components.");
    }
}

public class FromVector4Node : GraphNode
{
    public FromVector4Node(int id, NVector2 position) 
        : base(id, "From Vector 4", null, position, new NVector2(180f, 170f), new NVector4(0.94f, 0.54f, 0.2f, 1f),
            inputs: new[] { ("RGBA", NodeValueKind.Color) },
            outputs: new[] { ("X", NodeValueKind.Scalar), ("Y", NodeValueKind.Scalar), ("Z", NodeValueKind.Scalar), ("W", NodeValueKind.Scalar) })
    {
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;

        EngineVec4? input = context.GetInputValue<EngineVec4>(Id, 0);
        if (input.HasValue)
        {
            return outputIndex switch
            {
                0 => input.Value.X,
                1 => input.Value.Y,
                2 => input.Value.Z,
                3 => input.Value.W,
                _ => null
            };
        }
        return outputIndex switch
        {
            0 => 0f,
            1 => 0f,
            2 => 0f,
            3 => 0f,
            _ => null
        };
    }

    public override void DrawInspector()
    {
        ImGui.TextColored(new NVector4(0.7f, 0.7f, 0.7f, 1f), "Splits RGBA color into X, Y, Z, W components.");
    }
}

public class ToScalarNode : GraphNode
{
    public int Component { get; set; } = 0;

    public ToScalarNode(int id, NVector2 position) 
        : base(id, "To Scalar", null, position, new NVector2(200f, 150f), new NVector4(0.27f, 0.37f, 0.6f, 1f),
            inputs: new[] { ("Input", NodeValueKind.Color) },
            outputs: new[] { ("Scalar", NodeValueKind.Scalar) })
    {
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string[] componentNames = { "R", "G", "B", "A" };
        if (Component >= 0 && Component < componentNames.Length)
        {
            Title = $"To Scalar ({componentNames[Component]})";
        }
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;

            EngineVec4? inputNullable = context.GetInputValue<EngineVec4>(Id, 0);
            if (inputNullable.HasValue)
            {
                return Component switch
                {
                    0 => inputNullable.Value.X,
                    1 => inputNullable.Value.Y,
                    2 => inputNullable.Value.Z,
                    3 => inputNullable.Value.W,
                    _ => 0.0f
                };
            }
            return 0.5f;
    }

    public override void DrawInspector()
    {
        ImGui.Text("Component");
        ImGui.Separator();
        int component = Component;
        string[] componentNames = { "R (X)", "G (Y)", "B (Z)", "A (W)" };
        if (ImGui.Combo("Component", ref component, componentNames, componentNames.Length))
        {
            Component = component;
            UpdateTitle();
        }
    }
}

public class ToVector2Node : GraphNode
{
    public int SwizzleX { get; set; } = 0;
    public int SwizzleY { get; set; } = 1;

    public ToVector2Node(int id, NVector2 position) 
        : base(id, "To Vector 2", null, position, new NVector2(200f, 170f), new NVector4(0.27f, 0.37f, 0.6f, 1f),
            inputs: new[] { ("Input", NodeValueKind.Color) },
            outputs: new[] { ("Vector 2", NodeValueKind.UV) })
    {
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;

            EngineVec4? inputNullable = context.GetInputValue<EngineVec4>(Id, 0);
            if (inputNullable.HasValue)
            {
                float[] components = { inputNullable.Value.X, inputNullable.Value.Y, inputNullable.Value.Z, inputNullable.Value.W };
                float x = SwizzleX >= 0 && SwizzleX < 4 ? components[SwizzleX] : 0.0f;
                float y = SwizzleY >= 0 && SwizzleY < 4 ? components[SwizzleY] : 0.0f;
                return new EngineVec2(x, y);
            }
            return new EngineVec2(0.5f, 0.5f);
    }

    public override void DrawInspector()
    {
        ImGui.Text("Swizzle");
        ImGui.Separator();
        int swizzleX = SwizzleX;
        int swizzleY = SwizzleY;
        string[] componentNames = { "R (X)", "G (Y)", "B (Z)", "A (W)" };
        if (ImGui.Combo("X", ref swizzleX, componentNames, componentNames.Length))
        {
            SwizzleX = swizzleX;
        }
        if (ImGui.Combo("Y", ref swizzleY, componentNames, componentNames.Length))
        {
            SwizzleY = swizzleY;
        }
    }
}
