using System;
using System.IO;
using System.Numerics;
using ImGuiNET;
using Engine.Graphics;
using Engine.Math;

namespace Engine.Editor;

public class MaterialEditorWindow
{
    private readonly EditorApplication _editor;
    private MaterialData? _currentMaterial;
    private string _materialName = "New Material";
    private Engine.Math.Vector4 _diffuseColor = new Engine.Math.Vector4(1, 1, 1, 1);
    private Engine.Math.Vector4 _specularColor = new Engine.Math.Vector4(1, 1, 1, 1);
    private Engine.Math.Vector4 _emissionColor = new Engine.Math.Vector4(0, 0, 0, 1);
    private float _shininess = 32.0f;
    private float _roughness = 0.5f;
    private string _diffuseMapPath = "";
    private string _specularMapPath = "";
    private string _normalMapPath = "";

    public bool Visible { get; set; } = false;

    public MaterialEditorWindow(EditorApplication editor)
    {
        _editor = editor;
    }

    public void Render()
    {
        if (!Visible)
            return;

        bool visible = Visible;
        if (ImGui.Begin("Material Editor", ref visible))
        {
            Visible = visible;

            if (ImGui.Button("New Material"))
            {
                CreateNewMaterial();
            }

            ImGui.SameLine();

            if (ImGui.Button("Load Material"))
            {
                LoadMaterial();
            }

            ImGui.SameLine();

            if (ImGui.Button("Save Material"))
            {
                SaveMaterial();
            }

            ImGui.Separator();

            byte[] nameBuffer = new byte[256];
            int nameLength = _materialName.Length > 255 ? 255 : _materialName.Length;
            System.Text.Encoding.UTF8.GetBytes(_materialName, 0, nameLength, nameBuffer, 0);
            if (ImGui.InputText("Material Name", nameBuffer, 256))
            {
                int nullIndex = Array.IndexOf(nameBuffer, (byte)0);
                if (nullIndex < 0) nullIndex = 256;
                _materialName = System.Text.Encoding.UTF8.GetString(nameBuffer, 0, nullIndex);
            }

            ImGui.Separator();

            if (ImGui.CollapsingHeader("Diffuse", ImGuiTreeNodeFlags.DefaultOpen))
            {
                System.Numerics.Vector4 diffuse = new System.Numerics.Vector4(_diffuseColor.X, _diffuseColor.Y, _diffuseColor.Z, _diffuseColor.W);
                if (ImGui.ColorEdit4("Color", ref diffuse))
                {
                    _diffuseColor = new Engine.Math.Vector4(diffuse.X, diffuse.Y, diffuse.Z, diffuse.W);
                }

                ImGui.Text("Diffuse Map:");
                ImGui.SameLine();
                ImGui.Text(string.IsNullOrEmpty(_diffuseMapPath) ? "None" : Path.GetFileName(_diffuseMapPath));
                ImGui.SameLine();
                if (ImGui.Button("Browse##Diffuse"))
                {
                    _diffuseMapPath = "";
                }
            }

            if (ImGui.CollapsingHeader("Specular"))
            {
                System.Numerics.Vector4 specular = new System.Numerics.Vector4(_specularColor.X, _specularColor.Y, _specularColor.Z, _specularColor.W);
                if (ImGui.ColorEdit4("Color", ref specular))
                {
                    _specularColor = new Engine.Math.Vector4(specular.X, specular.Y, specular.Z, specular.W);
                }

                if (ImGui.DragFloat("Shininess", ref _shininess, 1.0f, 1.0f, 256.0f))
                {
                }

                ImGui.Text("Specular Map:");
                ImGui.SameLine();
                ImGui.Text(string.IsNullOrEmpty(_specularMapPath) ? "None" : Path.GetFileName(_specularMapPath));
                ImGui.SameLine();
                if (ImGui.Button("Browse##Specular"))
                {
                    _specularMapPath = "";
                }
            }

            if (ImGui.CollapsingHeader("Emission"))
            {
                System.Numerics.Vector4 emission = new System.Numerics.Vector4(_emissionColor.X, _emissionColor.Y, _emissionColor.Z, _emissionColor.W);
                if (ImGui.ColorEdit4("Color", ref emission))
                {
                    _emissionColor = new Engine.Math.Vector4(emission.X, emission.Y, emission.Z, emission.W);
                }
            }

            if (ImGui.CollapsingHeader("Surface Properties"))
            {
                if (ImGui.DragFloat("Roughness", ref _roughness, 0.01f, 0.0f, 1.0f))
                {
                }
            }

            if (ImGui.CollapsingHeader("Normal Map"))
            {
                ImGui.Text("Normal Map:");
                ImGui.SameLine();
                ImGui.Text(string.IsNullOrEmpty(_normalMapPath) ? "None" : Path.GetFileName(_normalMapPath));
                ImGui.SameLine();
                if (ImGui.Button("Browse##Normal"))
                {
                    _normalMapPath = "";
                }
            }

            ImGui.Separator();

            if (ImGui.Button("Assign to Selected Object"))
            {
                AssignToSelected();
            }
        }
        ImGui.End();
    }

    private void CreateNewMaterial()
    {
        _materialName = "New Material";
        _diffuseColor = new Engine.Math.Vector4(1, 1, 1, 1);
        _specularColor = new Engine.Math.Vector4(1, 1, 1, 1);
        _emissionColor = new Engine.Math.Vector4(0, 0, 0, 1);
        _shininess = 32.0f;
        _roughness = 0.5f;
        _diffuseMapPath = "";
        _specularMapPath = "";
        _normalMapPath = "";
    }

    private void LoadMaterial()
    {
        string projectPath = _editor.ProjectManager.CurrentProjectPath ?? ".";
        string materialsPath = Path.Combine(projectPath, "Assets", "Materials");
        if (Directory.Exists(materialsPath))
        {
            var files = Directory.GetFiles(materialsPath, "*.mat");
            if (files.Length > 0)
            {
                var materialData = MaterialData.Load(files[0]);
                LoadMaterialData(materialData);
            }
        }
    }

    private void LoadMaterialData(MaterialData data)
    {
        _materialName = data.Name;
        _diffuseColor = data.DiffuseColor;
        _specularColor = data.SpecularColor;
        _emissionColor = data.EmissionColor;
        _shininess = data.Shininess;
        _roughness = data.Roughness;
        _diffuseMapPath = data.DiffuseMapPath ?? "";
        _specularMapPath = data.SpecularMapPath ?? "";
        _normalMapPath = data.NormalMapPath ?? "";
    }

    private void SaveMaterial()
    {
        string projectPath = _editor.ProjectManager.CurrentProjectPath ?? ".";
        string materialsPath = Path.Combine(projectPath, "Assets", "Materials");
        Directory.CreateDirectory(materialsPath);

        var materialData = new MaterialData
        {
            Name = _materialName,
            DiffuseColor = _diffuseColor,
            SpecularColor = _specularColor,
            EmissionColor = _emissionColor,
            Shininess = _shininess,
            Roughness = _roughness,
            DiffuseMapPath = _diffuseMapPath,
            SpecularMapPath = _specularMapPath,
            NormalMapPath = _normalMapPath
        };

        string filePath = Path.Combine(materialsPath, $"{_materialName}.mat");
        materialData.Save(filePath);
    }

    private void AssignToSelected()
    {
        var selected = _editor.SelectedObject;
        if (selected == null)
            return;

        var renderer = selected.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = selected.AddComponent<MeshRenderer>();
            renderer.SetDefaultShader(_editor.DefaultShader);
        }

        var material = new Material
        {
            Color = _diffuseColor
        };

        renderer.Material = material;
    }
}

