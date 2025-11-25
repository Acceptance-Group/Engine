using System;
using System.Collections.Generic;
using System.Linq;

namespace Engine.Core;

public class GameObject
{
    private readonly Dictionary<Type, List<Component>> _components = new Dictionary<Type, List<Component>>();
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
        if (type == typeof(Transform) && _transform != null && component != _transform)
            throw new InvalidOperationException("GameObject already has a Transform");

        if (!_components.TryGetValue(type, out var list))
        {
            list = new List<Component>();
            _components[type] = list;
        }

        list.Add(component);
        _componentList.Add(component);
        component.GameObject = this;
        if (component is Transform transform)
            _transform = transform;
        component.OnAttached();
    }

    public T? GetComponent<T>() where T : Component
    {
        Type type = typeof(T);
        if (_components.TryGetValue(type, out var components) && components.Count > 0)
            return (T)components[0];

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
        var component = _componentList.FirstOrDefault(c => c is T);
        return component != null && RemoveComponent(component);
    }

    public bool RemoveComponent(Component component)
    {
        if (component.GameObject != this)
            return false;

        Type type = component.GetType();
        if (!_components.TryGetValue(type, out var list))
            return false;

        if (!list.Remove(component))
            return false;

        if (list.Count == 0)
            _components.Remove(type);

        _componentList.Remove(component);
        component.OnDetached();
        component.GameObject = null;

        if (component == _transform)
            _transform = null;

        return true;
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

