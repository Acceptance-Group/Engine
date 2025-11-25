using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Engine.Math;

namespace Engine.Graphics;

public class MaterialData
{
    public string Name { get; set; } = "New Material";
    public Vector4 DiffuseColor { get; set; } = Vector4.One;
    public Vector4 SpecularColor { get; set; } = Vector4.One;
    public Vector4 EmissionColor { get; set; } = Vector4.Zero;
    public float Shininess { get; set; } = 32.0f;
    public float Roughness { get; set; } = 0.5f;
    public string? DiffuseMapPath { get; set; }
    public string? SpecularMapPath { get; set; }
    public string? NormalMapPath { get; set; }

    public void Save(string filePath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(this, options);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, json);
    }

    public static MaterialData Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<MaterialData>(json) ?? new MaterialData();
    }
}

