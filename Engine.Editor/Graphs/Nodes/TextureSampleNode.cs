using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Engine.Editor.Graphs;
using Engine.Graphics;
using EngineVec2 = Engine.Math.Vector2;
using EngineVec4 = Engine.Math.Vector4;
using ImGuiNET;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace Engine.Editor.Graphs.Nodes;

public class TextureSampleNode : GraphNode
{
    public string? TexturePath { get; set; }
    private TextureFileData? _cachedTexture;
    private string? _cachedPath;
    private DateTime _cachedTimestamp;
    private string? _loadError;

    public TextureSampleNode(int id, NVector2 position) 
        : base(id, "Texture Sample", null, position, new NVector2(230f, 190f), new NVector4(0.1f, 0.42f, 0.66f, 1f),
            inputs: new[] { ("UVs", NodeValueKind.UV), ("RGBA", NodeValueKind.Color) },
            outputs: new[] { ("RGBA", NodeValueKind.Color) })
    {
        if (InputPins.Count > 1)
        {
            InputPins[1].DefaultValue = EngineVec4.One;
        }
    }

    public override object? EvaluateOutput(int outputIndex, NodeEvaluationContext context)
    {
        if (outputIndex >= OutputPins.Count)
            return null;

            EngineVec2? uvNullable = context.GetInputValue<EngineVec2>(Id, 0);
            EngineVec2 uv = uvNullable.HasValue ? uvNullable.Value : new EngineVec2(0.5f, 0.5f);
        EngineVec4 sampled = SampleTextureBilinear(uv);
        EngineVec4 colorMultiplier = GetColorMultiplier(context);
        EngineVec4 tinted = new EngineVec4(
            sampled.X * colorMultiplier.X,
            sampled.Y * colorMultiplier.Y,
            sampled.Z * colorMultiplier.Z,
            sampled.W * colorMultiplier.W
        );
        
        var pin = OutputPins[outputIndex];
        
        if (pin.IsSplitPin && pin.ChannelIndex.HasValue)
        {
            return pin.ChannelIndex.Value switch
            {
                0 => tinted.X,
                1 => tinted.Y,
                2 => tinted.Z,
                3 => tinted.W,
                _ => 0f
            };
        }
        
        return tinted;
    }

    private EngineVec4 SampleTextureBilinear(EngineVec2 uv)
    {
        if (!TryEnsureTextureLoaded() || _cachedTexture == null)
        {
            return new EngineVec4(0.5f, 0.5f, 0.5f, 1.0f);
        }

        float u = uv.X - MathF.Floor(uv.X);
        if (u < 0f) u += 1f;
        float v = uv.Y - MathF.Floor(uv.Y);
        if (v < 0f) v += 1f;
        v = 1f - v;

        int width = System.Math.Max(_cachedTexture.Width, 1);
        int height = System.Math.Max(_cachedTexture.Height, 1);

        float x = u * width;
        float y = v * height;

        int x0 = System.Math.Clamp((int)MathF.Floor(x), 0, width - 1);
        int y0 = System.Math.Clamp((int)MathF.Floor(y), 0, height - 1);
        int x1 = System.Math.Clamp(x0 + 1, 0, width - 1);
        int y1 = System.Math.Clamp(y0 + 1, 0, height - 1);

        float fx = x - x0;
        float fy = y - y0;

        EngineVec4 c00 = GetPixel(x0, y0);
        EngineVec4 c10 = GetPixel(x1, y0);
        EngineVec4 c01 = GetPixel(x0, y1);
        EngineVec4 c11 = GetPixel(x1, y1);

        EngineVec4 c0 = c00 * (1f - fx) + c10 * fx;
        EngineVec4 c1 = c01 * (1f - fx) + c11 * fx;
        EngineVec4 result = c0 * (1f - fy) + c1 * fy;

        return result;
    }

    private EngineVec4 GetPixel(int x, int y)
    {
        if (_cachedTexture == null)
            return new EngineVec4(0.5f, 0.5f, 0.5f, 1.0f);

        int index = y * _cachedTexture.Width + x;
        if (index >= 0 && index < _cachedTexture.Pixels.Length)
        {
            return _cachedTexture.Pixels[index];
        }
        return new EngineVec4(0.5f, 0.5f, 0.5f, 1.0f);
        }

    private bool TryEnsureTextureLoaded()
    {
        if (string.IsNullOrWhiteSpace(TexturePath))
        {
            _cachedTexture = null;
            _cachedPath = null;
            _loadError = "Texture path is empty.";
            return false;
        }

        string resolvedPath;
        try
        {
            resolvedPath = Path.GetFullPath(TexturePath);
        }
        catch (Exception ex)
        {
            _cachedTexture = null;
            _cachedPath = null;
            _loadError = $"Invalid path: {ex.Message}";
            return false;
        }

        DateTime timestamp = File.Exists(resolvedPath) ? File.GetLastWriteTimeUtc(resolvedPath) : DateTime.MinValue;

        if (_cachedTexture != null &&
            _cachedPath != null &&
            string.Equals(_cachedPath, resolvedPath, StringComparison.OrdinalIgnoreCase) &&
            _cachedTimestamp == timestamp)
        {
            _loadError = null;
            return true;
        }

        try
        {
            _cachedTexture = TextureFileLoader.Load(resolvedPath);
            _cachedPath = resolvedPath;
            _cachedTimestamp = timestamp;
            _loadError = null;
            return true;
        }
        catch (Exception ex)
        {
            _cachedTexture = null;
            _cachedPath = null;
            _loadError = ex.Message;
            return false;
        }
    }

    private void InvalidateCachedTexture()
    {
        _cachedTexture = null;
        _cachedPath = null;
        _loadError = null;
    }

    private EngineVec4 GetColorMultiplier(NodeEvaluationContext context)
    {
        EngineVec4? multiplier = context.GetInputValue<EngineVec4>(Id, 1);
        if (multiplier.HasValue)
            return multiplier.Value;

        if (InputPins.Count > 1 && InputPins[1].DefaultValue is EngineVec4 defaultColor)
            return defaultColor;

        return EngineVec4.One;
    }

    public override void DrawInspector()
    {
        ImGui.Spacing();
        ImGui.Text("Texture");
        ImGui.Separator();
        string texturePath = TexturePath ?? "";
        byte[] buffer = new byte[512];
        if (texturePath.Length > 0)
        {
            byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(texturePath);
            int copyLength = System.Math.Min(pathBytes.Length, buffer.Length - 1);
            Array.Copy(pathBytes, buffer, copyLength);
            buffer[copyLength] = 0;
        }
        else
        {
            buffer[0] = 0;
        }
        
        if (ImGui.InputText("Path", buffer, (uint)buffer.Length))
        {
            int nullIndex = Array.IndexOf<byte>(buffer, 0);
            if (nullIndex >= 0)
        {
                TexturePath = System.Text.Encoding.UTF8.GetString(buffer, 0, nullIndex).Trim();
                if (string.IsNullOrWhiteSpace(TexturePath))
                    TexturePath = null;
            }
            else
            {
                TexturePath = System.Text.Encoding.UTF8.GetString(buffer).Trim();
                if (string.IsNullOrWhiteSpace(TexturePath))
                TexturePath = null;
            }
            InvalidateCachedTexture();
        }

        if (!string.IsNullOrEmpty(_loadError))
        {
            ImGui.TextColored(new NVector4(0.95f, 0.42f, 0.42f, 1f), _loadError);
        }
        else if (_cachedTexture != null)
        {
            ImGui.TextColored(new NVector4(0.6f, 0.8f, 0.6f, 1f), $"{_cachedTexture.Width}x{_cachedTexture.Height}");
        }

        ImGui.Spacing();
        }
    }
