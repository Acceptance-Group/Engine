using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Engine.Core;
using Engine.Math;

namespace Engine.Editor;

public class SceneSerializer
{
    public void Save(Scene scene, string filePath)
    {
        var sceneData = new SceneData
        {
            Name = scene.Name,
            GameObjects = new List<GameObjectData>()
        };

        foreach (var gameObject in scene.GameObjects)
        {
            sceneData.GameObjects.Add(SerializeGameObject(gameObject));
        }

        string json = JsonSerializer.Serialize(sceneData, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, json);
    }

    public Scene Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var sceneData = JsonSerializer.Deserialize<SceneData>(json);

        if (sceneData == null)
            throw new Exception("Failed to deserialize scene");

        var scene = new Scene(sceneData.Name);

        var gameObjectMap = new Dictionary<ulong, GameObject>();

        foreach (var goData in sceneData.GameObjects)
        {
            var gameObject = DeserializeGameObject(goData);
            gameObjectMap[gameObject.ID] = gameObject;
            scene.AddGameObject(gameObject);
        }

        foreach (var goData in sceneData.GameObjects)
        {
            if (goData.ParentID.HasValue && gameObjectMap.TryGetValue(goData.ParentID.Value, out var parent))
            {
                if (gameObjectMap.TryGetValue(goData.ID, out var child))
                {
                    child.Transform.Parent = parent.Transform;
                }
            }
        }

        return scene;
    }

    private GameObjectData SerializeGameObject(GameObject gameObject)
    {
        var transform = gameObject.Transform;
        return new GameObjectData
        {
            ID = gameObject.ID,
            Name = gameObject.Name,
            Active = gameObject.Active,
            Position = new float[] { transform.Position.X, transform.Position.Y, transform.Position.Z },
            Rotation = new float[] { transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W },
            Scale = new float[] { transform.Scale.X, transform.Scale.Y, transform.Scale.Z },
            ParentID = transform.Parent?.GameObject?.ID
        };
    }

    private GameObject DeserializeGameObject(GameObjectData data)
    {
        var gameObject = new GameObject(data.Name)
        {
            Active = data.Active
        };

        gameObject.Transform.Position = new Vector3(data.Position[0], data.Position[1], data.Position[2]);
        gameObject.Transform.Rotation = new Quaternion(data.Rotation[0], data.Rotation[1], data.Rotation[2], data.Rotation[3]);
        gameObject.Transform.Scale = new Vector3(data.Scale[0], data.Scale[1], data.Scale[2]);

        return gameObject;
    }

    private class SceneData
    {
        public string Name { get; set; } = "";
        public List<GameObjectData> GameObjects { get; set; } = new();
    }

    private class GameObjectData
    {
        public ulong ID { get; set; }
        public string Name { get; set; } = "";
        public bool Active { get; set; } = true;
        public float[] Position { get; set; } = new float[3];
        public float[] Rotation { get; set; } = new float[4];
        public float[] Scale { get; set; } = new float[3];
        public ulong? ParentID { get; set; }
    }
}

