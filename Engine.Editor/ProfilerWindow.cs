using System.Collections.Generic;
using ImGuiNET;

namespace Engine.Editor;

public class ProfilerWindow
{
    private readonly Queue<float> _fpsHistory = new Queue<float>();
    private readonly Queue<float> _frameTimeHistory = new Queue<float>();
    private readonly int _maxHistorySize = 100;
    private float _currentFPS = 0.0f;
    private float _currentFrameTime = 0.0f;
    private float _deltaTime = 0.0f;

    public bool Visible { get; set; } = true;

    public void BeginFrame()
    {
    }

    public void EndFrame(float deltaTime)
    {
        _deltaTime = deltaTime;
        _currentFrameTime = deltaTime * 1000.0f;
        _currentFPS = deltaTime > 0.0f ? 1.0f / deltaTime : 0.0f;

        _fpsHistory.Enqueue(_currentFPS);
        _frameTimeHistory.Enqueue(_currentFrameTime);

        if (_fpsHistory.Count > _maxHistorySize)
            _fpsHistory.Dequeue();
        if (_frameTimeHistory.Count > _maxHistorySize)
            _frameTimeHistory.Dequeue();
    }

    public void Render()
    {
        if (!Visible)
            return;

        bool visible = Visible;
        if (ImGui.Begin("Profiler", ref visible))
        {
            Visible = visible;

            ImGui.Text($"FPS: {_currentFPS:F1}");
            ImGui.Text($"Frame Time: {_currentFrameTime:F2} ms");
            ImGui.Text($"Delta Time: {_deltaTime:F4} s");

            ImGui.Separator();

            if (_fpsHistory.Count > 0)
            {
                float[] fpsArray = _fpsHistory.ToArray();
                ImGui.PlotLines("FPS", ref fpsArray[0], fpsArray.Length, 0, "", 0, 200, new System.Numerics.Vector2(0, 100));
            }

            if (_frameTimeHistory.Count > 0)
            {
                float[] frameTimeArray = _frameTimeHistory.ToArray();
                ImGui.PlotLines("Frame Time (ms)", ref frameTimeArray[0], frameTimeArray.Length, 0, "", 0, 100, new System.Numerics.Vector2(0, 100));
            }
        }
        ImGui.End();
    }
}
