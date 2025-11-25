using System;
using System.IO;
using System.Text.Json;

namespace Engine.Editor;

public class ProjectManager
{
    public string? CurrentProjectPath { get; private set; }

    public void NewProject()
    {
        string projectName = $"Project_{DateTime.Now:yyyyMMdd_HHmmss}";
        string projectPath = Path.Combine(Environment.CurrentDirectory, "Projects", projectName);
        
        CurrentProjectPath = projectPath;
        Directory.CreateDirectory(Path.Combine(CurrentProjectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(CurrentProjectPath, "Scenes"));
        Directory.CreateDirectory(Path.Combine(CurrentProjectPath, "Settings"));

        var projectData = new
        {
            Name = projectName,
            Version = "1.0.0",
            Created = DateTime.Now
        };

        string json = JsonSerializer.Serialize(projectData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(CurrentProjectPath, "project.json"), json);
    }

    public void OpenProject()
    {
        string projectsPath = Path.Combine(Environment.CurrentDirectory, "Projects");
        if (!Directory.Exists(projectsPath))
        {
            Directory.CreateDirectory(projectsPath);
            return;
        }

        var projectDirs = Directory.GetDirectories(projectsPath);
        if (projectDirs.Length > 0)
        {
            string projectPath = projectDirs[0];
            string projectFile = Path.Combine(projectPath, "project.json");
            if (File.Exists(projectFile))
            {
                CurrentProjectPath = projectPath;
            }
        }
    }

    public void SaveProject()
    {
        if (CurrentProjectPath == null)
        {
            NewProject();
        }
    }
}

