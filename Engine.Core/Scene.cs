using System;
using System.Collections.Generic;
using System.Linq;

namespace Engine.Core;

public class Scene
{
    private readonly Dictionary<ulong, GameObject> _gameObjects = new Dictionary<ulong, GameObject>();
    private readonly List<GameObject> _gameObjectList = new List<GameObject>();
    private string _name;
    private object? _physicsWorld;

    public string Name
    {
        get => _name;
        set => _name = value ?? "Scene";
    }

    public IReadOnlyList<GameObject> GameObjects => _gameObjectList.AsReadOnly();

    public object? PhysicsWorld
    {
        get => _physicsWorld;
        set => _physicsWorld = value;
    }

    public Scene(string name = "Scene")
    {
        _name = name;
    }

    public GameObject CreateGameObject(string name = "GameObject")
    {
        var gameObject = new GameObject(name);
        AddGameObject(gameObject);
        return gameObject;
    }

    public void AddGameObject(GameObject gameObject)
    {
        if (gameObject.Scene != null)
            throw new InvalidOperationException("GameObject is already in a scene");

        _gameObjects[gameObject.ID] = gameObject;
        _gameObjectList.Add(gameObject);
        gameObject.Scene = this;
    }

    public bool RemoveGameObject(GameObject gameObject)
    {
        if (gameObject.Scene != this)
            return false;

        if (_gameObjects.Remove(gameObject.ID))
        {
            _gameObjectList.Remove(gameObject);
            gameObject.Scene = null;
            return true;
        }
        return false;
    }

    public GameObject? FindGameObject(ulong id)
    {
        _gameObjects.TryGetValue(id, out var gameObject);
        return gameObject;
    }

    public GameObject? FindGameObject(string name)
    {
        return _gameObjectList.FirstOrDefault(go => go.Name == name);
    }

    public GameObject[] FindGameObjects(string name)
    {
        return _gameObjectList.Where(go => go.Name == name).ToArray();
    }

    public T? FindObjectOfType<T>() where T : Component
    {
        foreach (var gameObject in _gameObjectList)
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
                return component;
        }
        return null;
    }

    public T[] FindObjectsOfType<T>() where T : Component
    {
        var results = new List<T>();
        foreach (var gameObject in _gameObjectList)
        {
            results.AddRange(gameObject.GetComponents<T>());
        }
        return results.ToArray();
    }

    public void Start()
    {
        foreach (var gameObject in _gameObjectList)
        {
            gameObject.Start();
        }
    }

    public void Update(float deltaTime)
    {
        foreach (var gameObject in _gameObjectList)
        {
            gameObject.Update(deltaTime);
        }
    }
}

