using System.Collections.Generic;
using ImGuiNET;
using Engine.Core;

namespace Engine.Editor;

public class ConsoleWindow
{
    private readonly List<LogEntry> _logs = new List<LogEntry>();
    private bool _autoScroll = true;
    private bool _showDebug = true;
    private bool _showInfo = true;
    private bool _showWarning = true;
    private bool _showError = true;

    public bool Visible { get; set; } = true;

    public void AddLog(LogEntry entry)
    {
        _logs.Add(entry);
        if (_logs.Count > 1000)
            _logs.RemoveAt(0);
    }

    public void Render()
    {
        if (!Visible)
            return;

        bool visible = Visible;
        if (ImGui.Begin("Console", ref visible))
        {
            Visible = visible;

            if (ImGui.BeginPopupContextWindow())
            {
                if (ImGui.MenuItem("Clear"))
                {
                    _logs.Clear();
                    Logger.Clear();
                }
                ImGui.EndPopup();
            }

            ImGui.Checkbox("Auto-scroll", ref _autoScroll);
            ImGui.SameLine();
            ImGui.Checkbox("Debug", ref _showDebug);
            ImGui.SameLine();
            ImGui.Checkbox("Info", ref _showInfo);
            ImGui.SameLine();
            ImGui.Checkbox("Warning", ref _showWarning);
            ImGui.SameLine();
            ImGui.Checkbox("Error", ref _showError);

            ImGui.Separator();

            if (ImGui.BeginChild("ScrollingRegion", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar))
            {
                foreach (var log in _logs)
                {
                    if (!ShouldShow(log.Level))
                        continue;

                    var color = GetColorForLevel(log.Level);
                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    ImGui.TextWrapped($"[{log.Timestamp:HH:mm:ss}] {log.Message}");
                    ImGui.PopStyleColor();
                }

                if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                    ImGui.SetScrollHereY(1.0f);
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }

    private bool ShouldShow(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => _showDebug,
            LogLevel.Info => _showInfo,
            LogLevel.Warning => _showWarning,
            LogLevel.Error => _showError,
            _ => true
        };
    }

    private System.Numerics.Vector4 GetColorForLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f),
            LogLevel.Info => new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            LogLevel.Warning => new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
            LogLevel.Error => new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            _ => new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f)
        };
    }
}
