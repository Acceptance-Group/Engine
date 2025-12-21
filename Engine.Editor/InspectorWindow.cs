using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Engine.Core;
using Engine.Graphics;
using Engine.Physics;
using Engine.Math;
using System.Reflection;

namespace Engine.Editor;

public class InspectorWindow
{
    private readonly EditorApplication _editor;
    private System.Numerics.Vector3? _lastEulerAngles;
    private string _componentSearchText = "";
    private readonly byte[] _componentSearchBuffer = new byte[256];

    private class ComponentInfo
    {
        public required string Name { get; set; }
        public required string Category { get; set; }
        public required Action<GameObject> AddAction { get; set; }
        public required Func<GameObject, bool> CanAdd { get; set; }
    }

    private readonly List<ComponentInfo> _availableComponents = new List<ComponentInfo>
    {
        new ComponentInfo
        {
            Name = "Box Collider",
            Category = "Colliders",
            AddAction = (go) => go.AddComponent<BoxCollider>(),
            CanAdd = (go) => true
        },
        new ComponentInfo
        {
            Name = "Mesh Collider",
            Category = "Colliders",
            AddAction = (go) => go.AddComponent<MeshCollider>(),
            CanAdd = (go) => go.GetComponent<MeshCollider>() == null
        },
        new ComponentInfo
        {
            Name = "Sphere Collider",
            Category = "Colliders",
            AddAction = (go) => go.AddComponent<SphereCollider>(),
            CanAdd = (go) => true
        },
        new ComponentInfo
        {
            Name = "Mesh Renderer",
            Category = "Graphics",
            AddAction = (go) => go.AddComponent<MeshRenderer>(),
            CanAdd = (go) => true
        },
        new ComponentInfo
        {
            Name = "Rigidbody",
            Category = "Physics",
            AddAction = (go) => go.AddComponent<Rigidbody>(),
            CanAdd = (go) => true
        }
    };

    public InspectorWindow(EditorApplication editor)
    {
        _editor = editor;
    }

    private GameObject? _lastSelectedObject;

    public void Render()
    {
        if (ImGui.Begin("Inspector"))
        {
            var selected = _editor.SelectedObject;
            if (selected != _lastSelectedObject)
            {
                _lastEulerAngles = null;
                _lastSelectedObject = selected;
            }
            
            if (selected != null)
            {
                RenderGameObject(selected);
            }
            else
            {
                ImGui.Text("No object selected");
            }
        }
        ImGui.End();
    }

    private void RenderGameObject(GameObject gameObject)
    {
        bool active = gameObject.Active;
        if (ImGui.Checkbox("Active", ref active))
        {
            gameObject.Active = active;
        }

        byte[] nameBuffer = new byte[256];
        int nameLength = gameObject.Name.Length > 255 ? 255 : gameObject.Name.Length;
        System.Text.Encoding.UTF8.GetBytes(gameObject.Name, 0, nameLength, nameBuffer, 0);
        if (ImGui.InputText("Name", nameBuffer, 256))
        {
            int nullIndex = Array.IndexOf(nameBuffer, (byte)0);
            if (nullIndex < 0) nullIndex = 256;
            gameObject.Name = System.Text.Encoding.UTF8.GetString(nameBuffer, 0, nullIndex);
        }

        ImGui.Separator();

        RenderTransform(gameObject.Transform);

        ImGui.Separator();

        var components = gameObject.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component is Transform)
                continue;

            RenderComponent(component);
        }

        ImGui.Separator();

        if (ImGui.Button("Add Component"))
        {
            _componentSearchText = "";
            Array.Clear(_componentSearchBuffer, 0, _componentSearchBuffer.Length);
            ImGui.OpenPopup("AddComponentPopup");
        }

        if (ImGui.BeginPopup("AddComponentPopup"))
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##Search", _componentSearchBuffer, 256))
            {
                int nullIndex = Array.IndexOf(_componentSearchBuffer, (byte)0);
                if (nullIndex < 0) nullIndex = 256;
                _componentSearchText = System.Text.Encoding.UTF8.GetString(_componentSearchBuffer, 0, nullIndex).ToLowerInvariant();
            }

            ImGui.Separator();

            var filtered = _availableComponents
                .Where(c => string.IsNullOrEmpty(_componentSearchText) || 
                           c.Name.ToLowerInvariant().Contains(_componentSearchText) ||
                           c.Category.ToLowerInvariant().Contains(_componentSearchText))
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Name)
                .ToList();

            string? currentCategory = null;
            foreach (var component in filtered)
            {
                if (currentCategory != component.Category)
                {
                    if (currentCategory != null)
                    {
                        ImGui.Separator();
                    }
                    currentCategory = component.Category;
                    ImGui.TextDisabled(component.Category);
                }

                bool canAdd = component.CanAdd(gameObject);
                ImGui.BeginDisabled(!canAdd);
                
                if (ImGui.MenuItem(component.Name))
                {
                    component.AddAction(gameObject);
                    if (component.Name == "Mesh Renderer")
                    {
                        var renderer = gameObject.GetComponent<MeshRenderer>();
                        if (renderer != null && _editor.DefaultShader != null)
                        {
                            renderer.SetDefaultShader(_editor.DefaultShader);
                        }
                    }
                }
                
                ImGui.EndDisabled();
            }

            if (filtered.Count == 0)
            {
                ImGui.TextDisabled("No components found");
            }

            ImGui.EndPopup();
        }
    }

    private void RenderTransform(Transform transform)
    {
        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            System.Numerics.Vector3 position = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
            if (ImGui.DragFloat3("Position", ref position, 0.1f))
            {
                transform.Position = new Vector3(position.X, position.Y, position.Z);
            }

            Vector3 currentEuler = transform.EulerAngles;
            System.Numerics.Vector3 currentEulerDegrees = new System.Numerics.Vector3(
                currentEuler.X * 180.0f / MathF.PI,
                currentEuler.Y * 180.0f / MathF.PI,
                currentEuler.Z * 180.0f / MathF.PI
            );
            
            if (!_lastEulerAngles.HasValue || _lastSelectedObject != _editor.SelectedObject)
            {
                _lastEulerAngles = currentEulerDegrees;
            }
            
            System.Numerics.Vector3 euler = _lastEulerAngles.Value;
            System.Numerics.Vector3 lastEuler = _lastEulerAngles.Value;
            
            if (MathF.Abs(euler.X - currentEulerDegrees.X) > 0.1f || 
                MathF.Abs(euler.Y - currentEulerDegrees.Y) > 0.1f || 
                MathF.Abs(euler.Z - currentEulerDegrees.Z) > 0.1f)
            {
                _lastEulerAngles = currentEulerDegrees;
                euler = currentEulerDegrees;
                lastEuler = currentEulerDegrees;
            }
            
            if (ImGui.DragFloat3("Rotation", ref euler, 1.0f))
            {
                Quaternion currentRotation = transform.Rotation;
                Vector3 right = transform.Right;
                Vector3 up = transform.Up;
                Vector3 forward = transform.Forward;
                
                System.Numerics.Vector3 newLastEuler = lastEuler;
                
                if (MathF.Abs(euler.X - lastEuler.X) > 0.001f)
                {
                    float deltaX = (euler.X - lastEuler.X) * MathF.PI / 180.0f;
                    currentRotation = currentRotation * Quaternion.FromAxisAngle(right, deltaX);
                    newLastEuler.X = euler.X;
                }
                
                if (MathF.Abs(euler.Y - lastEuler.Y) > 0.001f)
                {
                    float deltaY = (euler.Y - lastEuler.Y) * MathF.PI / 180.0f;
                    currentRotation = currentRotation * Quaternion.FromAxisAngle(up, deltaY);
                    newLastEuler.Y = euler.Y;
                }
                
                if (MathF.Abs(euler.Z - lastEuler.Z) > 0.001f)
                {
                    float deltaZ = (euler.Z - lastEuler.Z) * MathF.PI / 180.0f;
                    currentRotation = currentRotation * Quaternion.FromAxisAngle(forward, deltaZ);
                    newLastEuler.Z = euler.Z;
                }
                
                transform.Rotation = currentRotation.Normalized();
                _lastEulerAngles = newLastEuler;
            }
            else
            {
                _lastEulerAngles = euler;
            }

            System.Numerics.Vector3 scale = new System.Numerics.Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);
            if (ImGui.DragFloat3("Scale", ref scale, 0.1f))
            {
                transform.Scale = new Vector3(scale.X, scale.Y, scale.Z);
                transform.GameObject?.GetComponent<MeshCollider>()?.RefreshSize(force: true);
            }
        }
    }

    private void RenderComponent(Component component)
    {
        bool enabled = component.Enabled;
        string headerName = $"{component.GetType().Name}##{component.GetHashCode()}";

        if (ImGui.CollapsingHeader(headerName))
        {
            if (ImGui.Checkbox("Enabled", ref enabled))
            {
                component.Enabled = enabled;
            }

            RenderComponentFields(component);

            if (ImGui.Button("Remove"))
            {
                component.GameObject?.RemoveComponent(component);
            }
        }
    }

    private void RenderComponentFields(Component component)
    {
        var type = component.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public;

        foreach (var field in type.GetFields(flags))
        {
            if (field.IsInitOnly)
                continue;

            if (field.Name == nameof(Component.GameObject) || field.Name == nameof(Component.Transform) || field.Name == nameof(Component.Enabled))
                continue;

            var value = field.GetValue(component);
            if (RenderValue(field.Name, field.FieldType, value, v => field.SetValue(component, v)))
                continue;
        }

        foreach (var prop in type.GetProperties(flags))
        {
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            if (prop.Name == nameof(Component.GameObject) || prop.Name == nameof(Component.Transform) || prop.Name == nameof(Component.Enabled))
                continue;

            var indexParams = prop.GetIndexParameters();
            if (indexParams.Length > 0)
                continue;

            object? value;
            try
            {
                value = prop.GetValue(component);
            }
            catch
            {
                continue;
            }

            if (RenderValue(prop.Name, prop.PropertyType, value, v => prop.SetValue(component, v)))
                continue;
        }
    }

    private bool RenderValue(string name, Type type, object? value, Action<object?> setter)
    {
        if (type == typeof(bool))
        {
            bool v = value is bool b && b;
            if (ImGui.Checkbox(name, ref v))
            {
                setter(v);
            }
            return true;
        }

        if (type == typeof(int))
        {
            int v = value is int i ? i : 0;
            if (ImGui.DragInt(name, ref v))
            {
                setter(v);
            }
            return true;
        }

        if (type == typeof(float))
        {
            float v = value is float f ? f : 0f;
            if (ImGui.DragFloat(name, ref v, 0.1f))
            {
                setter(v);
            }
            return true;
        }

        if (type == typeof(double))
        {
            float v = value is double d ? (float)d : 0f;
            if (ImGui.DragFloat(name, ref v, 0.1f))
            {
                setter((double)v);
            }
            return true;
        }

        if (type == typeof(string))
        {
            string s = value as string ?? string.Empty;
            byte[] buffer = new byte[256];
            int len = s.Length > 255 ? 255 : s.Length;
            System.Text.Encoding.UTF8.GetBytes(s, 0, len, buffer, 0);
            if (ImGui.InputText(name, buffer, (uint)buffer.Length))
            {
                int nullIndex = Array.IndexOf(buffer, (byte)0);
                if (nullIndex < 0) nullIndex = buffer.Length;
                string newValue = System.Text.Encoding.UTF8.GetString(buffer, 0, nullIndex);
                setter(newValue);
            }
            return true;
        }

        if (type == typeof(Vector2))
        {
            Vector2 v = value is Vector2 vv ? vv : Vector2.Zero;
            var nv = new System.Numerics.Vector2(v.X, v.Y);
            if (ImGui.DragFloat2(name, ref nv, 0.1f))
            {
                setter(new Vector2(nv.X, nv.Y));
            }
            return true;
        }

        if (type == typeof(Vector3))
        {
            Vector3 v = value is Vector3 vv ? vv : Vector3.Zero;
            var nv = new System.Numerics.Vector3(v.X, v.Y, v.Z);
            if (ImGui.DragFloat3(name, ref nv, 0.1f))
            {
                setter(new Vector3(nv.X, nv.Y, nv.Z));
            }
            return true;
        }

        if (type == typeof(Vector4))
        {
            Vector4 v = value is Vector4 vv ? vv : Vector4.Zero;
            var nv = new System.Numerics.Vector4(v.X, v.Y, v.Z, v.W);
            if (ImGui.DragFloat4(name, ref nv, 0.1f))
            {
                setter(new Vector4(nv.X, nv.Y, nv.Z, nv.W));
            }
            return true;
        }

        if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            if (names.Length == 0)
                return true;

            int current = 0;
            if (value != null)
            {
                var idx = Array.IndexOf(names, value.ToString());
                if (idx >= 0)
                    current = idx;
            }

            if (ImGui.Combo(name, ref current, names, names.Length))
            {
                var parsed = Enum.Parse(type, names[current]);
                setter(parsed);
            }
            return true;
        }

        string text = value != null ? value.ToString() ?? string.Empty : "null";
        ImGui.Text($"{name}: {text}");
        return true;
    }
}
