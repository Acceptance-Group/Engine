using System;
using System.Collections.Generic;
using System.Linq;

namespace Engine.Core;

public class GameObject
{
    private readonly Dictionary<Type, Component> _components = new Dictionary<Type, Component>();
    private readonly List<Component> _componentList = new List<Component>();
    private Transform? _transform;
    private string _name;
    private bool _active = true;

    public ulong ID { get; }
    public string Name
    {
        get => _name;
        set => _name = value ?? "GameObject";
    }

    public bool Active
    {
        get => _active;
        set => _active = value;
    }

    public Transform Transform
    {
        get
        {
            if (_transform == null)
            {
                _transform = GetComponent<Transform>();
                if (_transform == null)
                {
                    _transform = new Transform();
                    AddComponent(_transform);
                }
            }
            return _transform;
        }
    }

    public Scene? Scene { get; internal set; }

    public GameObject(string name = "GameObject")
    {
        ID = IDGenerator.Generate();
        _name = name;
    }

    public T AddComponent<T>() where T : Component, new()
    {
        var component = new T();
        AddComponent(component);
        return component;
    }

    public void AddComponent(Component component)
    {
        if (component.GameObject != null)
            throw new InvalidOperationException("Component is already attached to a GameObject");

        Type type = component.GetType();
        if (_components.ContainsKey(type))
            throw new InvalidOperationException($"Component of type {type.Name} already exists");

        _components[type] = component;
        _componentList.Add(component);
        component.GameObject = this;
        component.OnAttached();
    }

    public T? GetComponent<T>() where T : Component
    {
        Type type = typeof(T);
        if (_components.TryGetValue(type, out var component))
            return (T)component;

        foreach (var comp in _componentList)
        {
            if (comp is T result)
                return result;
        }

        return null;
    }

    public T[] GetComponents<T>() where T : Component
    {
        return _componentList.OfType<T>().ToArray();
    }

    public bool RemoveComponent<T>() where T : Component
    {
        Type type = typeof(T);
        if (_components.TryGetValue(type, out var component))
        {
            _components.Remove(type);
            _componentList.Remove(component);
            component.OnDetached();
            component.GameObject = null;
            return true;
        }
        return false;
    }

    public bool RemoveComponent(Component component)
    {
        if (component.GameObject != this)
            return false;

        Type type = component.GetType();
        if (_components.Remove(type))
        {
            _componentList.Remove(component);
            component.OnDetached();
            component.GameObject = null;
            return true;
        }
        return false;
    }

    internal void Start()
    {
        if (!_active) return;

        foreach (var component in _componentList)
        {
            if (component.Enabled)
                component.OnStart();
        }
    }

    public void Update(float deltaTime)
    {
        if (!_active) return;

        foreach (var component in _componentList)
        {
            if (component.Enabled)
                component.OnUpdate(deltaTime);
        }
    }

    public void Destroy()
    {
        Scene?.RemoveGameObject(this);
    }
}

