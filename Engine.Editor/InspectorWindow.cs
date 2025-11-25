using System;
using ImGuiNET;
using Engine.Core;
using Engine.Graphics;
using Engine.Physics;
using Engine.Math;

namespace Engine.Editor;

public class InspectorWindow
{
    private readonly EditorApplication _editor;

    public InspectorWindow(EditorApplication editor)
    {
        _editor = editor;
    }

    public void Render()
    {
        if (ImGui.Begin("Inspector"))
        {
            var selected = _editor.SelectedObject;
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
            ImGui.OpenPopup("AddComponentPopup");
        }

        if (ImGui.BeginPopup("AddComponentPopup"))
        {
            if (ImGui.MenuItem("Mesh Renderer"))
            {
                var renderer = gameObject.AddComponent<MeshRenderer>();
                if (_editor.DefaultShader != null)
                    renderer.SetDefaultShader(_editor.DefaultShader);
            }
            if (ImGui.MenuItem("Physics"))
            {
                gameObject.AddComponent<PhysicsComponent>();
            }

            bool hasMeshCollider = gameObject.GetComponent<MeshCollider>() != null;
            ImGui.BeginDisabled(hasMeshCollider);
            if (ImGui.MenuItem("Mesh Collider"))
            {
                gameObject.AddComponent<MeshCollider>();
            }
            ImGui.EndDisabled();

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

            System.Numerics.Vector3 euler = new System.Numerics.Vector3(transform.EulerAngles.X, transform.EulerAngles.Y, transform.EulerAngles.Z);
            if (ImGui.DragFloat3("Rotation", ref euler, 1.0f))
            {
                transform.EulerAngles = new Vector3(euler.X, euler.Y, euler.Z);
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

            if (component is MeshRenderer renderer)
            {
                RenderMeshRenderer(renderer);
            }
            else if (component is PhysicsComponent physics)
            {
                RenderPhysicsComponent(physics);
            }

            if (ImGui.Button("Remove"))
            {
                component.GameObject?.RemoveComponent(component);
            }
        }
    }

    private void RenderMeshRenderer(MeshRenderer renderer)
    {
        ImGui.Text($"Has Mesh: {renderer.Mesh != null}");
        ImGui.Text($"Has Material: {renderer.Material != null}");
    }

    private void RenderPhysicsComponent(PhysicsComponent physics)
    {
        bool kinematic = physics.IsKinematic;
        if (ImGui.Checkbox("Is Kinematic", ref kinematic))
        {
            physics.IsKinematic = kinematic;
        }

        float mass = physics.Mass;
        if (ImGui.DragFloat("Mass", ref mass, 0.1f, 0.0f, 1000.0f))
        {
            physics.Mass = mass;
        }
    }
}
