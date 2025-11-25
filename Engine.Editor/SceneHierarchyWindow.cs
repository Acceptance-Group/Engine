using System.Linq;
using ImGuiNET;
using Engine.Core;

namespace Engine.Editor;

public class SceneHierarchyWindow
{
    private readonly EditorApplication _editor;

    public SceneHierarchyWindow(EditorApplication editor)
    {
        _editor = editor;
    }

    public void Render()
    {
        if (ImGui.Begin("Hierarchy"))
        {
            var scene = _editor.CurrentScene;
            if (scene != null)
            {
                ImGui.Text($"Scene: {scene.Name}");

                if (ImGui.Button("+"))
                {
                    scene.CreateGameObject("GameObject");
                }

                ImGui.Separator();

                foreach (var gameObject in scene.GameObjects.ToArray())
                {
                    RenderGameObject(gameObject);
                }
            }
            else
            {
                ImGui.Text("No scene loaded");
            }
        }
        ImGui.End();
    }

    private void RenderGameObject(GameObject gameObject)
    {
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (_editor.SelectedObject == gameObject)
            flags |= ImGuiTreeNodeFlags.Selected;

        bool hasChildren = gameObject.Transform.Children.Count > 0;
        if (!hasChildren)
            flags |= ImGuiTreeNodeFlags.Leaf;

        bool isOpen = ImGui.TreeNodeEx(gameObject.Name, flags);

        if (ImGui.IsItemClicked())
        {
            _editor.SelectedObject = gameObject;
        }

        if (ImGui.BeginPopupContextItem())
        {
            if (ImGui.MenuItem("Delete"))
            {
                gameObject.Destroy();
                if (_editor.SelectedObject == gameObject)
                    _editor.SelectedObject = null;
            }
            ImGui.EndPopup();
        }

        if (isOpen)
        {
            foreach (var child in gameObject.Transform.Children.ToArray())
            {
                if (child.GameObject != null)
                    RenderGameObject(child.GameObject);
            }
            ImGui.TreePop();
        }
    }
}

